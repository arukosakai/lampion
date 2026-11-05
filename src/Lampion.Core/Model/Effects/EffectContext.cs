namespace Lampion.Core.Model.Effects;

/// <summary>Where one fixture sits within a clip, for effects that vary in time or across fixtures.</summary>
/// <param name="Time">Seconds into the effect (clip-local time plus the clip's effect offset); may be outside 0..Duration during transitions.</param>
/// <param name="Duration">Seconds the effect is timed over.</param>
/// <param name="Index">The fixture's 0-based place in the track's fixture order.</param>
/// <param name="Count">Number of fixtures the track drives (at least 1 when sampled by the evaluator).</param>
public readonly record struct EffectContext(double Time, double Duration, int Index, int Count)
{
    /// <summary>The fixture's place in the track's order, 0..1 (0.5 when there is only one).</summary>
    public double Spread => Count <= 1 ? 0.5 : Index / (double)(Count - 1);
}
