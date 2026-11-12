using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Effects;

/// <summary>Which fixture an <see cref="EvenSparkleEffect"/> hit lands on. Saved by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SparkleOrder>))]
public enum SparkleOrder
{
    /// <summary>A fresh seeded shuffle every round, so it looks random while the timing stays even.</summary>
    Shuffled,

    /// <summary>First fixture to last, then wraps.</summary>
    Sequential,
}
