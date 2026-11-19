namespace Lampion.Core.Live;

/// <summary>
/// Turns <see cref="LiveControls"/>' pad generations into on/off edges for a poller that runs slower than presses
/// arrive (a UI timer), so a tap that starts and ends between two polls still counts. Not thread-safe; one poller.
/// </summary>
public sealed class PadEdges
{
    private readonly int[] _seen = new int[LiveControls.MaxPads];

    /// <summary>Pad <paramref name="index"/>'s edge since the last call for it.</summary>
    public PadEdge Poll(LiveControls controls, int index)
    {
        if ((uint)index >= LiveControls.MaxPads)
            return PadEdge.None;
        var generation = controls.PadGeneration(index);
        var seen = _seen[index];
        _seen[index] = generation;
        if (generation == seen)
            return PadEdge.None;
        // Odd generations are "on": the range (seen, generation] holds one exactly when it switched on in between.
        var firstOn = (seen & 1) == 0 ? seen + 1 : seen + 2;
        return generation >= firstOn ? PadEdge.On : PadEdge.Off;
    }

    /// <summary>Takes every pad's current state as seen, so changes made without a press (a patch rebuild) don't count.</summary>
    public void Skip(LiveControls controls)
    {
        for (var i = 0; i < LiveControls.MaxPads; i++)
            _seen[i] = controls.PadGeneration(i);
    }
}
