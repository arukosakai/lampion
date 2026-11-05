namespace Lampion.App.Rendering;

/// <summary>Procedural surface detail in the lit shader; the values are the shader's <c>uPattern</c> codes.</summary>
internal enum SurfacePattern
{
    /// <summary>Flat colour.</summary>
    Plain = 0,

    /// <summary>Stage deck: painted ply sheets with seams.</summary>
    Deck = 1,

    /// <summary>Polished concrete with the metre grid.</summary>
    HouseFloor = 2,

    /// <summary>Theatrical drape with vertical folds.</summary>
    Drape = 3,

    /// <summary>Brushed aluminium.</summary>
    BrushedMetal = 4,

    /// <summary>LED wall showing the video texture at full brightness.</summary>
    Video = 5,
}
