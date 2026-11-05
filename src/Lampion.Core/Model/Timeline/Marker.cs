namespace Lampion.Core.Model.Timeline;

/// <summary>A labelled point on the timeline the playhead and clips snap to.</summary>
public sealed class Marker
{
    /// <summary>Stable id.</summary>
    public string Id { get; set; } = Ids.New();

    /// <summary>Seconds on the show timeline.</summary>
    public double Time { get; set; }

    /// <summary>Text shown on the ruler; may be empty.</summary>
    public string Label { get; set; } = "";
}
