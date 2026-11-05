using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lampion.App.Rendering;

namespace Lampion.App.Controls;

/// <summary>
/// Blender's navigation gizmo: axis balls that turn with the camera. Click a ball to look down that axis
/// (orthographic), drag anywhere on the disc to orbit, click the label underneath to swap perspective and ortho.
/// </summary>
public sealed class ViewGizmo : Control, Avalonia.Rendering.ICustomHitTest
{
    private const double Radius = 33;
    private const double Ball = 9;
    private const double Size = 92;
    private const double DragThreshold = 3;

    private static readonly Typeface Font = new("Inter", FontStyle.Normal, FontWeight.Bold);
    private static readonly Color[] AxisColors = [Color.FromRgb(0xFF, 0x33, 0x52), Color.FromRgb(0x8B, 0xDC, 0x00), Color.FromRgb(0x28, 0x90, 0xFF)];
    private static readonly IBrush Backdrop = new SolidColorBrush(Color.FromArgb(46, 255, 255, 255));
    private static readonly IBrush Letter = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10));
    private static readonly IBrush HoverLabel = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x7A));

    // Camera side each ball stands for: the +X ball is the Right view, and so on.
    private static readonly (Vector3 Axis, ViewPreset View, int Color, string Name)[] Balls =
    [
        (Vector3.UnitX, ViewPreset.Right, 0, "X"),
        (Vector3.UnitY, ViewPreset.Top, 1, "Y"),
        (Vector3.UnitZ, ViewPreset.Front, 2, "Z"),
        (-Vector3.UnitX, ViewPreset.Left, 0, "-X"),
        (-Vector3.UnitY, ViewPreset.Bottom, 1, "-Y"),
        (-Vector3.UnitZ, ViewPreset.Back, 2, "-Z"),
    ];

    private readonly StageView _view;
    private int? _hover;
    private bool _hoverLabel;
    private bool _hoverDisc;
    private Point? _pressed;
    private Point _last;
    private bool _dragging;

    public ViewGizmo(StageView view)
    {
        _view = view;
        Width = Size;
        Height = Size + 22;
        Cursor = new Cursor(StandardCursorType.Hand);
        view.Rendered += () => Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual);
    }

    private Point Center => new(Size / 2, Size / 2);

    // Only the disc and the label take clicks; the corners stay clickable for the stage underneath.
    public bool HitTest(Point point) => Distance(point, Center) < Radius + Ball + 2 || IsOnLabel(point);

    private bool IsOnLabel(Point p) => p.Y > Size && p.Y < Size + 20 && Math.Abs(p.X - Center.X) < 30;

    private static bool IsPositive(int index) => index < 3;

    /// <summary>Where each ball lands on screen, and how far toward the viewer it points (for draw order).</summary>
    private List<(int Index, Point Position, float Depth)> Layout()
    {
        var (right, up, forward) = _view.Camera.Basis();
        var result = new List<(int, Point, float)>();
        for (var i = 0; i < Balls.Length; i++)
        {
            var axis = Balls[i].Axis;
            var screen = new Point(Center.X + Vector3.Dot(axis, right) * Radius, Center.Y - Vector3.Dot(axis, up) * Radius);
            result.Add((i, screen, -Vector3.Dot(axis, forward)));
        }
        return result;
    }

    public override void Render(DrawingContext ctx)
    {
        if (_hoverDisc || _dragging)
            ctx.DrawEllipse(Backdrop, null, Center, Radius + Ball + 2, Radius + Ball + 2);

        // Painter's order: balls pointing away from the viewer first, so near ones overlap them.
        // Each positive axis line is drawn together with its ball so a nearer ball still covers it.
        foreach (var (index, position, depth) in Layout().OrderBy(c => c.Depth))
            DrawBall(ctx, index, position, depth);

        var label = new FormattedText(_view.Camera.Label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Inter"), 11, _hoverLabel ? HoverLabel : Brushes.Gainsboro);
        ctx.DrawText(label, new Point(Center.X - label.Width / 2, Size + 2));
    }

    private void DrawBall(DrawingContext ctx, int index, Point position, float depth)
    {
        var (_, _, colorIndex, name) = Balls[index];
        var hovered = _hover == index;
        // Balls on the far side fade a little, which is most of what sells the depth.
        var fade = (byte)(hovered ? 255 : 150 + 105 * (depth + 1) / 2);
        var color = AxisColors[colorIndex];
        var solid = new SolidColorBrush(Color.FromArgb(fade, color.R, color.G, color.B));

        if (IsPositive(index))
        {
            ctx.DrawLine(new Pen(solid, 2), Center, position);
            ctx.DrawEllipse(solid, hovered ? new Pen(Brushes.White, 1.5) : null, position, Ball, Ball);
            DrawLetter(ctx, name, position, Letter);
        }
        else
        {
            var tint = new SolidColorBrush(Color.FromArgb((byte)(fade * 0.4), color.R, color.G, color.B));
            ctx.DrawEllipse(tint, new Pen(hovered ? Brushes.White : solid, 1.5), position, Ball - 0.75, Ball - 0.75);
            if (hovered)
                DrawLetter(ctx, name, position, Brushes.White);
        }
    }

    private static void DrawLetter(DrawingContext ctx, string text, Point at, IBrush brush)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, 10, brush);
        ctx.DrawText(formatted, new Point(at.X - formatted.Width / 2, at.Y - formatted.Height / 2));
    }

    // ---- Interaction -----------------------------------------------------------------------------------

    private int? BallAt(Point p)
    {
        // Nearest-to-viewer balls win where they overlap, as they are drawn on top.
        foreach (var (index, position, _) in Layout().OrderByDescending(c => c.Depth))
            if (Distance(p, position) < Ball + 1)
                return index;
        return null;
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);

        if (_pressed is { } start)
        {
            if (!_dragging && Distance(point, start) > DragThreshold)
                _dragging = true;
            if (_dragging)
            {
                _view.Camera.Orbit((float)(point.X - _last.X), (float)(point.Y - _last.Y));
                _last = point;
            }
        }

        _hoverLabel = IsOnLabel(point);
        _hoverDisc = !_hoverLabel && Distance(point, Center) < Radius + Ball + 2;
        _hover = _dragging || _hoverLabel ? null : BallAt(point);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        (_hover, _hoverLabel, _hoverDisc) = (null, false, false);
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
        var point = e.GetPosition(this);
        if (IsOnLabel(point))
        {
            _view.Camera.ToggleProjection();
            InvalidateVisual();
            return;
        }
        _pressed = _last = point;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressed is null)
            return;
        e.Handled = true;
        if (!_dragging && BallAt(e.GetPosition(this)) is { } index)
            _view.ApplyPreset(Balls[index].View);
        _pressed = null;
        _dragging = false;
        e.Pointer.Capture(null);
        InvalidateVisual();
    }
}
