using System.Text.Json.Serialization;

namespace Lampion.Core.Model.Rig;

/// <summary>How the preview draws a fixture's body and beam. Saved by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BeamKind>))]
public enum BeamKind
{
    /// <summary>A round can with a soft-edged cone.</summary>
    Par,

    /// <summary>A wide, soft wash light.</summary>
    Wash,

    /// <summary>A hard-edged spot, typically a moving head.</summary>
    Spot,

    /// <summary>A linear LED batten.</summary>
    Bar,

    /// <summary>A strobe panel.</summary>
    Strobe,

    /// <summary>An audience blinder.</summary>
    Blinder,
}
