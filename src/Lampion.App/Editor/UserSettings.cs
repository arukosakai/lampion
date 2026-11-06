using Lampion.App.Output;
using Lampion.App.Rendering;

namespace Lampion.App.Editor;

/// <summary>Preferences that belong to this machine, not to a show. Saved as JSON; property names are the file format.</summary>
internal sealed record UserSettings
{
    /// <summary>How much GPU work the 3D stage spends per frame.</summary>
    public StageFidelity Fidelity { get; init; } = StageFidelity.Quality;

    /// <summary>True to overlay the frame rate on the 3D stage.</summary>
    public bool ShowFps { get; init; }

    /// <summary>Where DMX output goes on this machine.</summary>
    public OutputSettings Output { get; init; } = new();
}
