using Lampion.Core.Engine;
using Lampion.Core.Model;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Live;

/// <summary>
/// A show loaded and mapped onto a <see cref="LivePatch"/>, ready for the engine to play. Owns a private
/// <see cref="Project"/> nobody else edits, and the evaluator for it, which only the engine thread uses
/// after the cue is handed over. Building one is cold work: do it off the engine thread.
/// </summary>
public sealed class ShowCue
{
    private readonly int[] _toRig;
    private readonly bool[] _owned;

    private ShowCue(Project show, ShowEvaluator evaluator, LivePatch patch, bool loop, ShowClock clock, ShowLooks looks)
    {
        Looks = looks;
        Show = show;
        Evaluator = evaluator;
        Patch = patch;
        Loop = loop;
        Clock = clock;
        Duration = show.ContentEnd;
        _toRig = MapFixtures(show, patch, out var unmatched);
        UnmatchedFixtures = unmatched;
        _owned = new bool[patch.FixtureCount];
        OwnedCount = MarkOwned(show, _toRig, _owned);
        AudioClips = show.Tracks.Where(t => t.Kind == TrackKind.Audio && !t.Muted).SelectMany(t => t.Clips.OfType<MediaClip>()).OrderBy(c => c.Start).ToList();
    }

    /// <summary>The show's name.</summary>
    public string Name => Show.Name;

    /// <summary>Seconds until the show is over (its <see cref="Project.ContentEnd"/>).</summary>
    public double Duration { get; }

    /// <summary>Start over at the end instead of stopping.</summary>
    public bool Loop { get; }

    /// <summary>The patch the fixture mapping was made for.</summary>
    public LivePatch Patch { get; }

    /// <summary>Rig fixtures the show drives; the console can't touch them while it plays.</summary>
    public int OwnedCount { get; }

    /// <summary>Show fixtures with no counterpart in the rig, matched neither by id nor by universe and address. They stay dark.</summary>
    public int UnmatchedFixtures { get; }

    internal Project Show { get; }

    internal ShowEvaluator Evaluator { get; }

    /// <summary>The engine's evaluation buffers for this show, shared with <see cref="Retarget"/>ed copies.</summary>
    internal ShowLooks Looks { get; }

    /// <summary>
    /// The show's time. Shared by a cue and its <see cref="Retarget"/>ed copies, so the show keeps its place across a patch
    /// change. Starts holding at 0: start it, or let the music's position reports start it.
    /// </summary>
    public ShowClock Clock { get; }

    /// <summary>Audio clips on the show's unmuted audio tracks, by start time; the live media player plays them and steers <see cref="Clock"/> by them.</summary>
    public IReadOnlyList<MediaClip> AudioClips { get; }

    /// <summary>
    /// Prepares <paramref name="show"/> to play on <paramref name="patch"/>. The project becomes the cue's:
    /// don't edit or reuse it afterwards. Evaluates it once so its buffers are grown before going live.
    /// </summary>
    public static ShowCue Load(Project show, LivePatch patch, bool loop)
    {
        var evaluator = new ShowEvaluator(show);
        evaluator.Evaluate(0);
        return new ShowCue(show, evaluator, patch, loop, new ShowClock(), new ShowLooks(show.Fixtures.Count));
    }

    /// <summary>The same show, still at the same point in time, mapped onto a rebuilt patch.</summary>
    public ShowCue Retarget(LivePatch patch) => new(Show, Evaluator, patch, Loop, Clock, Looks);

    /// <summary>True when the show drives rig fixture <paramref name="rigIndex"/>.</summary>
    public bool Owns(int rigIndex) => (uint)rigIndex < (uint)_owned.Length && _owned[rigIndex];

    /// <summary>The rig index of show fixture <paramref name="showIndex"/>, or -1.</summary>
    internal int RigIndexOf(int showIndex) => _toRig[showIndex];

    /// <summary>Ownership by rig index; the engine reads it every tick.</summary>
    internal ReadOnlySpan<bool> Owned => _owned;

    private static int[] MapFixtures(Project show, LivePatch patch, out int unmatched)
    {
        var byAddress = new Dictionary<(int, int), int>();
        for (var i = 0; i < patch.FixtureCount; i++)
        {
            var fixture = patch.FixtureAt(i);
            byAddress.TryAdd((fixture.Universe, fixture.Address), i);
        }
        var map = new int[show.Fixtures.Count];
        unmatched = 0;
        for (var i = 0; i < map.Length; i++)
        {
            var fixture = show.Fixtures[i];
            var rig = patch.FixtureIndexOf(fixture.Id);
            if (rig < 0 && !byAddress.TryGetValue((fixture.Universe, fixture.Address), out rig))
                rig = -1;
            map[i] = rig;
            if (rig < 0)
                unmatched++;
        }
        return map;
    }

    // A fixture is the show's if any light track that can play targets it. Decided once per cue rather than per
    // frame, so a fader never works one second and is overruled the next because a clip happened to start.
    private static int MarkOwned(Project show, int[] toRig, bool[] owned)
    {
        var anySolo = show.Tracks.Exists(t => t.Kind == TrackKind.Light && t.Solo);
        var count = 0;
        foreach (var track in show.Tracks)
        {
            if (track.Kind != TrackKind.Light || track.Muted || (anySolo && !track.Solo) || track.Clips.Count == 0)
                continue;
            foreach (var fixture in TrackTargets.Resolve(show, track))
            {
                var rig = toRig[show.Fixtures.IndexOf(fixture)];
                if (rig >= 0 && !owned[rig])
                {
                    owned[rig] = true;
                    count++;
                }
            }
        }
        return count;
    }
}
