using Lampion.Core.Model.Animation;

namespace Lampion.Core.Model.Timeline;

/// <summary>A transition from the clip that ends where this one starts, centred on the cut.</summary>
public sealed class Transition
{
    /// <summary>The kind of transition.</summary>
    public TransitionKind Kind { get; set; } = TransitionKind.Crossfade;

    /// <summary>Seconds, centred on the cut; 0 or less is a hard cut.</summary>
    public double Duration { get; set; } = 1;

    /// <summary>Curve of the transition's progress.</summary>
    public Easing Easing { get; set; } = Easing.EaseInOut;

    /// <summary>An independent copy.</summary>
    public Transition Clone() => new() { Kind = Kind, Duration = Duration, Easing = Easing };
}
