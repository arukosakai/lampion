using System.Text.Json.Serialization;

namespace Lampion.Core.Model;

/// <summary>
/// What an automation lane drives. The colour targets are semantic: Lampion maps them onto whichever
/// R/G/B/W/dimmer channels each fixture has. Role and Channel write DMX channels directly.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AutomationTarget>))]
public enum AutomationTarget
{
    /// <summary>Hue rotation; 1.0 is one full turn, so 0 → 3 is three trips around the wheel.</summary>
    HueShift,
    Saturation,
    Intensity,
    Opacity,
    White,
    Red,
    Green,
    Blue,

    /// <summary>The first channel with <see cref="AutomationLane.Role"/> on each fixture.</summary>
    Role,

    /// <summary>A fixed 1-based channel within each fixture's footprint.</summary>
    Channel,
}

[JsonConverter(typeof(JsonStringEnumConverter<Easing>))]
public enum Easing
{
    Linear,
    Hold,
    EaseIn,
    EaseOut,
    EaseInOut,
}

public sealed class Keyframe
{
    /// <summary>Seconds from the start of the clip.</summary>
    public double Time { get; set; }

    public double Value { get; set; }

    /// <summary>Curve used from this key to the next one.</summary>
    public Easing Easing { get; set; } = Easing.Linear;

    public Keyframe Clone() => new() { Time = Time, Value = Value, Easing = Easing };
}

public sealed class AutomationLane
{
    public string Id { get; set; } = Ids.New();
    public AutomationTarget Target { get; set; }
    public ChannelRole Role { get; set; } = ChannelRole.Strobe;

    /// <summary>1-based channel within the fixture, for <see cref="AutomationTarget.Channel"/>.</summary>
    public int Channel { get; set; } = 1;

    public bool Enabled { get; set; } = true;
    public List<Keyframe> Keys { get; set; } = [];

    [JsonIgnore]
    public string Label =>
        Target switch
        {
            AutomationTarget.HueShift => "Hue shift",
            AutomationTarget.Role => $"{Role} channel",
            AutomationTarget.Channel => $"Channel {Channel}",
            _ => Target.ToString(),
        };

    /// <summary>The value range the timeline draws; hue may run past 1 for several cycles.</summary>
    [JsonIgnore]
    public (double Min, double Max) DisplayRange =>
        Target == AutomationTarget.HueShift
            ? (Math.Min(0, Keys.Count == 0 ? 0 : Keys.Min(k => k.Value)), Math.Max(1, Keys.Count == 0 ? 1 : Keys.Max(k => k.Value)))
            : (0, 1);

    public double DefaultValue =>
        Target switch
        {
            AutomationTarget.HueShift => 0,
            AutomationTarget.Saturation or AutomationTarget.Intensity or AutomationTarget.Opacity => 1,
            _ => 0,
        };

    public void SortKeys() => Keys.Sort((a, b) => a.Time.CompareTo(b.Time));

    public double Evaluate(double time)
    {
        if (Keys.Count == 0)
            return DefaultValue;
        if (time <= Keys[0].Time)
            return Keys[0].Value;
        for (var i = 0; i < Keys.Count - 1; i++)
        {
            var a = Keys[i];
            var b = Keys[i + 1];
            if (time >= b.Time)
                continue;
            var span = b.Time - a.Time;
            var t = span <= 1e-9 ? 1 : (time - a.Time) / span;
            return a.Value + (b.Value - a.Value) * Ease(a.Easing, t);
        }
        return Keys[^1].Value;
    }

    public static double Ease(Easing easing, double t) =>
        easing switch
        {
            Easing.Hold => 0,
            Easing.EaseIn => t * t,
            Easing.EaseOut => 1 - (1 - t) * (1 - t),
            Easing.EaseInOut => t * t * (3 - 2 * t),
            _ => t,
        };

    public AutomationLane Clone() =>
        new()
        {
            Id = Ids.New(),
            Target = Target,
            Role = Role,
            Channel = Channel,
            Enabled = Enabled,
            Keys = Keys.Select(k => k.Clone()).ToList(),
        };
}
