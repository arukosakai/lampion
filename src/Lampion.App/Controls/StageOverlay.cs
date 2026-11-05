using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lampion.App.Editor;
using Lampion.App.Rendering;
using Lampion.Core.Model;

namespace Lampion.App.Controls;

/// <summary>
/// 2D annotations over the 3D stage: names, the move gizmo, and measurements in metres (position,
/// throw distance and spot size) — the numbers a lighting designer needs when hanging a rig.
/// </summary>
public sealed class StageOverlay : Control
{
    private static readonly Typeface Font = new("Inter");
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.FromArgb(200, 230, 230, 235));
    private static readonly IBrush PanelBrush = new SolidColorBrush(Color.FromArgb(190, 18, 18, 22));
    private static readonly IBrush Accent = new SolidColorBrush(Color.FromRgb(45, 140, 235));
    private static readonly IBrush[] AxisBrushes =
    [
        new SolidColorBrush(Color.FromRgb(235, 72, 72)),
        new SolidColorBrush(Color.FromRgb(90, 210, 90)),
        new SolidColorBrush(Color.FromRgb(70, 140, 255)),
    ];

    private readonly StageView _view;
    private readonly EditorState _state;

    public StageOverlay(StageView view, EditorState state)
    {
        _view = view;
        _state = state;
        IsHitTestVisible = false;
        view.Rendered += () => Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual);
    }

    public override void Render(DrawingContext ctx)
    {
        if (_view.Error is { } error)
        {
            Text(ctx, "3D preview unavailable: " + error, new Point(12, 12), LabelBrush, 13, PanelBrush);
            return;
        }
        var scene = _view.Scene;
        if (scene is null)
            return;
        var project = _state.Project;

        if (_view.ShowLabels)
        {
            foreach (var fixture in project.Fixtures)
                if (_view.ToScreen(ToVector(fixture.Position) + new Vector3(0, -0.3f, 0)) is { } s)
                    Text(ctx, fixture.Name, new Point(s.X, s.Y), LabelBrush, 10, null, center: true);
            foreach (var obj in project.Stage.Objects)
                if (_view.ToScreen(ToVector(obj.Position) + new Vector3(0, (float)obj.Size.Y / 2 + 0.25f, 0)) is { } s && obj.Kind != StageObjectKind.Platform)
                    Text(ctx, obj.Name, new Point(s.X, s.Y), Brushes.Gray, 10, null, center: true);
        }

        var measurements = _view.ShowGizmos && _view.ShowMeasurements;
        if (measurements && _view.Camera.Preset == ViewPreset.Top && _view.BuildMode)
            DrawMetreRuler(ctx);

        if (_view.Gizmo() is { } gizmo)
        {
            string[] names = ["X", "Y", "Z"];
            for (var i = 0; i < 3; i++)
            {
                var width = _view.ActiveAxis == i ? 4 : 2.5;
                var pen = new Pen(AxisBrushes[i], width);
                var a = new Point(gizmo.Origin.X, gizmo.Origin.Y);
                var b = new Point(gizmo.Tips[i].X, gizmo.Tips[i].Y);
                ctx.DrawLine(pen, a, b);
                ctx.DrawEllipse(AxisBrushes[i], null, b, 4.5, 4.5);
                Text(ctx, names[i], b + new Point(6, -7), AxisBrushes[i], 11, null);
            }
            ctx.DrawEllipse(Brushes.White, null, new Point(gizmo.Origin.X, gizmo.Origin.Y), 3, 3);
        }

        if (measurements)
            DrawReadout(ctx, scene);

        if (_view.Tool == BuilderTool.Aim && _view.AimTarget is { } aim && _view.ToScreen(aim) is { } at)
        {
            var pen = new Pen(Accent, 1.5);
            ctx.DrawEllipse(null, pen, new Point(at.X, at.Y), 9, 9);
            ctx.DrawLine(pen, new Point(at.X - 14, at.Y), new Point(at.X + 14, at.Y));
            ctx.DrawLine(pen, new Point(at.X, at.Y - 14), new Point(at.X, at.Y + 14));
        }

        if (_view.RendererInfo is { } info)
            Text(ctx, info, new Point(8, Bounds.Height - 18), Brushes.DimGray, 10, null);
    }

    private void DrawReadout(DrawingContext ctx, SceneSnapshot scene)
    {
        var lines = new List<string>();
        if (_state.SelectionKind == SelectionKind.Fixture && _state.PrimaryFixture is { } fixture)
        {
            var count = _state.SelectedFixtureIds.Count;
            lines.Add(count > 1 ? $"{fixture.Name} (+{count - 1} more)" : fixture.Name);
            lines.Add($"X {M(fixture.Position.X)}   Y {M(fixture.Position.Y)}   Z {M(fixture.Position.Z)}");
            lines.Add($"Yaw {fixture.Yaw:0.#}°   Pitch {fixture.Pitch:0.#}°   DMX {fixture.Universe}.{fixture.Address}");
            if (scene.Fixtures.FirstOrDefault(v => v.Id == fixture.Id) is { } visual)
            {
                var spot = 2 * visual.BeamLength * MathF.Tan(visual.BeamAngle * 0.5f * MathF.PI / 180);
                lines.Add($"Throw {M(visual.BeamLength)}   Spot Ø {M(spot)} ({visual.BeamAngle:0.#}°)");
            }
        }
        else if (_state.SelectionKind == SelectionKind.StageObject && _state.SelectedObject is { } obj)
        {
            lines.Add($"{obj.Name} ({obj.Kind})");
            lines.Add($"X {M(obj.Position.X)}   Y {M(obj.Position.Y)}   Z {M(obj.Position.Z)}");
            lines.Add($"W {M(obj.Size.X)}   H {M(obj.Size.Y)}   D {M(obj.Size.Z)}   Yaw {obj.Yaw:0.#}°");
            lines.Add($"Top at {M(obj.Position.Y + obj.Size.Y / 2)}   bottom at {M(obj.Position.Y - obj.Size.Y / 2)}");
        }
        if (_view.BuildMode)
            lines.Add(
                _view.Tool == BuilderTool.Aim
                    ? "Aim: click/drag on the stage to point the selected fixtures"
                    : $"Drag to move · Alt+drag height · arrows lock an axis · snap {M(_state.Project.Stage.GridStep)}"
            );
        if (lines.Count == 0)
            return;
        var text = string.Join("\n", lines);
        Text(ctx, text, new Point(10, 10), LabelBrush, 11.5, PanelBrush);
    }

    /// <summary>Metre ticks along the X and Z axes in the top view.</summary>
    private void DrawMetreRuler(DrawingContext ctx)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(120, 140, 160, 190)), 1);
        for (var m = -20; m <= 20; m++)
        {
            if (m % 2 != 0)
                continue;
            if (_view.ToScreen(new Vector3(m, 0, 0)) is { } x)
            {
                ctx.DrawLine(pen, new Point(x.X, x.Y - 4), new Point(x.X, x.Y + 4));
                Text(ctx, $"{m} m", new Point(x.X, x.Y + 6), Brushes.Gray, 9, null, center: true);
            }
            if (m != 0 && _view.ToScreen(new Vector3(0, 0, m)) is { } z)
                Text(ctx, $"z {m}", new Point(z.X + 4, z.Y - 6), Brushes.Gray, 9, null);
        }
    }

    private static string M(double metres) => metres.ToString("0.00", CultureInfo.InvariantCulture) + " m";

    private static Vector3 ToVector(Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    private static void Text(DrawingContext ctx, string text, Point at, IBrush brush, double size, IBrush? background, bool center = false)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Font, size, brush);
        var origin = center ? new Point(at.X - formatted.Width / 2, at.Y) : at;
        if (background is not null)
            ctx.DrawRectangle(background, null, new Rect(origin.X - 6, origin.Y - 4, formatted.Width + 12, formatted.Height + 8), 4, 4);
        ctx.DrawText(formatted, origin);
    }
}
