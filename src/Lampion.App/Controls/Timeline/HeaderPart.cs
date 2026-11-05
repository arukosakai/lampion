namespace Lampion.App.Controls.Timeline;

/// <summary>The clickable part of a track header under the pointer.</summary>
internal enum HeaderPart
{
    /// <summary>Anywhere else on the header: selects the track.</summary>
    None,

    /// <summary>The M toggle.</summary>
    Mute,

    /// <summary>The S toggle (light tracks).</summary>
    Solo,

    /// <summary>The twirl-down (light tracks).</summary>
    Expand,
}
