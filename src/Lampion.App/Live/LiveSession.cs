using System.Diagnostics;
using Lampion.App.Editor;
using Lampion.App.Output;
using Lampion.Core.Live;
using Lampion.Core.Midi;
using Lampion.Core.Model;
using Lampion.Core.Model.Live;
using Lampion.Core.Model.Timeline;
using Lampion.Core.Persistence;

namespace Lampion.App.Live;

/// <summary>
/// Live mode for one workspace: runs the <see cref="LiveLoop"/> on the project's rig, loads set-list shows off the
/// UI thread, routes MIDI, lights the controller's pads and keeps the engine's patch in step with edits.
/// While running, the editor's stage and DMX monitor and the DMX output all show the live frames. UI thread only.
/// </summary>
internal sealed class LiveSession : IDisposable
{
    // Rebuilding the patch serializes the project, so edits mid-drag are folded into a few rebuilds a second.
    private static readonly long RebuildInterval = Stopwatch.Frequency / 4;
    private static readonly long StatsInterval = Stopwatch.Frequency;
    private static readonly long MidiCheckInterval = Stopwatch.Frequency * 2;

    private readonly EditorState _state;
    private readonly DmxOutput _output;
    private readonly PadLeds _leds = new();
    private readonly LiveMedia _media = new();
    private int _patchVersion = -1;
    private Project? _patchProject;
    private long _lastRebuild;
    private long _lastStats;
    private long _midiSeen;
    private Action<MidiBinding>? _learn;
    private bool _ledsDirty;
    private int _loadGeneration;
    private string[] _faderIds = [];
    private string[] _padIds = [];
    private int _faultsSeen;
    private string? _midiErrorSeen;
    private string? _wantedIn;
    private string? _wantedOut;
    private long _lastMidiCheck;
    private LivePower? _power;

    /// <summary>A stopped session over <paramref name="state"/>'s project, sending through <paramref name="output"/>.</summary>
    public LiveSession(EditorState state, DmxOutput output)
    {
        _state = state;
        _output = output;
        Engine = new LiveEngine(Controls);
        Loop = new LiveLoop(Engine);
        Router = new MidiRouter(Controls);
    }

    /// <summary>Fader positions and pad states; the UI writes here like MIDI does.</summary>
    public LiveControls Controls { get; } = new();

    /// <summary>The engine.</summary>
    public LiveEngine Engine { get; }

    /// <summary>The realtime thread driving the engine.</summary>
    public LiveLoop Loop { get; }

    /// <summary>Where MIDI goes.</summary>
    public MidiRouter Router { get; }

    /// <summary>The patch in use, or null before <see cref="Start"/>.</summary>
    public LivePatch? Patch { get; private set; }

    /// <summary>The set-list entry playing, or null.</summary>
    public LiveShow? Playing { get; private set; }

    /// <summary>The audio clip steering the show's clock, or null when the lights run on their own clock.</summary>
    public MediaClip? FollowingMusic => _media.Following;

    /// <summary>The show playing, mapped onto the rig, or null.</summary>
    public ShowCue? Cue => Engine.Scene?.Cue;

    /// <summary>True while a show is being loaded.</summary>
    public bool Loading { get; private set; }

    /// <summary>The open MIDI input, or null.</summary>
    public IMidiInput? MidiIn { get; private set; }

    /// <summary>The open MIDI output (pad lights), or null.</summary>
    public IMidiOutput? MidiOut { get; private set; }

    /// <summary>The last message received, as text, for the activity readout; null until one arrives.</summary>
    public string? LastMidi { get; private set; }

    /// <summary>True while the next MIDI message will be learned.</summary>
    public bool IsLearning => _learn is not null;

    /// <summary>Engine timing over the last second, refreshed by <see cref="Tick"/>.</summary>
    public LiveLoopStats Stats { get; private set; }

    /// <summary>The latest problem in words (show load, MIDI, engine), or null.</summary>
    public string? Problem { get; private set; }

    /// <summary>Shows <paramref name="problem"/> in the live window's problem line.</summary>
    public void Report(string problem)
    {
        Problem = problem;
        Changed?.Invoke();
    }

    /// <summary>Raised when the patch, the playing show or the MIDI devices change, so panels can rebuild.</summary>
    public event Action? Changed;

    /// <summary>Starts the engine thread and routes the editor's monitors and the DMX output to it.</summary>
    public void Start()
    {
        if (Loop.IsRunning)
            return;
        // Live mode owns the speakers: the editor's own playback would play the timeline's music over the show's.
        _state.Pause();
        TryRebuild();
        _power = new LivePower();
        Loop.Start();
        _state.LiveSource = Loop.Frames;
        _output.Source = Loop.Frames;
    }

    /// <summary>Stops the engine and hands the monitors and DMX output back to the timeline.</summary>
    public void Stop()
    {
        if (!Loop.IsRunning)
            return;
        _media.Stop();
        _output.Source = null;
        _state.LiveSource = null;
        Loop.Stop();
        _power?.Dispose();
        _power = null;
        Controls.ReleaseAllPads();
        _leds.Update(MidiOut, _state.Project.Live.Pads, Controls, force: true);
    }

    /// <summary>Loads <paramref name="show"/> off the UI thread and cuts to it when ready.</summary>
    public async Task Go(LiveShow show)
    {
        var generation = ++_loadGeneration;
        Loading = true;
        Problem = null;
        Changed?.Invoke();
        try
        {
            // The project's own timeline is snapshotted here; a file is read on the thread pool.
            var own = string.IsNullOrEmpty(show.Path) ? Copy(_state.Project) : null;
            var path = ShowPaths.Resolve(_state.FilePath, show.Path);
            var patch = Patch ?? throw new InvalidOperationException("Live mode isn't running.");
            var cue = await Task.Run(() => ShowCue.Load(own ?? ProjectSerializer.Load(path), patch, show.Loop));
            if (generation != _loadGeneration)
                return;
            // A rebuild may have swapped the patch while loading.
            if (!ReferenceEquals(cue.Patch, Patch) && Patch is { } current)
                cue = cue.Retarget(current);
            // The cue goes live holding its first frame; the music (or, without music, the clock) then starts it.
            Engine.Scene = new LiveScene(cue.Patch, cue);
            _media.LightDelay = _state.Project.Live.LightDelayMs / 1000;
            IReadOnlyList<string> missing = [];
            string? audioFailure = null;
            bool music;
            try
            {
                music = _media.Play(cue, out missing);
            }
            catch (Exception ex)
            {
                // The show is already on stage; without music it must still run, on its own clock.
                _media.Stop();
                music = false;
                audioFailure = ex.Message;
            }
            if (!music)
                cue.Clock.Start(Stopwatch.GetTimestamp(), 0);
            Playing = show;
            if (audioFailure is not null)
                Problem = $"{show.Name}: the music couldn't start ({audioFailure}); the lights run on their own clock.";
            else if (missing.Count > 0)
                Problem = $"{show.Name}: music not found ({string.Join(", ", missing)}); the lights run on their own clock.";
            else if (_media.Error is { } audio && cue.AudioClips.Count > 0)
                Problem = audio;
            else if (cue.UnmatchedFixtures > 0)
                Problem = $"{show.Name}: {cue.UnmatchedFixtures} fixture(s) aren't in this rig (no matching id or address) and stay dark.";
        }
        catch (Exception ex)
        {
            // Whatever is wrong with the file, the show that is playing now keeps playing.
            Problem = $"Couldn't load {show.Name}: {ex.Message}";
        }
        finally
        {
            if (generation == _loadGeneration)
                Loading = false;
            Changed?.Invoke();
        }
    }

    /// <summary>Stops the show; its fixtures go back to the console.</summary>
    public void StopShow()
    {
        _loadGeneration++;
        Loading = false;
        _media.Stop();
        if (Patch is { } patch)
            Engine.Scene = new LiveScene(patch, null);
        Playing = null;
        Changed?.Invoke();
    }

    /// <summary>Presses pad <paramref name="index"/>, as its MIDI note would.</summary>
    public void PressPad(int index)
    {
        if (Patch is { } patch && index < patch.PadCount)
            Controls.PressPad(index, patch.PadModeAt(index));
    }

    /// <summary>Releases pad <paramref name="index"/>, as its MIDI note off would.</summary>
    public void ReleasePad(int index)
    {
        if (Patch is { } patch && index < patch.PadCount)
            Controls.ReleasePad(index, patch.PadModeAt(index));
    }

    /// <summary>Opens MIDI devices by position in <see cref="MidiDevices"/>' lists; -1 closes that side.</summary>
    public void OpenMidi(int input, int output)
    {
        CloseMidi();
        _wantedIn = input >= 0 && input < MidiDevices.InputNames().Count ? MidiDevices.InputNames()[input] : null;
        _wantedOut = output >= 0 && output < MidiDevices.OutputNames().Count ? MidiDevices.OutputNames()[output] : null;
        try
        {
            if (input >= 0)
                MidiIn = MidiDevices.OpenInput(input, Router.Handle);
            if (output >= 0)
                MidiOut = MidiDevices.OpenOutput(output);
            Problem = null;
            _ledsDirty = true;
        }
        catch (Exception ex) when (ex is IOException or PlatformNotSupportedException)
        {
            Problem = "MIDI: " + ex.Message;
        }
        Changed?.Invoke();
    }

    /// <summary>Calls <paramref name="onLearned"/> with the next control or note that arrives; null cancels.</summary>
    public void Learn(Action<MidiBinding>? onLearned) => _learn = onLearned;

    /// <summary>Per UI frame: rebuilds the patch after edits, finishes MIDI learn, updates pad lights and stats.</summary>
    public void Tick()
    {
        if (!Loop.IsRunning)
            return;
        var now = Stopwatch.GetTimestamp();
        if ((_state.Version != _patchVersion || !ReferenceEquals(_state.Project, _patchProject)) && now - _lastRebuild > RebuildInterval)
            TryRebuild();

        if (Engine.FaultCount != _faultsSeen)
        {
            _faultsSeen = Engine.FaultCount;
            Problem = Engine.LastFault;
        }
        if (Router.Error is { } midiError && !ReferenceEquals(midiError, _midiErrorSeen))
        {
            _midiErrorSeen = midiError;
            Problem = "MIDI message ignored: " + midiError;
        }

        _media.LightDelay = _state.Project.Live.LightDelayMs / 1000;
        _media.Tick();

        if (Router.TryGetLatest(ref _midiSeen, out var message))
        {
            LastMidi = Describe(message);
            if (_learn is { } learn && (message.IsControlChange || message.IsNoteOn))
            {
                _learn = null;
                learn(new MidiBinding(message.IsControlChange ? MidiBindingKind.Control : MidiBindingKind.Note, message.Channel, message.Data1));
            }
        }

        if (_leds.Update(MidiOut, _state.Project.Live.Pads, Controls, _ledsDirty) is { } ledProblem)
            Problem = "MIDI out: " + ledProblem;
        _ledsDirty = false;

        if (now - _lastMidiCheck > MidiCheckInterval)
        {
            _lastMidiCheck = now;
            WatchMidi();
        }

        if (now - _lastStats > StatsInterval)
        {
            _lastStats = now;
            Stats = Loop.TakeStats();
            if (Loop.Error is { } error)
                Problem = "Engine: " + error;
        }
    }

    /// <summary>Stops everything and closes MIDI.</summary>
    public void Dispose()
    {
        Stop();
        CloseMidi();
        _media.Dispose();
    }

    // A controller that drops off the USB bus (a kicked cable, a flaky hub) is closed when it disappears and reopened by
    // name when it comes back, so nobody has to dig through menus mid-show.
    private void WatchMidi()
    {
        if (_wantedIn is null && _wantedOut is null)
            return;
        var inputs = MidiDevices.InputNames();
        var outputs = MidiDevices.OutputNames();
        var changed = false;
        if (_wantedIn is { } inName)
        {
            var at = IndexOf(inputs, inName);
            if (at < 0 && MidiIn is not null)
            {
                MidiIn.Dispose();
                MidiIn = null;
                Problem = $"MIDI controller \"{inName}\" disconnected; plug it back in and it reconnects by itself.";
                changed = true;
            }
            else if (at >= 0 && MidiIn is null && TryOpenMidi(() => MidiIn = MidiDevices.OpenInput(at, Router.Handle)))
            {
                Problem = $"MIDI controller \"{inName}\" reconnected.";
                changed = true;
            }
        }
        if (_wantedOut is { } outName)
        {
            var at = IndexOf(outputs, outName);
            if (at < 0 && MidiOut is not null)
            {
                MidiOut.Dispose();
                MidiOut = null;
                changed = true;
            }
            else if (at >= 0 && MidiOut is null && TryOpenMidi(() => MidiOut = MidiDevices.OpenOutput(at)))
            {
                _ledsDirty = true;
                changed = true;
            }
        }
        if (changed)
            Changed?.Invoke();
    }

    private static int IndexOf(IReadOnlyList<string> names, string name)
    {
        for (var i = 0; i < names.Count; i++)
            if (names[i] == name)
                return i;
        return -1;
    }

    // A device that is back in the list but not ready yet fails to open; the next check tries again.
    private static bool TryOpenMidi(Action open)
    {
        try
        {
            open();
            return true;
        }
        catch (Exception ex) when (ex is IOException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    // An edit that can't be compiled must not touch the show: keep running on the last good patch and say why.
    private void TryRebuild()
    {
        try
        {
            Rebuild();
        }
        catch (Exception ex)
        {
            _patchVersion = _state.Version;
            _patchProject = _state.Project;
            _lastRebuild = Stopwatch.GetTimestamp();
            Problem = "That edit couldn't be applied live (" + ex.Message + "); still running the previous setup.";
        }
    }

    private void Rebuild()
    {
        // Positions shift when faders or pads are added, removed or reordered; carry the live values over by id.
        var faderValues = new Dictionary<string, double>();
        for (var i = 0; i < _faderIds.Length; i++)
            faderValues[_faderIds[i]] = Controls.Fader(i);
        var padsOn = new HashSet<string>();
        for (var i = 0; i < _padIds.Length; i++)
            if (Controls.IsPadOn(i))
                padsOn.Add(_padIds[i]);

        var patch = LivePatch.Build(_state.Project);
        var live = _state.Project.Live;
        _faderIds = live.Faders.Take(patch.FaderCount).Select(f => f.Id).ToArray();
        _padIds = live.Pads.Take(patch.PadCount).Select(p => p.Id).ToArray();
        for (var i = 0; i < _faderIds.Length; i++)
            Controls.SetFader(i, faderValues.GetValueOrDefault(_faderIds[i]));
        for (var i = 0; i < LiveControls.MaxPads; i++)
            Controls.SetPad(i, i < _padIds.Length && padsOn.Contains(_padIds[i]));

        Patch = patch;
        Router.Patch = patch;
        Engine.Scene = new LiveScene(patch, Cue?.Retarget(patch));
        _patchVersion = _state.Version;
        _patchProject = _state.Project;
        _lastRebuild = Stopwatch.GetTimestamp();
        _ledsDirty = true;
        Changed?.Invoke();
    }

    private static Project Copy(Project project) => ProjectSerializer.Deserialize(ProjectSerializer.Serialize(project, indented: false));

    private static string Describe(MidiMessage m) =>
        m.IsControlChange ? $"CC {m.Data1} = {m.Data2} · ch {m.Channel}"
        : m.IsNoteOn ? $"Note {m.Data1} on · ch {m.Channel}"
        : m.IsNoteOff ? $"Note {m.Data1} off · ch {m.Channel}"
        : $"0x{m.Status:X2} {m.Data1} {m.Data2}";

    private void CloseMidi()
    {
        MidiIn?.Dispose();
        MidiIn = null;
        MidiOut?.Dispose();
        MidiOut = null;
    }
}
