namespace Lampion.Core.Model.Timeline;

/// <summary>An audio or video file on a media track.</summary>
public sealed class MediaClip : Clip
{
    /// <summary>Absolute path, or relative to the project file.</summary>
    public string Path { get; set; } = "";

    /// <summary>Seconds into the media file the clip starts at (a left trim).</summary>
    public double MediaOffset { get; set; }

    /// <summary>Length of the whole source file in seconds, when known — trims cannot run past it.</summary>
    public double? SourceDuration { get; set; }

    /// <summary>Playback gain, 0..1.</summary>
    public double Volume { get; set; } = 1;

    /// <inheritdoc />
    public override Clip Clone() =>
        new MediaClip
        {
            Name = Name,
            Start = Start,
            Duration = Duration,
            Path = Path,
            MediaOffset = MediaOffset,
            SourceDuration = SourceDuration,
            Volume = Volume,
        };
}
