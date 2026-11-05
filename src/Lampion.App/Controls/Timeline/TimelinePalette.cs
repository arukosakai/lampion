using Avalonia.Media;

namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Every brush and pen the timeline draws with, created once. Text brushes are also part of the
/// <see cref="TextCache"/> key, so these shared instances are what keeps label lookups hitting the cache.
/// </summary>
internal static class TimelinePalette
{
    /// <summary>Behind everything.</summary>
    public static readonly IBrush Background = Solid(0x1D, 0x1D, 0x1D);

    /// <summary>Track headers and the ruler's left corner.</summary>
    public static readonly IBrush Header = Solid(0x26, 0x26, 0x26);

    /// <summary>The selected track's header.</summary>
    public static readonly IBrush HeaderSelected = Solid(0x30, 0x34, 0x3C);

    /// <summary>Every other track row, for zebra striping.</summary>
    public static readonly IBrush RowAlt = Solid(0x21, 0x21, 0x21);

    /// <summary>Keyframe rows.</summary>
    public static readonly IBrush LaneRow = Solid(0x19, 0x19, 0x1B);

    /// <summary>The ruler strip.</summary>
    public static readonly IBrush Ruler = Solid(0x2A, 0x2A, 0x2A);

    /// <summary>Main text.</summary>
    public static readonly IBrush Text = Solid(0xD6, 0xD6, 0xD6);

    /// <summary>Secondary text and ruler ticks.</summary>
    public static readonly IBrush DimText = Solid(0x8C, 0x8C, 0x8C);

    /// <summary>The playhead, light-track accents and the drop preview outline.</summary>
    public static readonly IBrush Playhead = Solid(0x4D, 0xA3, 0xFF);

    /// <summary>Keyframes and the keyframe-row captions.</summary>
    public static readonly IBrush Key = Solid(0xFF, 0xC8, 0x3A);

    /// <summary>The bar behind a clip's name.</summary>
    public static readonly IBrush NameBar = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));

    /// <summary>The shaded fade ramps on light clips.</summary>
    public static readonly IBrush FadeShade = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0));

    /// <summary>Behind a light clip's colour strip.</summary>
    public static readonly IBrush LightClipBody = Solid(0x20, 0x20, 0x24);

    /// <summary>Audio clips and the audio track accent.</summary>
    public static readonly IBrush Audio = Solid(0x2E, 0x6B, 0x4F);

    /// <summary>Video clips and the video track accent.</summary>
    public static readonly IBrush Video = Solid(0x5A, 0x48, 0x8C);

    /// <summary>The audio waveform.</summary>
    public static readonly IBrush Wave = new SolidColorBrush(Color.FromArgb(200, 170, 240, 200));

    /// <summary>Markers on the ruler.</summary>
    public static readonly IBrush Marker = Solid(0xF0, 0xB4, 0x2D);

    /// <summary>Where a drag from another panel would land.</summary>
    public static readonly IBrush Drop = new SolidColorBrush(Color.FromArgb(90, 77, 163, 255));

    /// <summary>The transition box on a light clip's left edge.</summary>
    public static readonly IBrush Transition = new SolidColorBrush(Color.FromArgb(220, 0x3A, 0x60, 0x9A));

    /// <summary>A clip's span on a keyframe row while its lane is enabled.</summary>
    public static readonly IBrush LaneSpanEnabled = new SolidColorBrush(Color.FromArgb(60, 0xFF, 0xC8, 0x3A));

    /// <summary>A clip's span on a keyframe row while its lane is disabled.</summary>
    public static readonly IBrush LaneSpanDisabled = new SolidColorBrush(Color.FromArgb(25, 0xFF, 0xC8, 0x3A));

    /// <summary>A header toggle that is off.</summary>
    public static readonly IBrush ToggleOff = Solid(0x38, 0x38, 0x38);

    /// <summary>The mute toggle when on.</summary>
    public static readonly IBrush MuteOn = Solid(0xD9, 0x53, 0x4F);

    /// <summary>The solo toggle when on.</summary>
    public static readonly IBrush SoloOn = Solid(0xE8, 0xB9, 0x3A);

    /// <summary>Beat lines.</summary>
    public static readonly Pen GridBeat = new(new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)), 1);

    /// <summary>Bar lines (every fourth beat).</summary>
    public static readonly Pen GridBar = new(new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)), 1);

    /// <summary>Outline of a selected clip.</summary>
    public static readonly Pen Selected = new(Brushes.White, 2);

    /// <summary>Outline of an unselected clip.</summary>
    public static readonly Pen ClipOutline = new(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), 1);

    /// <summary>Automation curves.</summary>
    public static readonly Pen Curve = new(Solid(0xFF, 0xC8, 0x3A), 1.5);

    /// <summary>Lines between rows.</summary>
    public static readonly Pen Divider = new(Solid(0x12, 0x12, 0x12), 1);

    /// <summary>The line between the headers and the clips.</summary>
    public static readonly Pen HeaderEdge = new(Brushes.Black, 1);

    /// <summary>Ruler ticks.</summary>
    public static readonly Pen Tick = new(DimText, 1);

    /// <summary>The dashed end-of-show line on the ruler.</summary>
    public static readonly Pen ShowEnd = new(Brushes.IndianRed, 1, DashStyle.Dash);

    /// <summary>The playhead line.</summary>
    public static readonly Pen PlayheadLine = new(Playhead, 1.5);

    /// <summary>Outline of the drop preview.</summary>
    public static readonly Pen DropOutline = new(Playhead, 1.5);

    /// <summary>Outline of the fade ramps.</summary>
    public static readonly Pen FadeOutline = new(Brushes.White, 0.6);

    /// <summary>Outline of the transition box.</summary>
    public static readonly Pen TransitionOutline = new(Brushes.White, 0.8);

    /// <summary>Outline of keyframe diamonds.</summary>
    public static readonly Pen KeyOutline = new(Brushes.Black, 1);

    /// <summary>The waveform's vertical strokes.</summary>
    public static readonly Pen WaveLine = new(Wave, 1);

    private static SolidColorBrush Solid(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
