using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Timeline;

/// <summary>The order effects walk the target fixtures in (chases, rainbow spread, wipes). Sorting is by stage X and stable. Saved by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FixtureOrder>))]
public enum FixtureOrder
{
    /// <summary>Explicit fixtures first, then group members, as listed.</summary>
    AsListed,

    /// <summary>Audience left to right (ascending X).</summary>
    LeftToRight,

    /// <summary>Audience right to left (descending X).</summary>
    RightToLeft,

    /// <summary>Nearest to the average X first.</summary>
    CenterOut,

    /// <summary>Furthest from the average X first.</summary>
    OutsideIn,
}
