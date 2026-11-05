namespace Lampion.App.Views.TrussBuilder;

/// <summary>Which top spans of a truss cube get lights; an arch only has the one.</summary>
internal enum LightSpans
{
    /// <summary>The span facing the audience.</summary>
    Front,

    /// <summary>The front and back spans.</summary>
    FrontAndBack,

    /// <summary>All four sides.</summary>
    All,
}
