using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Live;

/// <summary>What a live console widget shows. Saved by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LiveWidgetKind>))]
public enum LiveWidgetKind
{
    /// <summary>The time of day, to the second.</summary>
    Clock,

    /// <summary>A picture of the operator's choosing.</summary>
    Photo,
}
