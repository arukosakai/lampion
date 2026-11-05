using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Timeline;

/// <summary>Something placed on a track for a span of show time. Saved with a <c>kind</c> discriminator.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LightClip), "light")]
[JsonDerivedType(typeof(MediaClip), "media")]
public abstract class Clip
{
    private protected Clip() { }

    /// <summary>Stable id the selection refers to.</summary>
    public string Id { get; set; } = Ids.New();

    /// <summary>Name drawn on the clip.</summary>
    public string Name { get; set; } = "Clip";

    /// <summary>Seconds on the show timeline.</summary>
    public double Start { get; set; }

    /// <summary>Length in seconds.</summary>
    public double Duration { get; set; } = 4;

    /// <summary>Seconds on the show timeline where the clip stops (exclusive).</summary>
    [JsonIgnore]
    public double End => Start + Duration;

    /// <summary>A deep copy with a new id.</summary>
    public abstract Clip Clone();
}
