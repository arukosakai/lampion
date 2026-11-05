using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Lampion.App.Rendering;

namespace Lampion.App.Controls;

/// <summary>
/// Unity's scene gizmo: axis cones that turn with the camera. Click a cone to look down that axis
/// (orthographic), the centre cube or the label underneath to swap perspective and ortho.
/// </summary>
public sealed class ViewGizmo : Control, Avalonia.Rendering.ICustomHitTest
{
    private const double Radius = 34;
    private const double Size = 92;

    private static readonly Typeface Font = new("Inter", FontStyle.Normal, FontWeight.Bold);
    private static readonly Color[] AxisColors = [Color.FromRgb(0xE5, 0x48, 0x48), Color.FromRgb(0x7C, 0xD1, 0x3C), Color.FromRgb(0x40, 0x8C, 0xFF)];
    private static readonly IBrush Negative = new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));
    private static readonly IBrush Hover = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x7A));

    // Camera side each cone stands for: the +X cone is the Right view, and so on.
    private static readonly (Vector3 Axis, ViewPreset View, int Color, string Name)[] Cones =
    [
        (Vector3.UnitX, ViewPreset.Right, 0, "x"),
        (Vector3.UnitY, ViewPreset.Top, 1, "y"),
        (Vector3.UnitZ, ViewPreset.Front, 2, "z"),
        (-Vector3.UnitX, ViewPreset.Left, -1, ""),
        (-Vector3.UnitY, ViewPreset.Bottom, -1, ""),
        (-Vector3.UnitZ, ViewPreset.Back, -1, ""),
    ];

    private readonly StageView _view;
    private int? _hover;
    private bool _hoverCenter;

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
    public bool HitTest(Point point) => Distance(point, Center) < Radius + 14 || (point.Y > Size && point.Y < Size + 20 && Math.Abs(point.X - Center.X) < 30);

    /// <summary>Where each cone's tip lands on screen, and how far toward the viewer it points (for draw order).</summary>
    private List<(int Index, Point Tip, float Depth)> Layout()
    {
        var (right, up, forward) = _view.Camera.Basis();
        var result = new List<(int, Point, float)>();
        for (var i = 0; i < Cones.Length; i++)
        {
            var axis = Cones[i].Axis;
            var screen = new Point(Center.X + Vector3.Dot(axis, right) * Radius, Center.Y - Vector3.Dot(axis, up) * Radius);
            result.Add((i, screen, -Vector3.Dot(axis, forward)));
        }
        return result;
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), null, Center, Radius + 12, Radius + 12);

        // Painter's order: cones pointing away from the viewer first, so near ones overlap them.
        var cones = Layout().OrderBy(c => c.Depth).ToList();
        var centerDrawn = false;
        foreach (var (index, tip, depth) in cones)
        {
            if (!centerDrawn && depth > 0)
            {
                DrawCenter(ctx);
                centerDrawn = true;
            }
            DrawCone(ctx, index, tip);
        }
        if (!centerDrawn)
            DrawCenter(ctx);

        var label = new FormattedText(_view.Camera.Label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Inter"), 11, _hoverCenter ? Hover : Brushes.Gainsboro);
        ctx.DrawText(label, new Point(Center.X - label.Width / 2, Size + 2));
    }

    private void DrawCone(DrawingContext ctx, int index, Point tip)
    {
        var (_, _, colorIndex, name) = Cones[index];
        var hovered = _hover == index;
        var direction = tip - Center;
        var length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        IBrush brush = hovered ? Hover : colorIndex < 0 ? Negative : new SolidColorBrush(AxisColors[colorIndex]);

        if (length < 3)
        {
            // Pointing straight at (or away from) the viewer: a cone seen end-on is a disc.
            ctx.DrawEllipse(brush, null, tip, 8, 8);
        }
        else
        {
            var unit = new Point(direction.X / length, direction.Y / length);
            var normal = new Point(-unit.Y, unit.X);
            // The cone's base sits a little out from the centre; its width shrinks as it turns end-on.
            var baseCenter = Center + unit * Math.Max(4, length * 0.35);
            var halfWidth = 8.5;
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(tip, true);
                g.LineTo(baseCenter + normal * halfWidth);
                g.LineTo(baseCenter - normal * halfWidth);
                g.EndFigure(true);
            }
            ctx.DrawGeometry(brush, new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), 0.8), geometry);
        }

        if (name.Length > 0)
        {
            var text = new FormattedText(name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, 11, hovered ? Hover : new SolidColorBrush(AxisColors[colorIndex]));
            var away = length < 3 ? new Point(0, -14) : new Point(direction.X / length * 12, direction.Y / length * 12);
            ctx.DrawText(text, new Point(tip.X + away.X - text.Width / 2, tip.Y + away.Y - text.Height / 2));
        }
    }

    private void DrawCenter(DrawingContext ctx)
    {
        var fill = _hoverCenter ? Hover : new SolidColorBrush(_view.Camera.Orthographic ? Color.FromRgb(0xD8, 0xD8, 0xD8) : Color.FromRgb(0x9A, 0x9A, 0x9A));
        ctx.DrawRectangle(fill, new Pen(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), 1), new Rect(Center.X - 7, Center.Y - 7, 14, 14), 2, 2);
    }

    // ---- Interaction -----------------------------------------------------------------------------------

    private (int? Cone, bool Center) HitTestAt(Point p)
    {
        if (p.Y > Size)
            return (null, true);
        // Nearest-to-viewer cones win where they overlap, as they are drawn on top.
        foreach (var (index, tip, _) in Layout().OrderByDescending(c => c.Depth))
            if (Distance(p, tip) < 11)
                return (index, false);
        return (null, Distance(p, Center) < 10);
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        (_hover, _hoverCenter) = HitTestAt(e.GetPosition(this));
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        (_hover, _hoverCenter) = (null, false);
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
        var (cone, center) = HitTestAt(e.GetPosition(this));
        if (cone is { } index)
            _view.ApplyPreset(Cones[index].View);
        else if (center)
            _view.Camera.ToggleProjection();
        InvalidateVisual();
    }
}
