using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lampion.App.Controls.Stage3D;
using Lampion.App.Editor;
using Lampion.App.Rendering;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Controls;

/// <summary>
/// 2D annotations over the 3D stage: names, the move gizmo, and measurements in metres (position,
/// throw distance and spot size) — the numbers a lighting designer needs when hanging a rig.
/// Redrawn after every 3D frame without allocating, apart from text that actually changed.
/// </summary>
internal sealed class StageOverlay : Control
{
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.FromArgb(200, 230, 230, 235));
    private static readonly IBrush PanelBrush = new SolidColorBrush(Color.FromArgb(190, 18, 18, 22));
    private static readonly Pen AimPen = new(new SolidColorBrush(Color.FromRgb(45, 140, 235)), 1.5);
    private static readonly IBrush[] AxisBrushes =
    [
        new SolidColorBrush(Color.FromRgb(235, 72, 72)),
        new SolidColorBrush(Color.FromRgb(90, 210, 90)),
        new SolidColorBrush(Color.FromRgb(70, 140, 255)),
    ];
    private static readonly Pen[] AxisPens = [new(AxisBrushes[0], 4), new(AxisBrushes[1], 4), new(AxisBrushes[2], 4)];
    private static readonly IBrush[] HighlightAxisBrushes =
    [
        new SolidColorBrush(Color.FromRgb(255, 160, 160)),
        new SolidColorBrush(Color.FromRgb(175, 245, 175)),
        new SolidColorBrush(Color.FromRgb(165, 200, 255)),
    ];
    private static readonly Pen[] HighlightAxisPens = [new(HighlightAxisBrushes[0], 6), new(HighlightAxisBrushes[1], 6), new(HighlightAxisBrushes[2], 6)];
    private static readonly string[] AxisNames = ["X", "Y", "Z"];
    private static readonly Vector3 FixtureLabelDrop = new(0, -0.3f, 0);

    private readonly StageView _view;
    private readonly EditorState _state;
    private readonly SelectionReadout _readout;
    private readonly FrameRateBadge _frameRate = new();
    private readonly Action _invalidate;
    private string? _error;
    private string _errorText = "";

    /// <summary>Annotates <paramref name="view"/>, redrawing whenever it renders a frame.</summary>
    public StageOverlay(StageView view, EditorState state)
    {
        _view = view;
        _state = state;
        _readout = new SelectionReadout(view, state);
        _invalidate = InvalidateVisual;
        IsHitTestVisible = false;
        view.Rendered += OnRendered;
    }

    private void OnRendered() => Avalonia.Threading.Dispatcher.UIThread.Post(_invalidate);

    /// <inheritdoc/>
    public override void Render(DrawingContext ctx)
    {
        if (_view.Error is { } error)
        {
            OverlayText.Draw(ctx, ErrorText(error), new Point(12, 12), LabelBrush, 13, PanelBrush);
            return;
        }
        if (_view.Scene is not { } scene)
            return;
        var projector = _view.Projector();

        if (_view.ShowLabels)
            DrawLabels(ctx, in projector);

        var measurements = _view.ShowGizmos && _view.ShowMeasurements;
        if (measurements && _view.Camera.Preset == ViewPreset.Top && _view.BuildMode)
            MetreRuler.Draw(ctx, in projector);

        if (_view.Gizmo() is { } gizmo)
            DrawMoveGizmo(ctx, gizmo);

        if (measurements && _readout.Compose(scene) is { } readout)
            OverlayText.Draw(ctx, readout, new Point(10, 10), LabelBrush, 11.5, PanelBrush);

        if (_view.Tool == BuilderTool.Aim && _view.AimTarget is { } aim && projector.TryProject(aim, out var at))
        {
            ctx.DrawEllipse(null, AimPen, new Point(at.X, at.Y), 9, 9);
            ctx.DrawLine(AimPen, new Point(at.X - 14, at.Y), new Point(at.X + 14, at.Y));
            ctx.DrawLine(AimPen, new Point(at.X, at.Y - 14), new Point(at.X, at.Y + 14));
        }

        if (_view.RendererInfo is { } info)
            OverlayText.Draw(ctx, info, new Point(8, Bounds.Height - 18), Brushes.DimGray, 10, null);

        if (_view.ShowFps)
            _frameRate.Draw(ctx, Bounds.Size, _view.FramesPerSecond, _view.Fidelity, LabelBrush, PanelBrush);
    }

    private void DrawLabels(DrawingContext ctx, in ScreenProjector projector)
    {
        var project = _state.Project;
        var fixtures = project.Fixtures;
        for (var i = 0; i < fixtures.Count; i++)
            if (projector.TryProject(StageGeometry.ToVector(fixtures[i].Position) + FixtureLabelDrop, out var s))
                OverlayText.Draw(ctx, fixtures[i].Name, new Point(s.X, s.Y), LabelBrush, 10, null, center: true);
        var objects = project.Stage.Objects;
        for (var i = 0; i < objects.Count; i++)
        {
            var obj = objects[i];
            if (projector.TryProject(StageGeometry.ToVector(obj.Position) + new Vector3(0, (float)obj.Size.Y / 2 + 0.25f, 0), out var s) && obj.Kind != StageObjectKind.Platform)
                OverlayText.Draw(ctx, obj.Name, new Point(s.X, s.Y), Brushes.Gray, 10, null, center: true);
        }
    }

    private void DrawMoveGizmo(DrawingContext ctx, in GizmoHandles gizmo)
    {
        var a = new Point(gizmo.Origin.X, gizmo.Origin.Y);
        var highlighted = _view.HighlightedAxis;
        for (var i = 0; i < 3; i++)
        {
            var tip = gizmo.Tip(i);
            var b = new Point(tip.X, tip.Y);
            var lit = highlighted == i;
            var brush = lit ? HighlightAxisBrushes[i] : AxisBrushes[i];
            ctx.DrawLine(lit ? HighlightAxisPens[i] : AxisPens[i], a, b);
            var radius = lit ? 8.5 : 6.5;
            ctx.DrawEllipse(brush, null, b, radius, radius);
            OverlayText.Draw(ctx, AxisNames[i], b + new Point(radius + 2, -8), brush, 11, null);
        }
        ctx.DrawEllipse(Brushes.White, null, a, 4, 4);
    }

    private string ErrorText(string error)
    {
        if (!ReferenceEquals(error, _error))
            (_error, _errorText) = (error, "3D preview unavailable: " + error);
        return _errorText;
    }
}
