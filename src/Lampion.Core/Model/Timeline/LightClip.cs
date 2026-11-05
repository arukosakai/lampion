using System.Text.Json.Serialization;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;

namespace Lampion.Core.Model.Timeline;

/// <summary>An effect on a light track, with fades, an optional transition in and automation lanes.</summary>
public sealed class LightClip : Clip
{
    /// <summary>The look; replaced wholesale when the user picks another effect.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
    public Effect Effect { get; set; } = new SolidEffect();

    /// <summary>Opacity ramp against the tracks below at the start, in seconds.</summary>
    public double FadeIn { get; set; }

    /// <summary>Opacity ramp at the end, in seconds.</summary>
    public double FadeOut { get; set; }

    /// <summary>Curve of both fades.</summary>
    public Easing FadeEasing { get; set; } = Easing.Linear;

    /// <summary>How this clip takes over from the clip ending at its start; null is a hard cut.</summary>
    public Transition? TransitionIn { get; set; }

    /// <summary>Automation lanes, applied in order.</summary>
    public List<AutomationLane> Automation { get; } = [];

    /// <summary>Seconds the effect is already into its run when the clip starts; set when a clip is split.</summary>
    public double EffectOffset { get; set; }

    /// <summary>Length the effect is timed over (e.g. a colour fade's full span), when it differs from the clip; null means the clip's duration.</summary>
    public double? EffectSpan { get; set; }

    /// <inheritdoc />
    public override Clip Clone()
    {
        var copy = new LightClip
        {
            Name = Name,
            Start = Start,
            Duration = Duration,
            EffectOffset = EffectOffset,
            EffectSpan = EffectSpan,
            Effect = Effect.Clone(),
            FadeIn = FadeIn,
            FadeOut = FadeOut,
            FadeEasing = FadeEasing,
            TransitionIn = TransitionIn?.Clone(),
        };
        foreach (var lane in Automation)
            copy.Automation.Add(lane.Clone());
        return copy;
    }

    /// <summary>Opacity from the fades at <paramref name="localTime"/> seconds into the clip, 0..1 (1 outside the fade ramps).</summary>
    public double Envelope(double localTime)
    {
        var level = 1.0;
        if (FadeIn > 0 && localTime < FadeIn)
            level = EasingCurve.Apply(FadeEasing, Math.Max(0, localTime / FadeIn));
        var untilEnd = Duration - localTime;
        if (FadeOut > 0 && untilEnd < FadeOut)
            level = Math.Min(level, EasingCurve.Apply(FadeEasing, Math.Max(0, untilEnd / FadeOut)));
        return level;
    }
}
