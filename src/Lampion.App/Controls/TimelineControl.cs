using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lampion.App.Editor;
using Lampion.App.Views;
using Lampion.Core.Color;
using Lampion.Core.Model;

namespace Lampion.App.Controls;

/// <summary>
/// Premiere-style timeline: track headers on the left, clips on the right, keyframe lanes under expanded
/// light tracks, and a ruler with the beat grid. Everything is drawn here; there are no child controls.
/// </summary>
public sealed class TimelineControl : Control
{
    public const double HeaderWidth = 210;
    public const double RulerHeight = 30;
    private const double LaneHeight = 38;
    private const double EdgeGrab = 6;

    private static readonly Typeface Font = new("Inter");
    private static readonly IBrush Background = new SolidColorBrush(Color.FromRgb(0x1D, 0x1D, 0x1D));
    private static readonly IBrush HeaderBrush = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26));
    private static readonly IBrush RowAlt = new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21));
    private static readonly IBrush LaneBrush = new SolidColorBrush(Color.FromRgb(0x19, 0x19, 0x1B));
    private static readonly IBrush RulerBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
    private static readonly IBrush TextBrush = new SolidColorBrush(Color.FromRgb(0xD6, 0xD6, 0xD6));
    private static readonly IBrush DimText = new SolidColorBrush(Color.FromRgb(0x8C, 0x8C, 0x8C));
    private static readonly IBrush PlayheadBrush = new SolidColorBrush(Color.FromRgb(0x4D, 0xA3, 0xFF));
    private static readonly IBrush NameBar = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
    private static readonly IBrush FadeShade = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0));
    private static readonly IBrush AudioBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x6B, 0x4F));
    private static readonly IBrush VideoBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x48, 0x8C));
    private static readonly IBrush WaveBrush = new SolidColorBrush(Color.FromArgb(200, 170, 240, 200));
    private static readonly IBrush MarkerBrush = new SolidColorBrush(Color.FromRgb(0xF0, 0xB4, 0x2D));
    private static readonly IBrush DropBrush = new SolidColorBrush(Color.FromArgb(90, 77, 163, 255));
    private static readonly Pen GridBeat = new(new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)), 1);
    private static readonly Pen GridBar = new(new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)), 1);
    private static readonly Pen SelectedPen = new(Brushes.White, 2);
    private static readonly Pen ClipPen = new(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), 1);
    private static readonly Pen CurvePen = new(new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x3A)), 1.5);
    private static readonly Pen Divider = new(new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x12)), 1);

    private readonly EditorState _state;
    private Interaction? _interaction;
    private (Track Track, double Time)? _dropPreview;

    public TimelineControl(EditorState state)
    {
        _state = state;
        Focusable = true;
        ClipToBounds = true;
        state.Changed += InvalidateVisual;
        state.SelectionChanged += InvalidateVisual;
        state.PlayheadChanged += OnPlayhead;
    }

    public double PixelsPerSecond { get; set; } = 40;
    public double ScrollX { get; set; }
    public double ScrollY { get; set; }

    public event Action? ViewChanged;

    private double TimeToX(double t) => HeaderWidth + (t - ScrollX) * PixelsPerSecond;

    private double XToTime(double x) => (x - HeaderWidth) / PixelsPerSecond + ScrollX;

    public double VisibleSeconds => Math.Max(0, Bounds.Width - HeaderWidth) / PixelsPerSecond;

    public double ContentSeconds => _state.Project.ContentEnd + 30;

    public double ContentHeight => Rows().LastOrDefault() is { } last ? last.Y + last.Height + ScrollY - RulerHeight + 40 : 0;

    private void OnPlayhead()
    {
        // Page forward like Premiere when playback runs off the right edge.
        if (_state.IsPlaying && _interaction is null)
        {
            var x = TimeToX(_state.Playhead);
            if (x > Bounds.Width - 20 || x < HeaderWidth)
            {
                ScrollX = Math.Max(0, _state.Playhead - VisibleSeconds * 0.1);
                ViewChanged?.Invoke();
            }
        }
        InvalidateVisual();
    }

    public void ZoomAround(double factor, double anchorX)
    {
        var anchorTime = XToTime(anchorX);
        PixelsPerSecond = Math.Clamp(PixelsPerSecond * factor, 2, 1200);
        ScrollX = Math.Max(0, anchorTime - (anchorX - HeaderWidth) / PixelsPerSecond);
        ViewChanged?.Invoke();
        InvalidateVisual();
    }

    public void ZoomToFit()
    {
        PixelsPerSecond = Math.Clamp((Bounds.Width - HeaderWidth - 20) / Math.Max(1, _state.Project.ContentEnd), 2, 1200);
        ScrollX = 0;
        ViewChanged?.Invoke();
        InvalidateVisual();
    }

    // ---- Layout ----------------------------------------------------------------------------------------

    private sealed record Row(Track Track, string? LaneKey, string Label, double Y, double Height);

    private static double TrackHeight(Track t) =>
        t.Kind switch
        {
            TrackKind.Audio => 60,
            TrackKind.Video => 46,
            _ => 54,
        };

    private List<Row> Rows()
    {
        var rows = new List<Row>();
        var y = RulerHeight - ScrollY;
        foreach (var track in _state.Project.Tracks)
        {
            var h = TrackHeight(track);
            rows.Add(new Row(track, null, track.Name, y, h));
            y += h;
            if (track.Kind == TrackKind.Light && track.Expanded)
                foreach (var (key, label) in TimelineOps.LaneKeys(track))
                {
                    rows.Add(new Row(track, key, label, y, LaneHeight));
                    y += LaneHeight;
                }
        }
        return rows;
    }

    private Row? RowAt(double y) => Rows().Find(r => y >= r.Y && y < r.Y + r.Height);

    // ---- Rendering -------------------------------------------------------------------------------------

    public override void Render(DrawingContext ctx)
    {
        var bounds = Bounds;
        ctx.FillRectangle(Background, new Rect(bounds.Size));
        var rows = Rows();
        var clipArea = new Rect(HeaderWidth, RulerHeight, Math.Max(0, bounds.Width - HeaderWidth), Math.Max(0, bounds.Height - RulerHeight));

        using (ctx.PushClip(clipArea))
        {
            for (var i = 0; i < rows.Count; i++)
                if (rows[i].LaneKey is null && i % 2 == 1)
                    ctx.FillRectangle(RowAlt, new Rect(HeaderWidth, rows[i].Y, clipArea.Width, rows[i].Height));
                else if (rows[i].LaneKey is not null)
                    ctx.FillRectangle(LaneBrush, new Rect(HeaderWidth, rows[i].Y, clipArea.Width, rows[i].Height));
            DrawBeatGrid(ctx, clipArea);
            foreach (var row in rows)
            {
                if (row.Y > bounds.Height || row.Y + row.Height < RulerHeight)
                    continue;
                if (row.LaneKey is null)
                    DrawTrackClips(ctx, row);
                else
                    DrawLane(ctx, row);
                ctx.DrawLine(Divider, new Point(HeaderWidth, row.Y + row.Height), new Point(bounds.Width, row.Y + row.Height));
            }
            if (_dropPreview is { } drop && rows.Find(r => r.Track == drop.Track && r.LaneKey is null) is { } dropRow)
            {
                var x = TimeToX(drop.Time);
                ctx.FillRectangle(DropBrush, new Rect(x, dropRow.Y + 2, _state.SecondsPerBeat * 8 * PixelsPerSecond, dropRow.Height - 4));
            }
        }

        using (ctx.PushClip(new Rect(0, RulerHeight, HeaderWidth, Math.Max(0, bounds.Height - RulerHeight))))
            foreach (var row in rows)
                DrawHeader(ctx, row);

        DrawRuler(ctx, bounds);
        DrawPlayhead(ctx, bounds);
        ctx.DrawLine(new Pen(Brushes.Black, 1), new Point(HeaderWidth, 0), new Point(HeaderWidth, bounds.Height));
    }

    private void DrawBeatGrid(DrawingContext ctx, Rect area)
    {
        var beat = _state.SecondsPerBeat;
        if (beat * PixelsPerSecond < 6)
            beat *= 4;
        if (beat * PixelsPerSecond < 6)
            return;
        var first = Math.Floor((ScrollX - _state.Project.BeatOffset) / beat);
        var barBeats = beat == _state.SecondsPerBeat ? 4 : 1;
        for (var k = first; ; k++)
        {
            var t = _state.Project.BeatOffset + k * beat;
            var x = TimeToX(t);
            if (x > area.Right)
                break;
            if (x < area.Left)
                continue;
            ctx.DrawLine(((long)k % barBeats == 0) ? GridBar : GridBeat, new Point(x, area.Top), new Point(x, area.Bottom));
        }
    }

    private void DrawRuler(DrawingContext ctx, Rect bounds)
    {
        ctx.FillRectangle(RulerBrush, new Rect(0, 0, bounds.Width, RulerHeight));
        ctx.FillRectangle(HeaderBrush, new Rect(0, 0, HeaderWidth, RulerHeight));
        Text(ctx, Timecode(_state.Playhead), new Point(10, 6), PlayheadBrush, 14, FontWeight.SemiBold);

        var step = NiceStep(80 / PixelsPerSecond);
        var pen = new Pen(DimText, 1);
        using (ctx.PushClip(new Rect(HeaderWidth, 0, Math.Max(0, bounds.Width - HeaderWidth), RulerHeight)))
        {
            for (var t = Math.Floor(ScrollX / step) * step; TimeToX(t) < bounds.Width; t += step)
            {
                var x = TimeToX(t);
                ctx.DrawLine(pen, new Point(x, RulerHeight - 10), new Point(x, RulerHeight));
                Text(ctx, Label(t, step), new Point(x + 3, 3), DimText, 10);
                for (var s = 1; s < 5; s++)
                {
                    var sx = TimeToX(t + step * s / 5);
                    ctx.DrawLine(pen, new Point(sx, RulerHeight - 4), new Point(sx, RulerHeight));
                }
            }
            foreach (var marker in _state.Project.Markers)
            {
                var x = TimeToX(marker.Time);
                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    g.BeginFigure(new Point(x - 5, RulerHeight - 12), true);
                    g.LineTo(new Point(x + 5, RulerHeight - 12));
                    g.LineTo(new Point(x, RulerHeight - 4));
                    g.EndFigure(true);
                }
                ctx.DrawGeometry(MarkerBrush, null, geo);
                Text(ctx, marker.Label, new Point(x + 6, RulerHeight - 15), MarkerBrush, 9);
            }
            var endX = TimeToX(_state.Project.Duration);
            ctx.DrawLine(new Pen(Brushes.IndianRed, 1, DashStyle.Dash), new Point(endX, 0), new Point(endX, RulerHeight));
        }
    }

    private void DrawPlayhead(DrawingContext ctx, Rect bounds)
    {
        var x = TimeToX(_state.Playhead);
        if (x < HeaderWidth || x > bounds.Width)
            return;
        ctx.DrawLine(new Pen(PlayheadBrush, 1.5), new Point(x, RulerHeight - 2), new Point(x, bounds.Height));
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(x - 7, 12), true);
            g.LineTo(new Point(x + 7, 12));
            g.LineTo(new Point(x + 7, 20));
            g.LineTo(new Point(x, RulerHeight - 2));
            g.LineTo(new Point(x - 7, 20));
            g.EndFigure(true);
        }
        ctx.DrawGeometry(PlayheadBrush, null, geo);
    }

    private void DrawHeader(DrawingContext ctx, Row row)
    {
        var rect = new Rect(0, row.Y, HeaderWidth, row.Height);
        var selected = row.LaneKey is null && _state.SelectedTrackId == row.Track.Id;
        ctx.FillRectangle(selected ? new SolidColorBrush(Color.FromRgb(0x30, 0x34, 0x3C)) : HeaderBrush, rect);
        ctx.DrawLine(Divider, rect.BottomLeft, rect.BottomRight);
        if (row.LaneKey is not null)
        {
            Text(ctx, "◆ " + row.Label, new Point(30, row.Y + 11), new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x3A)), 11);
            return;
        }
        var track = row.Track;
        var kindColor = track.Kind switch
        {
            TrackKind.Audio => AudioBrush,
            TrackKind.Video => VideoBrush,
            _ => PlayheadBrush,
        };
        ctx.FillRectangle(kindColor, new Rect(0, row.Y, 4, row.Height));
        if (track.Kind == TrackKind.Light)
            Text(ctx, track.Expanded ? "▾" : "▸", new Point(9, row.Y + 7), TextBrush, 13);
        Text(ctx, track.Name, new Point(26, row.Y + 7), TextBrush, 12, FontWeight.SemiBold);
        var sub = track.Kind switch
        {
            TrackKind.Light => TargetSummary(track),
            TrackKind.Audio => "Audio",
            _ => "Video",
        };
        Text(ctx, sub, new Point(26, row.Y + 25), DimText, 10);
        DrawToggle(ctx, MuteRect(row), "M", track.Muted, Color.FromRgb(0xD9, 0x53, 0x4F));
        if (track.Kind == TrackKind.Light)
            DrawToggle(ctx, SoloRect(row), "S", track.Solo, Color.FromRgb(0xE8, 0xB9, 0x3A));
    }

    private string TargetSummary(Track track)
    {
        var parts = track.GroupIds.Select(id => _state.Project.Groups.Find(g => g.Id == id)?.Name).OfType<string>().ToList();
        if (track.FixtureIds.Count > 0)
            parts.Add($"{track.FixtureIds.Count} fixture{(track.FixtureIds.Count == 1 ? "" : "s")}");
        var text = parts.Count == 0 ? "no fixtures — pick some in Inspector" : string.Join(", ", parts);
        if (track.Blend != BlendMode.Normal)
            text += $" · {track.Blend}";
        return text;
    }

    private static Rect MuteRect(Row row) => new(HeaderWidth - 48, row.Y + 6, 18, 18);

    private static Rect SoloRect(Row row) => new(HeaderWidth - 26, row.Y + 6, 18, 18);

    private static Rect ExpandRect(Row row) => new(4, row.Y + 4, 20, 22);

    private static void DrawToggle(DrawingContext ctx, Rect rect, string label, bool on, Color color)
    {
        ctx.DrawRectangle(on ? new SolidColorBrush(color) : new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38)), null, rect, 3, 3);
        Text(ctx, label, new Point(rect.X + 5, rect.Y + 2), on ? Brushes.Black : DimText, 10, FontWeight.Bold);
    }

    private void DrawTrackClips(DrawingContext ctx, Row row)
    {
        var dim = row.Track.Muted ? 0.45 : 1;
        using var opacity = ctx.PushOpacity(dim);
        foreach (var clip in row.Track.Clips)
        {
            var x0 = TimeToX(clip.Start);
            var x1 = TimeToX(clip.End);
            if (x1 < HeaderWidth || x0 > Bounds.Width)
                continue;
            var rect = new Rect(x0, row.Y + 3, Math.Max(2, x1 - x0), row.Height - 6);
            var selected = _state.SelectedClipIds.Contains(clip.Id);
            switch (clip)
            {
                case LightClip light:
                    DrawLightClip(ctx, light, rect, row.Track);
                    break;
                case MediaClip media:
                    DrawMediaClip(ctx, media, rect, row.Track.Kind);
                    break;
            }
            ctx.DrawRectangle(null, selected ? SelectedPen : ClipPen, rect, 3, 3);
        }
    }

    private void DrawLightClip(DrawingContext ctx, LightClip clip, Rect rect, Track track)
    {
        // A strip of what the effect actually outputs over time, for the middle fixture of the track.
        var count = Math.Max(1, _state.Project.ResolveTargets(track).Count);
        var visibleLeft = Math.Max(rect.Left, HeaderWidth);
        var visibleRight = Math.Min(rect.Right, Bounds.Width);
        const double step = 4;
        using (ctx.PushClip(new RoundedRect(rect, 3)))
        {
            ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x24)), rect);
            for (var x = visibleLeft; x < visibleRight; x += step)
            {
                var local = XToTime(x + step / 2) - clip.Start;
                var sample = clip.Effect.Sample(new EffectContext(local + clip.EffectOffset, clip.EffectSpan ?? clip.Duration, count / 2, count));
                var color = (sample.Color * Math.Clamp(sample.Intensity, 0, 1) * clip.Envelope(local)).Clamped();
                var brush = new SolidColorBrush(Color.FromRgb(Rgb.ToByte(color.R), Rgb.ToByte(color.G), Rgb.ToByte(color.B)));
                ctx.FillRectangle(brush, new Rect(x, rect.Top + 16, step + 0.5, rect.Height - 16));
            }
            ctx.FillRectangle(NameBar, new Rect(rect.Left, rect.Top, rect.Width, 16));
            ctx.FillRectangle(Ui.Brush(clip.Effect.SwatchColor), new Rect(rect.Left, rect.Top, 3, 16));
            Text(ctx, clip.Name + (clip.Automation.Count > 0 ? $"  ◆{clip.Automation.Count}" : ""), new Point(Math.Max(rect.Left, HeaderWidth) + 6, rect.Top + 1), TextBrush, 10.5);
            DrawFade(ctx, rect, clip.FadeIn * PixelsPerSecond, true);
            DrawFade(ctx, rect, clip.FadeOut * PixelsPerSecond, false);
        }
        if (clip.TransitionIn is { } transition)
        {
            var half = transition.Duration / 2 * PixelsPerSecond;
            var box = new Rect(rect.Left - half, rect.Bottom - 15, Math.Max(6, half * 2), 13);
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, 0x3A, 0x60, 0x9A)), new Pen(Brushes.White, 0.8), box, 2, 2);
            Text(ctx, transition.Kind switch { TransitionKind.DipToBlack => "Dip", TransitionKind.Wipe => "Wipe", _ => "Xfade" }, new Point(box.Left + 3, box.Top), Brushes.White, 9);
        }
    }

    private static void DrawFade(DrawingContext ctx, Rect rect, double width, bool fadeIn)
    {
        var handleX = fadeIn ? rect.Left + width : rect.Right - width;
        if (width > 0.5)
        {
            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                if (fadeIn)
                {
                    g.BeginFigure(new Point(rect.Left, rect.Top + 16), true);
                    g.LineTo(new Point(handleX, rect.Top + 16));
                    g.LineTo(new Point(rect.Left, rect.Bottom));
                }
                else
                {
                    g.BeginFigure(new Point(rect.Right, rect.Top + 16), true);
                    g.LineTo(new Point(handleX, rect.Top + 16));
                    g.LineTo(new Point(rect.Right, rect.Bottom));
                }
                g.EndFigure(true);
            }
            ctx.DrawGeometry(FadeShade, new Pen(Brushes.White, 0.6), geo);
        }
        ctx.DrawRectangle(Brushes.White, null, new Rect(handleX - 3, rect.Top + 1, 6, 6), 1, 1);
    }

    private void DrawMediaClip(DrawingContext ctx, MediaClip clip, Rect rect, TrackKind kind)
    {
        using (ctx.PushClip(new RoundedRect(rect, 3)))
        {
            ctx.FillRectangle(kind == TrackKind.Audio ? AudioBrush : VideoBrush, rect);
            if (kind == TrackKind.Audio && _state.Waveforms.TryGetValue(clip.Path, out var peaks) && peaks is not null)
            {
                var mid = rect.Top + 16 + (rect.Height - 16) / 2;
                var half = (rect.Height - 18) / 2;
                var pen = new Pen(WaveBrush, 1);
                for (var x = Math.Max(rect.Left, HeaderWidth); x < Math.Min(rect.Right, Bounds.Width); x += 1.5)
                {
                    var sourceTime = XToTime(x) - clip.Start + clip.MediaOffset;
                    var a = (int)(sourceTime * peaks.PeaksPerSecond);
                    var b = (int)((sourceTime + 1.5 / PixelsPerSecond) * peaks.PeaksPerSecond);
                    var peak = 0f;
                    for (var i = Math.Max(0, a); i <= Math.Min(peaks.Peaks.Length - 1, b); i++)
                        peak = Math.Max(peak, peaks.Peaks[i]);
                    var h = peak * half * clip.Volume;
                    ctx.DrawLine(pen, new Point(x, mid - h), new Point(x, mid + h));
                }
            }
            ctx.FillRectangle(NameBar, new Rect(rect.Left, rect.Top, rect.Width, 16));
            Text(ctx, (kind == TrackKind.Audio ? "♪ " : "▶ ") + clip.Name, new Point(Math.Max(rect.Left, HeaderWidth) + 6, rect.Top + 1), TextBrush, 10.5);
        }
    }

    private void DrawLane(DrawingContext ctx, Row row)
    {
        foreach (var clip in row.Track.Clips.OfType<LightClip>())
        {
            var lane = clip.Automation.Find(l => TimelineOps.LaneKey(l) == row.LaneKey);
            if (lane is null)
                continue;
            var x0 = TimeToX(clip.Start);
            var x1 = TimeToX(clip.End);
            if (x1 < HeaderWidth || x0 > Bounds.Width)
                continue;
            var rect = new Rect(x0, row.Y + 3, x1 - x0, row.Height - 6);
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(lane.Enabled ? (byte)60 : (byte)25, 0xFF, 0xC8, 0x3A)), null, rect, 2, 2);
            var (min, max) = lane.DisplayRange;
            double Y(double v) => rect.Bottom - 3 - (v - min) / Math.Max(1e-6, max - min) * (rect.Height - 6);

            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                var first = true;
                for (var x = Math.Max(x0, HeaderWidth); x <= Math.Min(x1, Bounds.Width); x += 2)
                {
                    var p = new Point(x, Y(lane.Evaluate(XToTime(x) - clip.Start)));
                    if (first)
                        g.BeginFigure(p, false);
                    else
                        g.LineTo(p);
                    first = false;
                }
                if (!first)
                    g.EndFigure(false);
            }
            ctx.DrawGeometry(null, CurvePen, geo);
            foreach (var key in lane.Keys)
            {
                var center = new Point(TimeToX(clip.Start + key.Time), Y(key.Value));
                var isSelected = _state.SelectedKey is { } sk && sk.Key == key;
                DrawDiamond(ctx, center, isSelected ? 6 : 4.5, isSelected ? Brushes.White : new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x3A)));
            }
        }
    }

    private static void DrawDiamond(DrawingContext ctx, Point c, double r, IBrush brush)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(c.X, c.Y - r), true);
            g.LineTo(new Point(c.X + r, c.Y));
            g.LineTo(new Point(c.X, c.Y + r));
            g.LineTo(new Point(c.X - r, c.Y));
            g.EndFigure(true);
        }
        ctx.DrawGeometry(brush, new Pen(Brushes.Black, 1), geo);
    }

    // ---- Interaction -----------------------------------------------------------------------------------

    private abstract record Interaction;

    private sealed record Scrub : Interaction;

    private sealed record ClipDrag(
        Clip Clip,
        Track Track,
        ClipZone Zone,
        double StartTime,
        double OrigStart,
        double OrigDuration,
        double OrigValue,
        double OrigOffset
    ) : Interaction
    {
        public bool Moved { get; set; }
    }

    private sealed record KeyDrag(LightClip Clip, AutomationLane Lane, Keyframe Key, Rect LaneRect) : Interaction
    {
        public bool Moved { get; set; }
    }

    private enum ClipZone
    {
        Body,
        TrimStart,
        TrimEnd,
        FadeIn,
        FadeOut,
        Transition,
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var p = e.GetPosition(this);
        var point = e.GetCurrentPoint(this);
        var additive = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (p.Y < RulerHeight)
        {
            if (p.X > HeaderWidth)
            {
                _interaction = new Scrub();
                e.Pointer.Capture(this);
                _state.Seek(Math.Max(0, XToTime(p.X)));
            }
            return;
        }

        var row = RowAt(p.Y);
        if (row is null)
        {
            _state.Deselect();
            return;
        }

        if (p.X < HeaderWidth)
        {
            HeaderPressed(row, p, point.Properties.IsRightButtonPressed);
            return;
        }

        if (row.LaneKey is not null)
        {
            LanePressed(row, p, e);
            return;
        }

        var hit = HitClip(row, p);
        if (point.Properties.IsRightButtonPressed)
        {
            if (hit is { } h)
            {
                if (!_state.SelectedClipIds.Contains(h.Clip.Id))
                    _state.SelectClip(h.Clip.Id);
                ClipMenu(h.Clip, row.Track).Open(this);
            }
            else
                TrackMenu(row.Track, XToTime(p.X)).Open(this);
            return;
        }

        if (hit is { } found)
        {
            if (!(_state.SelectedClipIds.Contains(found.Clip.Id) && !additive))
                _state.SelectClip(found.Clip.Id, additive);
            if (row.Track.Locked)
                return;
            var origValue = found.Zone switch
            {
                ClipZone.FadeIn => ((LightClip)found.Clip).FadeIn,
                ClipZone.FadeOut => ((LightClip)found.Clip).FadeOut,
                ClipZone.Transition => ((LightClip)found.Clip).TransitionIn!.Duration,
                _ => 0,
            };
            _interaction = new ClipDrag(found.Clip, row.Track, found.Zone, XToTime(p.X), found.Clip.Start, found.Clip.Duration, origValue, (found.Clip as MediaClip)?.MediaOffset ?? 0);
            e.Pointer.Capture(this);
            return;
        }

        if (e.ClickCount == 2 && row.Track.Kind == TrackKind.Light)
        {
            TimelineOps.AddEffectClip(_state, row.Track, "solid", _state.Snap(XToTime(p.X), 10 / PixelsPerSecond));
            return;
        }
        _state.SelectTrack(row.Track.Id);
        _state.Seek(Math.Max(0, XToTime(p.X)));
    }

    private void HeaderPressed(Row row, Point p, bool right)
    {
        var track = row.Track;
        if (right)
        {
            _state.SelectTrack(track.Id);
            TrackMenu(track, _state.Playhead).Open(this);
            return;
        }
        if (row.LaneKey is not null)
            return;
        if (MuteRect(row).Contains(p))
        {
            track.Muted = !track.Muted;
            _state.Commit();
            return;
        }
        if (track.Kind == TrackKind.Light && SoloRect(row).Contains(p))
        {
            track.Solo = !track.Solo;
            _state.Commit();
            return;
        }
        if (track.Kind == TrackKind.Light && ExpandRect(row).Contains(p))
        {
            track.Expanded = !track.Expanded;
            _state.Touch();
            ViewChanged?.Invoke();
            return;
        }
        _state.SelectTrack(track.Id);
    }

    private (Clip Clip, ClipZone Zone)? HitClip(Row row, Point p)
    {
        var top = row.Y + 3;
        // Later clips are drawn on top, so test them first.
        for (var i = row.Track.Clips.Count - 1; i >= 0; i--)
        {
            var clip = row.Track.Clips[i];
            var x0 = TimeToX(clip.Start);
            var x1 = TimeToX(clip.End);
            if (clip is LightClip { TransitionIn: { } tr } lc)
            {
                var half = tr.Duration / 2 * PixelsPerSecond;
                if (p.X >= x0 - half && p.X <= x0 + half && p.Y >= row.Y + row.Height - 18)
                    return (lc, ClipZone.Transition);
            }
            if (p.X < x0 - 2 || p.X > x1 + 2)
                continue;
            if (clip is LightClip light && p.Y < top + 10)
            {
                if (Math.Abs(p.X - (x0 + light.FadeIn * PixelsPerSecond)) < 6)
                    return (clip, ClipZone.FadeIn);
                if (Math.Abs(p.X - (x1 - light.FadeOut * PixelsPerSecond)) < 6)
                    return (clip, ClipZone.FadeOut);
            }
            if (p.X - x0 < EdgeGrab)
                return (clip, ClipZone.TrimStart);
            if (x1 - p.X < EdgeGrab)
                return (clip, ClipZone.TrimEnd);
            return (clip, ClipZone.Body);
        }
        return null;
    }

    private void LanePressed(Row row, Point p, PointerPressedEventArgs e)
    {
        foreach (var clip in row.Track.Clips.OfType<LightClip>())
        {
            var lane = clip.Automation.Find(l => TimelineOps.LaneKey(l) == row.LaneKey);
            if (lane is null)
                continue;
            var x0 = TimeToX(clip.Start);
            var x1 = TimeToX(clip.End);
            if (p.X < x0 - 6 || p.X > x1 + 6)
                continue;
            var rect = new Rect(x0, row.Y + 3, x1 - x0, row.Height - 6);
            var (min, max) = lane.DisplayRange;
            double Y(double v) => rect.Bottom - 3 - (v - min) / Math.Max(1e-6, max - min) * (rect.Height - 6);
            var key = lane.Keys.FirstOrDefault(k => Math.Abs(TimeToX(clip.Start + k.Time) - p.X) < 7 && Math.Abs(Y(k.Value) - p.Y) < 7);

            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                if (key is not null)
                {
                    lane.Keys.Remove(key);
                    _state.SelectedKey = null;
                    _state.Commit();
                }
                return;
            }
            if (key is null && e.ClickCount == 2)
            {
                key = new Keyframe { Time = Math.Clamp(XToTime(p.X) - clip.Start, 0, clip.Duration), Value = lane.Evaluate(XToTime(p.X) - clip.Start) };
                lane.Keys.Add(key);
                lane.SortKeys();
                _state.Commit();
            }
            if (key is null)
            {
                _state.SelectClip(clip.Id);
                return;
            }
            _state.SelectClip(clip.Id);
            _state.SelectedKey = (lane.Id, key);
            _state.NotifySelectionChanged();
            _interaction = new KeyDrag(clip, lane, key, rect);
            e.Pointer.Capture(this);
            return;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        switch (_interaction)
        {
            case Scrub:
                _state.Seek(Math.Max(0, XToTime(p.X)));
                break;
            case ClipDrag drag:
                DragClip(drag, p, e.KeyModifiers.HasFlag(KeyModifiers.Alt));
                break;
            case KeyDrag key:
            {
                var (min, max) = key.Lane.DisplayRange;
                var time = Math.Clamp(XToTime(p.X) - key.Clip.Start, 0, key.Clip.Duration);
                var beats = !e.KeyModifiers.HasFlag(KeyModifiers.Alt);
                key.Key.Time = _state.Snap(key.Clip.Start + time, 8 / PixelsPerSecond, beats: beats) - key.Clip.Start;
                var value = min + (key.LaneRect.Bottom - 3 - p.Y) / Math.Max(1, key.LaneRect.Height - 6) * (max - min);
                key.Key.Value = key.Lane.Target == AutomationTarget.HueShift ? Math.Round(value, 3) : Math.Clamp(value, 0, 1);
                key.Lane.SortKeys();
                key.Moved = true;
                _state.Touch();
                break;
            }
            default:
                UpdateCursor(p);
                break;
        }
    }

    private void DragClip(ClipDrag drag, Point p, bool noSnap)
    {
        var clip = drag.Clip;
        var dt = XToTime(p.X) - drag.StartTime;
        var tolerance = noSnap ? 0 : 8 / PixelsPerSecond;
        var ignore = new HashSet<string> { clip.Id };
        switch (drag.Zone)
        {
            case ClipZone.Body:
            {
                var start = Math.Max(0, drag.OrigStart + dt);
                var snappedStart = _state.Snap(start, tolerance, ignore);
                var snappedEnd = _state.Snap(start + clip.Duration, tolerance, ignore) - clip.Duration;
                clip.Start = Math.Max(0, Math.Abs(snappedStart - start) <= Math.Abs(snappedEnd - start) ? snappedStart : snappedEnd);
                // Dragging up or down hands the clip to another track of the same kind.
                if (RowAt(p.Y) is { LaneKey: null } row && row.Track != drag.Track && row.Track.Kind == drag.Track.Kind && !row.Track.Locked)
                {
                    drag.Track.Clips.Remove(clip);
                    row.Track.Clips.Add(clip);
                    _interaction = drag with { Track = row.Track };
                    ((ClipDrag)_interaction).Moved = true;
                }
                break;
            }
            case ClipZone.TrimStart:
            {
                var end = drag.OrigStart + drag.OrigDuration;
                var minStart = clip is MediaClip ? drag.OrigStart - drag.OrigOffset : 0;
                var start = Math.Clamp(_state.Snap(drag.OrigStart + dt, tolerance, ignore), Math.Max(0, minStart), end - 0.05);
                clip.Start = start;
                clip.Duration = end - start;
                if (clip is MediaClip media)
                    media.MediaOffset = drag.OrigOffset + (start - drag.OrigStart);
                if (clip is LightClip light)
                    ClampFades(light);
                break;
            }
            case ClipZone.TrimEnd:
            {
                var end = _state.Snap(drag.OrigStart + drag.OrigDuration + dt, tolerance, ignore);
                var maxDuration = clip is MediaClip { SourceDuration: { } source } m ? source - m.MediaOffset : double.MaxValue;
                clip.Duration = Math.Clamp(end - clip.Start, 0.05, maxDuration);
                if (clip is LightClip light)
                    ClampFades(light);
                break;
            }
            case ClipZone.FadeIn when clip is LightClip light:
                light.FadeIn = Math.Clamp(drag.OrigValue + dt, 0, clip.Duration - light.FadeOut);
                break;
            case ClipZone.FadeOut when clip is LightClip light:
                light.FadeOut = Math.Clamp(drag.OrigValue - dt, 0, clip.Duration - light.FadeIn);
                break;
            case ClipZone.Transition when clip is LightClip { TransitionIn: { } transition }:
                // The box is centred on the cut, so its width follows the pointer's distance from it on either side.
                transition.Duration = Math.Clamp(Math.Abs(XToTime(p.X) - clip.Start) * 2, 0.05, Math.Min(60, clip.Duration * 2));
                break;
        }
        ((ClipDrag)_interaction!).Moved = true;
        _state.Touch();
    }

    private static void ClampFades(LightClip clip)
    {
        clip.FadeIn = Math.Min(clip.FadeIn, clip.Duration);
        clip.FadeOut = Math.Min(clip.FadeOut, clip.Duration - clip.FadeIn);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
        var interaction = _interaction;
        _interaction = null;
        switch (interaction)
        {
            case ClipDrag { Moved: true } drag:
                drag.Track.SortClips();
                _state.Commit();
                break;
            case KeyDrag { Moved: true }:
                _state.Commit();
                break;
        }
    }

    private void UpdateCursor(Point p)
    {
        var cursor = StandardCursorType.Arrow;
        if (p.X > HeaderWidth && RowAt(p.Y) is { LaneKey: null } row && HitClip(row, p) is { } hit)
            cursor = hit.Zone switch
            {
                ClipZone.TrimStart or ClipZone.TrimEnd or ClipZone.Transition => StandardCursorType.SizeWestEast,
                ClipZone.FadeIn or ClipZone.FadeOut => StandardCursorType.Hand,
                _ => StandardCursorType.Arrow,
            };
        Cursor = new Cursor(cursor);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var p = e.GetPosition(this);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            ZoomAround(e.Delta.Y > 0 ? 1.2 : 1 / 1.2, Math.Max(HeaderWidth, p.X));
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) || Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y))
        {
            var delta = Math.Abs(e.Delta.X) > 0 ? e.Delta.X : e.Delta.Y;
            ScrollX = Math.Max(0, ScrollX - delta * 60 / PixelsPerSecond);
        }
        else
            ScrollY = Math.Clamp(ScrollY - e.Delta.Y * 40, 0, Math.Max(0, ContentHeight - Bounds.Height + RulerHeight));
        ViewChanged?.Invoke();
        InvalidateVisual();
        e.Handled = true;
    }

    // ---- Effect drag-and-drop from the Effects panel ----------------------------------------------------

    /// <summary>Shows where a dragged effect would land; pass null to clear.</summary>
    public void PreviewDrop(Point? p)
    {
        _dropPreview = p is { } point && point.X > HeaderWidth && RowAt(point.Y) is { LaneKey: null, Track.Kind: TrackKind.Light } row
            ? (row.Track, _state.Snap(Math.Max(0, XToTime(point.X)), 8 / PixelsPerSecond))
            : null;
        InvalidateVisual();
    }

    public bool Drop(string effectKind, Point p)
    {
        PreviewDrop(null);
        if (p.X <= HeaderWidth || RowAt(p.Y) is not { LaneKey: null } row || row.Track.Kind != TrackKind.Light)
            return false;
        TimelineOps.AddEffectClip(_state, row.Track, effectKind, _state.Snap(Math.Max(0, XToTime(p.X)), 8 / PixelsPerSecond));
        return true;
    }

    // ---- Context menus ---------------------------------------------------------------------------------

    private ContextMenu ClipMenu(Clip clip, Track track)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("Split at playhead\tCtrl+K", () => TimelineOps.SplitAtPlayhead(_state)));
        menu.Items.Add(Item("Duplicate\tCtrl+D", () => TimelineOps.Duplicate(_state)));
        menu.Items.Add(Item("Delete\tDel", () => TimelineOps.DeleteSelection(_state)));
        if (clip is LightClip light)
        {
            menu.Items.Add(new Separator());
            var transition = new MenuItem { Header = "Transition in" };
            foreach (var kind in Enum.GetValues<TransitionKind>())
                transition.Items.Add(Item(kind.ToString(), () => TimelineOps.SetTransition(_state, light, kind)));
            transition.Items.Add(Item("None", () => TimelineOps.SetTransition(_state, light, null)));
            menu.Items.Add(transition);
            var lanes = new MenuItem { Header = "Add automation" };
            foreach (var target in new[] { AutomationTarget.HueShift, AutomationTarget.Saturation, AutomationTarget.Intensity, AutomationTarget.Opacity, AutomationTarget.White })
                lanes.Items.Add(Item(target == AutomationTarget.HueShift ? "Hue shift (rainbow)" : target.ToString(), () => AddLane(light, track, target)));
            var roles = new MenuItem { Header = "Channel role" };
            foreach (var role in new[] { ChannelRole.Pan, ChannelRole.Tilt, ChannelRole.Strobe, ChannelRole.Zoom, ChannelRole.Amber, ChannelRole.UV, ChannelRole.Gobo, ChannelRole.Effect, ChannelRole.PanTiltSpeed })
                roles.Items.Add(Item(role.ToString(), () => AddLane(light, track, AutomationTarget.Role, role)));
            lanes.Items.Add(roles);
            menu.Items.Add(lanes);
        }
        return menu;
    }

    private void AddLane(LightClip clip, Track track, AutomationTarget target, ChannelRole role = ChannelRole.Strobe)
    {
        TimelineOps.AddLane(_state, clip, target, role);
        track.Expanded = true;
        _state.Touch();
        ViewChanged?.Invoke();
    }

    private ContextMenu TrackMenu(Track track, double time)
    {
        var menu = new ContextMenu();
        if (track.Kind == TrackKind.Light)
        {
            var add = new MenuItem { Header = "Add effect here" };
            foreach (var (kind, name, _) in Core.Model.Effect.Catalog)
                add.Items.Add(Item(name, () => TimelineOps.AddEffectClip(_state, track, kind, _state.Snap(Math.Max(0, time), 8 / PixelsPerSecond))));
            menu.Items.Add(add);
        }
        menu.Items.Add(Item("New light track", () => TimelineOps.AddLightTrack(_state, index: _state.Project.Tracks.IndexOf(track))));
        menu.Items.Add(Item("Move up", () => MoveTrack(track, -1)));
        menu.Items.Add(Item("Move down", () => MoveTrack(track, 1)));
        menu.Items.Add(Item(track.Locked ? "Unlock" : "Lock", () =>
        {
            track.Locked = !track.Locked;
            _state.Commit();
        }));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Delete track", () =>
        {
            _state.Project.Tracks.Remove(track);
            _state.Commit();
            _state.Deselect();
        }));
        return menu;
    }

    private void MoveTrack(Track track, int direction)
    {
        var tracks = _state.Project.Tracks;
        var i = tracks.IndexOf(track);
        var j = Math.Clamp(i + direction, 0, tracks.Count - 1);
        if (i == j)
            return;
        tracks.RemoveAt(i);
        tracks.Insert(j, track);
        _state.Commit();
    }

    private static MenuItem Item(string header, Action action)
    {
        var parts = header.Split('\t');
        var item = new MenuItem { Header = parts[0] };
        if (parts.Length > 1)
            item.InputGesture = KeyGesture.Parse(parts[1]);
        item.Click += (_, _) => action();
        return item;
    }

    // ---- Text helpers ----------------------------------------------------------------------------------

    private static void Text(DrawingContext ctx, string text, Point at, IBrush brush, double size, FontWeight weight = FontWeight.Normal)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(Font.FontFamily, FontStyle.Normal, weight), size, brush);
        ctx.DrawText(formatted, at);
    }

    public static string Timecode(double seconds, int fps = 30)
    {
        var totalFrames = (long)Math.Round(seconds * fps);
        var frames = totalFrames % fps;
        var s = totalFrames / fps;
        return $"{s / 3600:00}:{s / 60 % 60:00}:{s % 60:00}:{frames:00}";
    }

    private static double NiceStep(double minimum)
    {
        foreach (var step in new[] { 0.1, 0.25, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600 })
            if (step >= minimum)
                return step;
        return 1200;
    }

    private static string Label(double t, double step)
    {
        var minutes = (int)(t / 60);
        var seconds = t - minutes * 60;
        return step < 1 ? $"{minutes}:{seconds:00.00}" : $"{minutes}:{seconds:00}";
    }
}
