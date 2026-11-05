namespace Lampion.Core.Model.Animation;

/// <summary>Evaluates <see cref="Easing"/> curves. Pure and allocation-free.</summary>
public static class EasingCurve
{
    /// <summary>The eased progress for linear progress <paramref name="t"/>.</summary>
    /// <param name="easing">The curve; unknown values behave as <see cref="Easing.Linear"/>.</param>
    /// <param name="t">Progress, normally 0..1; not clamped, so values outside extrapolate the curve.</param>
    public static double Apply(Easing easing, double t) =>
        easing switch
        {
            Easing.Hold => 0,
            Easing.EaseIn => t * t,
            Easing.EaseOut => 1 - (1 - t) * (1 - t),
            Easing.EaseInOut => t * t * (3 - 2 * t),
            _ => t,
        };
}
