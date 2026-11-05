using Lampion.Core.Model;

namespace Lampion.Core.Engine;

/// <summary>Full DMX output at one instant: 512 channels per used universe (keys are 1-based).</summary>
public sealed class DmxFrame
{
    public SortedDictionary<int, byte[]> Universes { get; } = [];

    public byte[] Universe(int number)
    {
        if (!Universes.TryGetValue(number, out var data))
            Universes[number] = data = new byte[512];
        return data;
    }

    /// <summary>The fixture's channels as they ended up in the frame (overlapping fixtures included).</summary>
    public ReadOnlySpan<byte> Slice(Fixture fixture, int channelCount)
    {
        if (!Universes.TryGetValue(fixture.Universe, out var data))
            return ReadOnlySpan<byte>.Empty;
        var start = Math.Clamp(fixture.Address - 1, 0, 512);
        return data.AsSpan(start, Math.Clamp(channelCount, 0, 512 - start));
    }

    /// <summary>A copy with <paramref name="values"/> written over each fixture's channels; this frame is untouched.</summary>
    public DmxFrame With(IEnumerable<Fixture> fixtures, ReadOnlySpan<byte> values)
    {
        var copy = new DmxFrame();
        foreach (var (universe, data) in Universes)
            copy.Universes[universe] = (byte[])data.Clone();
        foreach (var fixture in fixtures)
        {
            var start = Math.Clamp(fixture.Address - 1, 0, 512);
            var count = Math.Min(values.Length, 512 - start);
            values[..count].CopyTo(copy.Universe(fixture.Universe).AsSpan(start, count));
        }
        return copy;
    }
}

public sealed class ShowRenderer(Project project)
{
    private readonly ShowEvaluator _evaluator = new(project);

    public Project Project => _evaluator.Project;

    public DmxFrame Render(double time)
    {
        var frame = new DmxFrame();
        var states = _evaluator.Evaluate(time);
        foreach (var fixture in Project.Fixtures)
        {
            var profile = Project.ProfileOf(fixture);
            if (profile is null)
                continue;
            var universe = frame.Universe(fixture.Universe);
            var values = DmxMapper.Map(fixture, profile, states[fixture.Id]);
            for (var i = 0; i < values.Length; i++)
            {
                var address = fixture.Address - 1 + i;
                if (address is >= 0 and < 512)
                    universe[address] = values[i];
            }
        }
        return frame;
    }

    /// <summary>Per-fixture channel values without going through universes, keyed by fixture id.</summary>
    public Dictionary<string, byte[]> RenderFixtures(double time)
    {
        var states = _evaluator.Evaluate(time);
        var result = new Dictionary<string, byte[]>(Project.Fixtures.Count);
        foreach (var fixture in Project.Fixtures)
            if (Project.ProfileOf(fixture) is { } profile)
                result[fixture.Id] = DmxMapper.Map(fixture, profile, states[fixture.Id]);
        return result;
    }
}
