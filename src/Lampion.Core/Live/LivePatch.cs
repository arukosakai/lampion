using Lampion.Core.Engine;
using Lampion.Core.Model;
using Lampion.Core.Model.Live;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;
using Lampion.Core.Persistence;

namespace Lampion.Core.Live;

/// <summary>
/// The rig and console compiled for the live engine: a private snapshot of the project (so the editor can keep
/// changing its own copy), every id resolved to an index, and the engine's scratch buffers sized up front so
/// rendering never allocates. Build one on any thread, then hand it to one <see cref="LiveEngine"/>; once
/// handed over it's immutable to everyone else.
/// </summary>
public sealed class LivePatch
{
    private readonly Dictionary<string, int> _faderById = [];
    private readonly Dictionary<string, int> _padById = [];
    private readonly Dictionary<string, int> _fixtureById = [];

    private LivePatch(Project rig)
    {
        Rig = rig;
        Index = new ProjectIndex(rig);
        Index.Rebuild();
        var count = rig.Fixtures.Count;
        States = new FixtureState[count];
        Manual = new FixtureState[count];
        Level = new double[count];
        Hue = new double[count];
        Saturation = new double[count];
        Touched = new bool[count];
        var widest = 1;
        for (var i = 0; i < count; i++)
        {
            _fixtureById.TryAdd(rig.Fixtures[i].Id, i);
            if (Index.ProfileOf(i) is { } profile)
                widest = Math.Max(widest, profile.Channels.Count);
        }
        Channels = new byte[widest];
    }

    /// <summary>Fixtures in the rig; engine buffers and show ownership are indexed like the rig's fixture list.</summary>
    public int FixtureCount => Rig.Fixtures.Count;

    /// <summary>Number of faders compiled (at most <see cref="LiveControls.MaxFaders"/>).</summary>
    public int FaderCount => Faders.Length;

    /// <summary>Number of pads compiled (at most <see cref="LiveControls.MaxPads"/>).</summary>
    public int PadCount => Pads.Length;

    internal Project Rig { get; }

    internal ProjectIndex Index { get; }

    internal CompiledFader[] Faders { get; private set; } = [];

    internal CompiledPad[] Pads { get; private set; } = [];

    internal MidiMap Midi { get; } = new();

    // Engine scratch, indexed by rig fixture; only the engine that owns this patch touches them.
    internal FixtureState[] States { get; }

    internal FixtureState[] Manual { get; }

    internal double[] Level { get; }

    internal double[] Hue { get; }

    internal double[] Saturation { get; }

    internal bool[] Touched { get; }

    internal byte[] Channels { get; }

    /// <summary>Compiles <paramref name="project"/>'s rig and <see cref="Project.Live"/> console. Takes a deep copy; the project is not kept.</summary>
    public static LivePatch Build(Project project)
    {
        var patch = new LivePatch(ProjectSerializer.Deserialize(ProjectSerializer.Serialize(project, indented: false)));
        patch.Compile();
        return patch;
    }

    /// <summary>The position of the fader with this id, or -1.</summary>
    public int FaderIndexOf(string id) => _faderById.TryGetValue(id, out var i) ? i : -1;

    /// <summary>The position of the pad with this id, or -1.</summary>
    public int PadIndexOf(string id) => _padById.TryGetValue(id, out var i) ? i : -1;

    /// <summary>The rig index of the fixture with this id, or -1.</summary>
    public int FixtureIndexOf(string id) => _fixtureById.TryGetValue(id, out var i) ? i : -1;

    /// <summary>The rig fixture at <paramref name="index"/>; read it, don't edit it.</summary>
    public Fixture FixtureAt(int index) => Rig.Fixtures[index];

    /// <summary>The rig indices fader <paramref name="index"/> drives; empty for pad targets.</summary>
    public ReadOnlySpan<int> FaderFixtures(int index) => Faders[index].Fixtures;

    /// <summary>The rig indices pad <paramref name="index"/> drives.</summary>
    public ReadOnlySpan<int> PadFixtures(int index) => Pads[index].Fixtures;

    /// <summary>Pad <paramref name="index"/>'s mode, so presses from the UI and MIDI behave alike.</summary>
    public PadMode PadModeAt(int index) => Pads[index].Mode;

    private void Compile()
    {
        var live = Rig.Live;
        var resolver = new TargetResolver();
        var targets = new List<int>();

        var padCount = Math.Min(live.Pads.Count, LiveControls.MaxPads);
        var pads = new CompiledPad[padCount];
        for (var p = 0; p < padCount; p++)
        {
            var pad = live.Pads[p];
            _padById.TryAdd(pad.Id, p);
            Resolve(resolver, pad.FixtureIds, pad.GroupIds, pad.Order, targets);
            pads[p] = new CompiledPad(pad.Effect, pad.Mode, pad.Blend, [.. targets]);
            Midi.BindPad(pad.Midi, p);
        }

        var faderCount = Math.Min(live.Faders.Count, LiveControls.MaxFaders);
        var faders = new CompiledFader[faderCount];
        for (var f = 0; f < faderCount; f++)
        {
            var fader = live.Faders[f];
            _faderById.TryAdd(fader.Id, f);
            var pad = fader.PadId is { } padId ? PadIndexOf(padId) : -1;
            Resolve(resolver, fader.FixtureIds, fader.GroupIds, FixtureOrder.AsListed, targets);
            faders[f] = new CompiledFader(fader.Target, fader.Role, pad, IsPadTarget(fader.Target) ? [] : [.. targets]);
            Midi.BindFader(fader.Midi, f);
        }
        Pads = pads;
        Faders = faders;
    }

    /// <summary>True for the targets that steer a pad rather than fixtures.</summary>
    internal static bool IsPadTarget(LiveFaderTarget target) =>
        target is LiveFaderTarget.PadIntensity or LiveFaderTarget.PadSpeed or LiveFaderTarget.PadHueShift;

    // The timeline's resolver, so pads walk fixtures in exactly the order a track with the same targets would.
    private void Resolve(TargetResolver resolver, List<string> fixtureIds, List<string> groupIds, FixtureOrder order, List<int> targets)
    {
        var track = new Track { Order = order };
        track.FixtureIds.AddRange(fixtureIds);
        track.GroupIds.AddRange(groupIds);
        resolver.Resolve(Index, track, targets);
    }
}
