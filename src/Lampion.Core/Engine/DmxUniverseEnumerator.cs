namespace Lampion.Core.Engine;

/// <summary>Allocation-free enumerator over a <see cref="DmxFrame"/>'s universes, in ascending number order. Don't modify the frame while enumerating.</summary>
public struct DmxUniverseEnumerator
{
    private readonly DmxFrame _frame;
    private int _index;

    /// <summary>Positions the enumerator before the frame's first universe.</summary>
    internal DmxUniverseEnumerator(DmxFrame frame)
    {
        _frame = frame;
        _index = -1;
    }

    /// <summary>Advances to the next universe.</summary>
    public bool MoveNext() => ++_index < _frame.UniverseCount;

    /// <summary>The current universe.</summary>
    public readonly DmxUniverse Current => new(_frame.NumberAt(_index), _frame.ChannelsAt(_index));
}
