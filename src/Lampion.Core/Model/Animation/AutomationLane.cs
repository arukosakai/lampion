using System.Text.Json.Serialization;
using Lampion.Core.Model.Rig;

namespace Lampion.Core.Model.Animation;

/// <summary>A keyframed value on a light clip that drives one <see cref="AutomationTarget"/> on every fixture the clip lights.</summary>
public sealed class AutomationLane
{
    /// <summary>Stable id, so the timeline can keep a key selected across edits.</summary>
    public string Id { get; set; } = Ids.New();

    /// <summary>What the lane drives.</summary>
    public AutomationTarget Target { get; set; }

    /// <summary>The channel role driven when <see cref="Target"/> is <see cref="AutomationTarget.Role"/>.</summary>
    public ChannelRole Role { get; set; } = ChannelRole.Strobe;

    /// <summary>1-based channel within the fixture, for <see cref="AutomationTarget.Channel"/>; channels outside the footprint are ignored.</summary>
    public int Channel { get; set; } = 1;

    /// <summary>Disabled lanes are kept but have no effect.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The After Effects stopwatch. Off: the lane holds one static value (its first key). On: the keys are
    /// interpolated over time.
    /// </summary>
    public bool Animated { get; set; } = true;

    /// <summary>Keys sorted by <see cref="Keyframe.Time"/>; call <see cref="SortKeys"/> after moving one. Empty means the lane does nothing.</summary>
    public List<Keyframe> Keys { get; } = [];

    /// <summary>Name shown on the timeline, e.g. "Pan channel". Allocates for role and channel lanes.</summary>
    [JsonIgnore]
    public string Label =>
        Target switch
        {
            AutomationTarget.HueShift => "Hue shift",
            AutomationTarget.Role => $"{Role} channel",
            AutomationTarget.Channel => $"Channel {Channel}",
            _ => Target.ToString(),
        };

    /// <summary>The value range the timeline draws: 0..1, widened for hue lanes whose keys run past it. Allocation-free.</summary>
    [JsonIgnore]
    public (double Min, double Max) DisplayRange
    {
        get
        {
            if (Target != AutomationTarget.HueShift || Keys.Count == 0)
                return (0, 1);
            var min = Keys[0].Value;
            var max = Keys[0].Value;
            for (var i = 1; i < Keys.Count; i++)
            {
                var v = Keys[i].Value;
                // Matches the LINQ this replaced: Min turns NaN as soon as one value is NaN, Max only when all are.
                if (v < min || double.IsNaN(v))
                    min = double.IsNaN(min) ? min : v;
                if (v > max || double.IsNaN(max))
                    max = v;
            }
            return (Math.Min(0, min), Math.Max(1, max));
        }
    }

    /// <summary>The neutral value for the target: 1 for the multipliers (saturation, intensity, opacity), else 0. Also written to project files.</summary>
    public double DefaultValue => DefaultValueFor(Target);

    /// <summary>The neutral value for <paramref name="target"/>: 1 for the multipliers (saturation, intensity, opacity), else 0.</summary>
    public static double DefaultValueFor(AutomationTarget target) =>
        target switch
        {
            AutomationTarget.HueShift => 0,
            AutomationTarget.Saturation or AutomationTarget.Intensity or AutomationTarget.Opacity => 1,
            _ => 0,
        };

    /// <summary>Restores time order after keys were added or moved.</summary>
    public void SortKeys() => Keys.Sort((a, b) => a.Time.CompareTo(b.Time));

    /// <summary>
    /// The lane's value at <paramref name="time"/> seconds into the clip: <see cref="DefaultValue"/> with no keys,
    /// the first key's value when not animated or before the first key, the last key's after it. Allocation-free.
    /// </summary>
    public double Evaluate(double time)
    {
        var keys = Keys;
        if (keys.Count == 0)
            return DefaultValue;
        if (!Animated || time <= keys[0].Time)
            return keys[0].Value;
        for (var i = 0; i < keys.Count - 1; i++)
        {
            var a = keys[i];
            var b = keys[i + 1];
            if (time >= b.Time)
                continue;
            var span = b.Time - a.Time;
            var t = span <= 1e-9 ? 1 : (time - a.Time) / span;
            return a.Value + (b.Value - a.Value) * EasingCurve.Apply(a.Easing, t);
        }
        return keys[^1].Value;
    }

    /// <summary>A deep copy with a new id.</summary>
    public AutomationLane Clone()
    {
        var copy = new AutomationLane
        {
            Target = Target,
            Role = Role,
            Channel = Channel,
            Enabled = Enabled,
            Animated = Animated,
        };
        foreach (var key in Keys)
            copy.Keys.Add(key.Clone());
        return copy;
    }
}
