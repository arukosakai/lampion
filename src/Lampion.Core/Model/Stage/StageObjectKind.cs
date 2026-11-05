using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Stage;

/// <summary>What a piece of set is, which decides how the preview draws it. Saved by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StageObjectKind>))]
public enum StageObjectKind
{
    /// <summary>A lattice bar fixtures hang from; drawn as truss and ignored by beam raycasts.</summary>
    Truss,

    /// <summary>A solid deck or riser.</summary>
    Platform,

    /// <summary>A solid wall or flat.</summary>
    Wall,

    /// <summary>A video surface: shows the timeline's video in the 3D preview.</summary>
    Screen,
}
