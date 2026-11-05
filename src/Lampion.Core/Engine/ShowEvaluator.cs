using Lampion.Core.Model;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Engine;

/// <summary>
/// Turns the timeline into per-fixture states at a given time. Pure: the same project and time always
/// give the same result, which is what lets the preview, Art-Net output and QLC+ export agree.
/// </summary>
/// <remarks>
/// Reads the project as it is at each call (nothing is cached across calls), so direct edits show up on
/// the next frame. Owns reusable buffers and allocates nothing in steady state; it only grows them when the
/// project grows. Not thread-safe: use one instance per thread.
/// </remarks>
public sealed class ShowEvaluator
{
    private readonly ProjectIndex _index;
    private readonly TargetResolver _resolver = new();
    private readonly List<int> _targets = [];
    private FixtureState[] _states = [];

    /// <summary>An evaluator for <paramref name="project"/>.</summary>
    public ShowEvaluator(Project project) => _index = new ProjectIndex(project);

    /// <summary>The evaluated project.</summary>
    public Project Project => _index.Project;

    /// <summary>Lookups built by the last <see cref="Evaluate(double, Span{FixtureState})"/>, for the renderer.</summary>
    internal ProjectIndex Index => _index;

    /// <summary>
    /// States for every fixture at <paramref name="time"/> seconds, indexed like <see cref="Project.Fixtures"/>,
    /// in a buffer this evaluator owns. The span is only valid until the next call.
    /// </summary>
    public ReadOnlySpan<FixtureState> Evaluate(double time)
    {
        var count = Project.Fixtures.Count;
        if (_states.Length < count)
            _states = new FixtureState[Math.Max(count, _states.Length * 2)];
        Evaluate(time, _states);
        return _states.AsSpan(0, count);
    }

    /// <summary>
    /// Writes the state of every fixture at <paramref name="time"/> seconds into <paramref name="states"/>,
    /// indexed like <see cref="Project.Fixtures"/>. Entries past the fixture count are left untouched.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="states"/> is shorter than the fixture list.</exception>
    public void Evaluate(double time, Span<FixtureState> states)
    {
        _index.Rebuild();
        var count = _index.FixtureCount;
        if (states.Length < count)
            throw new ArgumentException($"Needs room for {count} fixtures, got {states.Length}.", nameof(states));
        states = states[..count];
        states.Clear();

        var tracks = Project.Tracks;
        var anySolo = false;
        for (var i = 0; i < tracks.Count; i++)
            anySolo |= tracks[i].Kind == TrackKind.Light && tracks[i].Solo;

        FixtureState layer = default;
        // The topmost track in the list is drawn on top, as in Premiere / After Effects.
        for (var i = tracks.Count - 1; i >= 0; i--)
        {
            var track = tracks[i];
            if (track.Kind != TrackKind.Light || track.Muted || (anySolo && !track.Solo))
                continue;
            _resolver.Resolve(_index, track, _targets);
            var targetCount = _targets.Count;
            for (var index = 0; index < targetCount; index++)
            {
                var alpha = TrackEvaluator.Evaluate(track, time, index, targetCount, ref layer);
                if (alpha > 0)
                    states[_targets[index]].Blend(in layer, alpha, track.Blend);
            }
        }

        for (var i = 0; i < count; i++)
            if (_index.CanonicalIndex(i) is var canonical && canonical != i)
                states[i] = states[canonical];
    }
}
