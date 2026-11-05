using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lampion.App.Controls.Stage3D;

namespace Lampion.App.Controls;

/// <summary>
/// Blender's navigation gizmo: axis balls that turn with the camera. Click a ball to look down that axis
/// (orthographic), drag anywhere on the disc to orbit, click the label underneath to swap perspective and ortho.
/// </summary>
internal sealed class ViewGizmo : Control, Avalonia.Rendering.ICustomHitTest
{
    private const double Radius = 33;
    private const double Ball = 9;
    private const double Size = 92;
    private const double DragThreshold = 3;

    private static readonly IBrush Backdrop = new SolidColorBrush(Color.FromArgb(46, 255, 255, 255));
    private static readonly IBrush Letter = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10));
    private static readonly IBrush HoverLabel = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x7A));
    private static readonly IPen HoverOutline = new Pen(Brushes.White, 1.5);

    private readonly StageView _view;
    private readonly Action _invalidate;
    private readonly BallPlacement[] _layout = new BallPlacement[GizmoBall.All.Length];
    private int? _hover;
    private bool _hoverLabel;
    private bool _hoverDisc;
    private Point? _pressed;
    private Point _last;
    private bool _dragging;

    /// <summary>A gizmo driving <paramref name="view"/>'s camera, redrawn with every frame it renders.</summary>
    public ViewGizmo(StageView view)
    {
        _view = view;
        _invalidate = InvalidateVisual;
        Width = Size;
        Height = Size + 22;
        Cursor = new Cursor(StandardCursorType.Hand);
        view.Rendered += OnRendered;
    }

    private void OnRendered() => Avalonia.Threading.Dispatcher.UIThread.Post(_invalidate);

    private static Point Center => new(Size / 2, Size / 2);

    // Only the disc and the label take clicks; the corners stay clickable for the stage underneath.
    /// <summary>Takes pointer events on the disc and the label only.</summary>
    public bool HitTest(Point point) => StageGeometry.Distance(point, Center) < Radius + Ball + 2 || IsOnLabel(point);

    private static bool IsOnLabel(Point p) => p.Y > Size && p.Y < Size + 20 && Math.Abs(p.X - Center.X) < 30;

    /// <summary>
    /// Fills <see cref="_layout"/> with where each ball lands and how far toward the viewer it points, ordered
    /// by depth (far first, or near first with <paramref name="nearFirst"/>); equal depths keep ball order.
    /// </summary>
    private ReadOnlySpan<BallPlacement> Layout(bool nearFirst)
    {
        var (right, up, forward) = _view.Camera.Basis();
        var balls = GizmoBall.All;
        for (var i = 0; i < balls.Length; i++)
        {
            var axis = balls[i].Axis;
            var screen = new Point(Center.X + Vector3.Dot(axis, right) * Radius, Center.Y - Vector3.Dot(axis, up) * Radius);
            var placement = new BallPlacement(i, screen, -Vector3.Dot(axis, forward));
            var at = i;
            while (at > 0 && (nearFirst ? _layout[at - 1].Depth < placement.Depth : _layout[at - 1].Depth > placement.Depth))
            {
                _layout[at] = _layout[at - 1];
                at--;
            }
            _layout[at] = placement;
        }
        return _layout;
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext ctx)
    {
        if (_hoverDisc || _dragging)
            ctx.DrawEllipse(Backdrop, null, Center, Radius + Ball + 2, Radius + Ball + 2);

        // Painter's order: balls pointing away from the viewer first, so near ones overlap them.
        // Each positive axis line is drawn together with its ball so a nearer ball still covers it.
        foreach (var placement in Layout(nearFirst: false))
            DrawBall(ctx, in placement);

        var label = TextCache.Get(_view.Camera.Label, 11, _hoverLabel ? HoverLabel : Brushes.Gainsboro);
        ctx.DrawText(label, new Point(Center.X - label.Width / 2, Size + 2));
    }

    private void DrawBall(DrawingContext ctx, in BallPlacement placement)
    {
        var (index, position, depth) = placement;
        var ball = GizmoBall.All[index];
        var hovered = _hover == index;
        // Balls on the far side fade a little, which is most of what sells the depth.
        var fade = (byte)(hovered ? 255 : 150 + 105 * (depth + 1) / 2);

        if (GizmoBall.IsPositive(index))
        {
            ctx.DrawLine(GizmoPaint.Line(ball.Color, fade), Center, position);
            ctx.DrawEllipse(GizmoPaint.Fill(ball.Color, fade), hovered ? HoverOutline : null, position, Ball, Ball);
            DrawLetter(ctx, ball.Name, position, Letter);
        }
        else
        {
            var tint = GizmoPaint.Fill(ball.Color, (byte)(fade * 0.4));
            ctx.DrawEllipse(tint, hovered ? HoverOutline : GizmoPaint.Ring(ball.Color, fade), position, Ball - 0.75, Ball - 0.75);
            if (hovered)
                DrawLetter(ctx, ball.Name, position, Brushes.White);
        }
    }

    private static void DrawLetter(DrawingContext ctx, string text, Point at, IBrush brush)
    {
        var formatted = TextCache.Get(text, 10, brush, FontWeight.Bold);
        ctx.DrawText(formatted, new Point(at.X - formatted.Width / 2, at.Y - formatted.Height / 2));
    }

    // ---- Interaction -----------------------------------------------------------------------------------

    private int? BallAt(Point p)
    {
        // Nearest-to-viewer balls win where they overlap, as they are drawn on top.
        foreach (var placement in Layout(nearFirst: true))
            if (StageGeometry.Distance(p, placement.Position) < Ball + 1)
                return placement.Index;
        return null;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);

        if (_pressed is { } start)
        {
            if (!_dragging && StageGeometry.Distance(point, start) > DragThreshold)
                _dragging = true;
            if (_dragging)
            {
                _view.Camera.Orbit((float)(point.X - _last.X), (float)(point.Y - _last.Y));
                _last = point;
            }
        }

        _hoverLabel = IsOnLabel(point);
        _hoverDisc = !_hoverLabel && StageGeometry.Distance(point, Center) < Radius + Ball + 2;
        _hover = _dragging || _hoverLabel ? null : BallAt(point);
        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        (_hover, _hoverLabel, _hoverDisc) = (null, false, false);
        InvalidateVisual();
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressed is null)
            return;
        e.Handled = true;
        if (!_dragging && BallAt(e.GetPosition(this)) is { } index)
            _view.ApplyPreset(GizmoBall.All[index].View);
        _pressed = null;
        _dragging = false;
        e.Pointer.Capture(null);
        InvalidateVisual();
    }
}
