using Lampion.Core.Engine;

namespace Lampion.Core.Live;

/// <summary>
/// Two show-sized state buffers. The engine evaluates the show into <see cref="Back"/> and swaps only when that
/// finished, so <see cref="Front"/> is always the last complete look: if evaluating ever fails, the show's fixtures
/// hold it instead of going dark. Swapping costs nothing; there is no per-frame copy. Engine thread only.
/// </summary>
internal sealed class ShowLooks(int fixtureCount)
{
    private FixtureState[] _front = new FixtureState[fixtureCount];
    private FixtureState[] _back = new FixtureState[fixtureCount];

    /// <summary>The buffer to evaluate the next look into.</summary>
    public Span<FixtureState> Back => _back;

    /// <summary>The last complete look; all black until the first <see cref="Swap"/>.</summary>
    public ReadOnlySpan<FixtureState> Front => _front;

    /// <summary>Makes the look just evaluated into <see cref="Back"/> the front one.</summary>
    public void Swap() => (_front, _back) = (_back, _front);
}
