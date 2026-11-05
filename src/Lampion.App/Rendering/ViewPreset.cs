namespace Lampion.App.Rendering;

/// <summary>Named views. <see cref="Free"/> is any orbited angle; the rest look straight down an axis.</summary>
internal enum ViewPreset
{
    /// <summary>Orbited freely, perspective or orthographic ("Iso").</summary>
    Free,

    /// <summary>Looking down from above.</summary>
    Top,

    /// <summary>Looking up from below the floor.</summary>
    Bottom,

    /// <summary>From the audience, at +Z.</summary>
    Front,

    /// <summary>From upstage, at -Z.</summary>
    Back,

    /// <summary>From +X.</summary>
    Right,

    /// <summary>From -X.</summary>
    Left,
}
