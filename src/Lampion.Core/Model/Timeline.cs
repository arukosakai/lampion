using System.Text.Json.Serialization;

namespace Lampion.Core.Model;

[JsonConverter(typeof(JsonStringEnumConverter<TrackKind>))]
public enum TrackKind
{
    Light,
    Audio,
    Video,
}

/// <summary>How a light track combines with the tracks below it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BlendMode>))]
public enum BlendMode
{
    /// <summary>Replaces what is below, weighted by clip opacity (fades reveal the lower tracks).</summary>
    Normal,

    /// <summary>Highest takes precedence, channel by channel — the classic lighting-desk merge.</summary>
    Htp,

    /// <summary>Adds light on top.</summary>
    Add,
}

/// <summary>The order effects walk the target fixtures in (chases, rainbow spread, wipes).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FixtureOrder>))]
public enum FixtureOrder
{
    AsListed,
    LeftToRight,
    RightToLeft,
    CenterOut,
    OutsideIn,
}

[JsonConverter(typeof(JsonStringEnumConverter<TransitionKind>))]
public enum TransitionKind
{
    Crossfade,
    DipToBlack,
    Wipe,
}

/// <summary>A transition from the clip that ends where this one starts, centred on the cut.</summary>
public sealed class Transition
{
    public TransitionKind Kind { get; set; } = TransitionKind.Crossfade;
    public double Duration { get; set; } = 1;
    public Easing Easing { get; set; } = Easing.EaseInOut;

    public Transition Clone() => new() { Kind = Kind, Duration = Duration, Easing = Easing };
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LightClip), "light")]
[JsonDerivedType(typeof(MediaClip), "media")]
public abstract class Clip
{
    public string Id { get; set; } = Ids.New();
    public string Name { get; set; } = "Clip";

    /// <summary>Seconds on the show timeline.</summary>
    public double Start { get; set; }

    public double Duration { get; set; } = 4;

    [JsonIgnore]
    public double End => Start + Duration;

    public abstract Clip Clone();
}

public sealed class LightClip : Clip
{
    public Effect Effect { get; set; } = new SolidEffect();

    /// <summary>Opacity ramps against the tracks below, in seconds.</summary>
    public double FadeIn { get; set; }

    public double FadeOut { get; set; }
    public Easing FadeEasing { get; set; } = Easing.Linear;
    public Transition? TransitionIn { get; set; }
    public List<AutomationLane> Automation { get; set; } = [];

    /// <summary>Seconds the effect is already into its run when the clip starts; set when a clip is split.</summary>
    public double EffectOffset { get; set; }

    /// <summary>Length the effect is timed over (e.g. a colour fade's full span), when it differs from the clip.</summary>
    public double? EffectSpan { get; set; }

    public override Clip Clone() =>
        new LightClip
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
            Automation = Automation.Select(a => a.Clone()).ToList(),
        };

    public double Envelope(double localTime)
    {
        var level = 1.0;
        if (FadeIn > 0 && localTime < FadeIn)
            level = AutomationLane.Ease(FadeEasing, Math.Max(0, localTime / FadeIn));
        var untilEnd = Duration - localTime;
        if (FadeOut > 0 && untilEnd < FadeOut)
            level = Math.Min(level, AutomationLane.Ease(FadeEasing, Math.Max(0, untilEnd / FadeOut)));
        return level;
    }
}

public sealed class MediaClip : Clip
{
    /// <summary>Absolute path, or relative to the project file.</summary>
    public string Path { get; set; } = "";

    /// <summary>Seconds into the media file the clip starts at (a left trim).</summary>
    public double MediaOffset { get; set; }

    /// <summary>Length of the whole source file, when known — trims cannot run past it.</summary>
    public double? SourceDuration { get; set; }

    public double Volume { get; set; } = 1;

    public override Clip Clone() =>
        new MediaClip
        {
            Name = Name,
            Start = Start,
            Duration = Duration,
            Path = Path,
            MediaOffset = MediaOffset,
            SourceDuration = SourceDuration,
            Volume = Volume,
        };
}

public sealed class Track
{
    public string Id { get; set; } = Ids.New();
    public string Name { get; set; } = "Track";
    public TrackKind Kind { get; set; } = TrackKind.Light;
    public bool Muted { get; set; }
    public bool Solo { get; set; }
    public bool Locked { get; set; }
    public bool Expanded { get; set; }
    public BlendMode Blend { get; set; } = BlendMode.Normal;
    public FixtureOrder Order { get; set; } = FixtureOrder.AsListed;

    /// <summary>Fixtures this light track drives; groups are expanded at evaluation time.</summary>
    public List<string> FixtureIds { get; set; } = [];

    public List<string> GroupIds { get; set; } = [];
    public List<Clip> Clips { get; set; } = [];

    public void SortClips() => Clips.Sort((a, b) => a.Start.CompareTo(b.Start));
}
