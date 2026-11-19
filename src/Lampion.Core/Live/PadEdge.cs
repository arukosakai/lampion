namespace Lampion.Core.Live;

/// <summary>What happened to a pad since <see cref="PadEdges"/> last polled it.</summary>
public enum PadEdge
{
    /// <summary>No change.</summary>
    None,

    /// <summary>It switched on at least once (it may already be off again after a quick tap).</summary>
    On,

    /// <summary>It switched off, without switching on in between.</summary>
    Off,
}
