namespace Lampion.Core.Live;

/// <summary>How the live loop kept time since the stats were last taken.</summary>
/// <param name="Frames">Frames rendered.</param>
/// <param name="Seconds">Wall-clock seconds covered.</param>
/// <param name="AverageRenderMs">Mean time spent rendering one frame, in milliseconds.</param>
/// <param name="WorstRenderMs">Longest single render, in milliseconds.</param>
/// <param name="WorstLateMs">Furthest a frame started after it was due, in milliseconds.</param>
/// <param name="Overruns">Frames that started more than one whole period late.</param>
public readonly record struct LiveLoopStats(long Frames, double Seconds, double AverageRenderMs, double WorstRenderMs, double WorstLateMs, long Overruns)
{
    /// <summary>Frames per second actually achieved.</summary>
    public double Rate => Seconds > 0 ? Frames / Seconds : 0;
}
