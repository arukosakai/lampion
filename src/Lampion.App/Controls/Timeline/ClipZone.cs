namespace Lampion.App.Controls.Timeline;

/// <summary>Which part of a clip the pointer is over, and so what dragging it does.</summary>
internal enum ClipZone
{
    /// <summary>The middle: move the clip (and hand it to another track).</summary>
    Body,

    /// <summary>The left edge: trim the start.</summary>
    TrimStart,

    /// <summary>The right edge: trim the end.</summary>
    TrimEnd,

    /// <summary>The fade-in handle at the top (light clips).</summary>
    FadeIn,

    /// <summary>The fade-out handle at the top (light clips).</summary>
    FadeOut,

    /// <summary>The transition box on the left edge (light clips with a transition).</summary>
    Transition,
}
