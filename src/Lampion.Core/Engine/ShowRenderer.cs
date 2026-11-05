using Lampion.Core.Model;

namespace Lampion.Core.Engine;

/// <summary>
/// Renders the show to DMX: evaluates every fixture and maps it onto its profile's channels. Owns its
/// evaluator and output buffers and allocates nothing in steady state. Not thread-safe: use one per thread.
/// </summary>
public sealed class ShowRenderer
{
    private readonly ShowEvaluator _evaluator;
    private readonly DmxFrame _frame = new();
    private readonly FixtureFrame _fixtures = new();
    private byte[] _scratch = new byte[DmxFrame.UniverseSize];

    /// <summary>A renderer for <paramref name="project"/>.</summary>
    public ShowRenderer(Project project) => _evaluator = new ShowEvaluator(project);

    /// <summary>The rendered project.</summary>
    public Project Project => _evaluator.Project;

    /// <summary>
    /// The DMX output at <paramref name="time"/> seconds, in a frame this renderer owns and reuses: the same
    /// instance comes back every call, overwritten. Copy it (<see cref="DmxFrame.CopyTo"/>) to keep a result.
    /// </summary>
    public DmxFrame Render(double time)
    {
        Render(time, _frame);
        return _frame;
    }

    /// <summary>
    /// Renders the DMX output at <paramref name="time"/> seconds into <paramref name="frame"/>, replacing its
    /// contents. Every universe used by a fixture with a profile is present; later fixtures win where
    /// footprints overlap, and channels past 512 are dropped.
    /// </summary>
    public void Render(double time, DmxFrame frame)
    {
        var states = _evaluator.Evaluate(time);
        var index = _evaluator.Index;
        var fixtures = Project.Fixtures;
        frame.Clear();
        for (var i = 0; i < states.Length; i++)
        {
            if (index.ProfileOf(i) is not { } profile)
                continue;
            var fixture = fixtures[i];
            var universe = frame.Universe(fixture.Universe);
            var values = Scratch(profile.Channels.Count);
            DmxMapper.Map(fixture, profile, in index.LayoutOf(i), in states[i], values);
            for (var c = 0; c < values.Length; c++)
            {
                var address = fixture.Address - 1 + c;
                if (address is >= 0 and < DmxFrame.UniverseSize)
                    universe[address] = values[c];
            }
        }
    }

    /// <summary>
    /// Per-fixture channel values at <paramref name="time"/> seconds, without going through universes, in a
    /// frame this renderer owns and reuses (the same instance every call, overwritten).
    /// </summary>
    public FixtureFrame RenderFixtures(double time)
    {
        RenderFixtures(time, _fixtures);
        return _fixtures;
    }

    /// <summary>Renders per-fixture channel values at <paramref name="time"/> seconds into <paramref name="frame"/>, replacing its contents.</summary>
    public void RenderFixtures(double time, FixtureFrame frame)
    {
        var states = _evaluator.Evaluate(time);
        var index = _evaluator.Index;
        var fixtures = Project.Fixtures;
        frame.Reset(states.Length);
        for (var i = 0; i < states.Length; i++)
            if (index.ProfileOf(i) is { } profile)
                DmxMapper.Map(fixtures[i], profile, in index.LayoutOf(i), in states[i], frame.Reserve(i, profile.Channels.Count));
    }

    private Span<byte> Scratch(int length)
    {
        if (_scratch.Length < length)
            _scratch = new byte[Math.Max(length, _scratch.Length * 2)];
        return _scratch.AsSpan(0, length);
    }
}
