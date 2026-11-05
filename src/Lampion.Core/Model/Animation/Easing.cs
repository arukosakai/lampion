using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Animation;

/// <summary>A curve from 0 to 1, used between keyframes, for fades and for transitions. Saved by name; see <see cref="EasingCurve"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Easing>))]
public enum Easing
{
    /// <summary>Straight line.</summary>
    Linear,

    /// <summary>Stays at the start value until the next key (a step).</summary>
    Hold,

    /// <summary>Starts slow (quadratic).</summary>
    EaseIn,

    /// <summary>Ends slow (quadratic).</summary>
    EaseOut,

    /// <summary>Slow at both ends (smoothstep).</summary>
    EaseInOut,
}
