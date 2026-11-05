namespace Lampion.Core.Model.Animation;

/// <summary>One key on an automation lane. Edited in place by the timeline, so it stays a class.</summary>
public sealed class Keyframe
{
    /// <summary>Seconds from the start of the clip.</summary>
    public double Time { get; set; }

    /// <summary>The value at <see cref="Time"/>; its range depends on the lane's <see cref="AutomationLane.Target"/>.</summary>
    public double Value { get; set; }

    /// <summary>Curve used from this key to the next one.</summary>
    public Easing Easing { get; set; } = Easing.Linear;

    /// <summary>An independent copy.</summary>
    public Keyframe Clone() => new() { Time = Time, Value = Value, Easing = Easing };
}
