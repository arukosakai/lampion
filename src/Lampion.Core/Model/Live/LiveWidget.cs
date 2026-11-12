namespace Lampion.Core.Model.Live;

/// <summary>A panel in the live console's widget column. Display only; it never affects the lights.</summary>
public sealed class LiveWidget
{
    /// <summary>Stable id.</summary>
    public string Id { get; set; } = Ids.New();

    /// <summary>What it shows.</summary>
    public LiveWidgetKind Kind { get; set; } = LiveWidgetKind.Clock;

    /// <summary>The picture for <see cref="LiveWidgetKind.Photo"/>: relative to the project file when it sits next to it, absolute otherwise.</summary>
    public string ImagePath { get; set; } = "";
}
