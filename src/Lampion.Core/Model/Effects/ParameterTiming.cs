namespace Lampion.Core.Model.Effects;

/// <summary>How an <see cref="AnimatableParameter"/> relates to the effect's clock, which decides how a keyframed value is applied.</summary>
public enum ParameterTiming
{
    /// <summary>A plain value, used as-is at each instant (decay, duty, spread, ...).</summary>
    Plain,

    /// <summary>
    /// Cycles per second (rate, speed). Keyframed, it's integrated over time so the effect speeds up and
    /// slows down smoothly instead of jumping to wherever the new speed would have put it.
    /// </summary>
    Frequency,

    /// <summary>Seconds per step (step time). Integrated like <see cref="Frequency"/>, as 1 / value.</summary>
    Period,
}
