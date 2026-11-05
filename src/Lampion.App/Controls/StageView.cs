using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Lampion.App.Editor;
using Lampion.App.Rendering;
using Lampion.Core.Engine;
using Lampion.Core.Model;
using Silk.NET.OpenGL;

namespace Lampion.App.Controls;

public enum BuilderTool
{
    Move,
    Aim,
}

/// <summary>The real-time 3D stage, and the stage builder that edits it in metres.</summary>
public sealed class StageView : OpenGlControlBase, Avalonia.Rendering.ICustomHitTest
{
    private readonly EditorState _state;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private StageRenderer? _renderer;
    private GL? _gl;
    private SceneSnapshot? _scene;
    private Drag? _drag;
    private Point _lastPointer;

    public StageView(EditorState state)
    {
        _state = state;
        Focusable = true;
        ClipToBounds = true;
    }

    // The GL surface is composited outside the normal render pass, so without this Avalonia sees nothing
    // drawn here and never routes pointer events to the view.
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    public Camera Camera { get; } = new();
    public BuilderTool Tool { get; set; } = BuilderTool.Move;
    public bool BuildMode { get; set; } = true;
    public bool ShowLabels { get; set; } = true;
    public VideoFrame? Video { get; set; }
    public string? RendererInfo { get; private set; }
    public string? Error { get; private set; }
    public SceneSnapshot? Scene => _scene;

    /// <summary>Axis the gizmo drag is on (0 X, 1 Y, 2 Z), so the overlay can highlight it.</summary>
    public int? ActiveAxis => _drag is AxisDrag a ? a.Axis : null;

    public Vector3? AimTarget { get; private set; }

    /// <summary>Raised after every frame so the overlay can redraw labels in step with the picture.</summary>
    public event Action? Rendered;

    // ---- GL lifecycle ----------------------------------------------------------------------------------

    protected override void OnOpenGlInit(GlInterface gl)
    {
        try
        {
            _gl = GL.GetApi(gl.GetProcAddress);
            _renderer = new StageRenderer(_gl, GlVersion.Type == GlProfileType.OpenGLES);
            RendererInfo = _renderer.Info;
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            _renderer = null;
        }
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _renderer?.Dispose();
        _renderer = null;
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var width = Math.Max(1, (int)(Bounds.Width * scale));
        var height = Math.Max(1, (int)(Bounds.Height * scale));
        if (_renderer is not null && _scene is not null)
        {
            try
            {
                _renderer.Render(_scene, Camera, fb, width, height);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                _renderer = null;
            }
        }
        else if (_gl is not null)
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)fb);
            _gl.ClearColor(0.05f, 0.05f, 0.06f, 1);
            _gl.Clear(ClearBufferMask.ColorBufferBit);
        }
        Rendered?.Invoke();
        // Haze drifts even when the show is paused, so the picture is always animating.
        RequestNextFrameRendering();
    }

    // ---- Scene snapshot --------------------------------------------------------------------------------

    /// <summary>Rebuilds the picture from the current DMX output. Called on the UI thread every frame.</summary>
    public void UpdateScene()
    {
        var project = _state.Project;
        var frame = _state.CurrentFrame;
        var time = (float)_clock.Elapsed.TotalSeconds;
        var objects = project.Stage.Objects;
        var fixtures = new List<FixtureVisual>(project.Fixtures.Count);
        foreach (var fixture in project.Fixtures)
        {
            var profile = project.ProfileOf(fixture);
            if (profile is null)
                continue;
            var emitted = DmxMapper.Decode(fixture, profile, frame.Slice(fixture, profile.Channels.Count));
            var direction = StageMath.BeamDirection(fixture, profile, emitted.Pan, emitted.Tilt).Normalized();
            var light = new Vector3((float)emitted.Color.R, (float)emitted.Color.G, (float)emitted.Color.B);
            // DMX levels are perceptual; light adds up linearly.
            light = new Vector3(MathF.Pow(light.X, 2.2f), MathF.Pow(light.Y, 2.2f), MathF.Pow(light.Z, 2.2f));
            if (emitted.Strobe > 0.04)
            {
                var rate = 1 + emitted.Strobe * 19;
                if ((time * rate) % 1 > 0.18)
                    light = Vector3.Zero;
            }
            var visual = new FixtureVisual(
                fixture.Id,
                profile.Beam,
                ToVector(fixture.Position),
                ToVector(direction),
                (float)profile.BeamAngle,
                light,
                0,
                profile.HasPanTilt,
                _state.SelectedFixtureIds.Contains(fixture.Id)
            );
            var lens = visual.Lens;
            var throwDistance = StageMath.Raycast(objects, new Vec3(lens.X, lens.Y, lens.Z), direction, 40) ?? 30;
            fixtures.Add(visual with { BeamLength = (float)Math.Min(throwDistance, 30) });
        }
        var bounce = Vector3.Zero;
        if (project.Stage.LightBounce)
        {
            // Wider beams light more of the set, so they spill more back into the room.
            foreach (var f in fixtures)
                bounce += f.Light * MathF.Sqrt(Math.Clamp(f.BeamAngle, 5, 120) / 25f);
            bounce *= 0.08f;
            var peak = MathF.Max(bounce.X, MathF.Max(bounce.Y, bounce.Z));
            if (peak > 0.6f)
                bounce *= 0.6f / peak;
        }
        var objectVisuals = objects
            .Select(o => new ObjectVisual(o.Id, o.Kind, ToVector(o.Position), ToVector(o.Size), (float)o.Yaw, ToColor(o.Color), o.Id == _state.SelectedObjectId))
            .ToList();
        _scene = new SceneSnapshot(
            fixtures,
            objectVisuals,
            (float)project.Stage.Haze,
            (float)project.Stage.Ambient,
            BuildMode ? 1f : 0.25f,
            bounce,
            time,
            Video,
            AimTarget
        );
    }

    private static Vector3 ToVector(Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    private static Vec3 ToVec(Vector3 v) => new(v.X, v.Y, v.Z);

    private static Vector3 ToColor(string hex)
    {
        var c = Core.Color.Rgb.FromHex(hex);
        return new Vector3(MathF.Pow((float)c.R, 2.2f), MathF.Pow((float)c.G, 2.2f), MathF.Pow((float)c.B, 2.2f));
    }

    // ---- Picking ---------------------------------------------------------------------------------------

    public (Vector3 Origin, Vector3 Direction) RayAt(Point p) => Camera.Ray((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height);

    public (float X, float Y)? ToScreen(Vector3 world) => Camera.Project(world, (float)Bounds.Width, (float)Bounds.Height);

    private (string Id, bool IsFixture, float Distance)? Pick(Point p)
    {
        var (origin, direction) = RayAt(p);
        (string, bool, float)? best = null;
        foreach (var fixture in _state.Project.Fixtures)
        {
            var center = ToVector(fixture.Position);
            var t = Vector3.Dot(center - origin, direction);
            if (t <= 0)
                continue;
            var miss = Vector3.Distance(origin + direction * t, center);
            if (miss < 0.32f && (best is null || t < best.Value.Item3))
                best = (fixture.Id, true, t);
        }
        // Fixtures hang inside trusses; give them priority so a click on a lamp never grabs the bar.
        if (best is not null)
            return best;
        foreach (var obj in _state.Project.Stage.Objects)
            if (StageMath.RayBox(obj, ToVec(origin), ToVec(direction)) is { } hit && (best is null || hit < best.Value.Item3))
                best = (obj.Id, false, (float)hit);
        return best;
    }

    private Vector3? SurfaceAt(Point p)
    {
        var (origin, direction) = RayAt(p);
        var hit = StageMath.Raycast(_state.Project.Stage.Objects, ToVec(origin), ToVec(direction), 300);
        return hit is { } t ? origin + direction * (float)t : null;
    }

    // ---- Dragging --------------------------------------------------------------------------------------

    private abstract record Drag(Point Start);

    private sealed record OrbitDrag(Point Start, bool Pan) : Drag(Start);

    private sealed record PlaneDrag(Point Start, float PlaneY, Vector3 Grab, Dictionary<string, Vec3> Origins, bool Vertical) : Drag(Start)
    {
        public bool Moved { get; set; }
    }

    private sealed record AxisDrag(Point Start, int Axis, Vector2 ScreenAxis, Dictionary<string, Vec3> Origins) : Drag(Start)
    {
        public bool Moved { get; set; }
    }

    private sealed record AimDrag(Point Start) : Drag(Start);

    private sealed record ClickDrag(Point Start) : Drag(Start);

    /// <summary>Where the selection's gizmo sits: the primary fixture or object.</summary>
    public Vector3? GizmoOrigin =>
        _state.SelectionKind switch
        {
            SelectionKind.Fixture when _state.PrimaryFixture is { } f => ToVector(f.Position),
            SelectionKind.StageObject when _state.SelectedObject is { } o => ToVector(o.Position),
            _ => null,
        };

    /// <summary>Screen-space end of each gizmo arrow (1 m along the axis), for drawing and hit testing.</summary>
    public (Vector2 Origin, Vector2[] Tips)? Gizmo()
    {
        if (!BuildMode || GizmoOrigin is not { } origin || ToScreen(origin) is not { } o)
            return null;
        var tips = new Vector2[3];
        Vector3[] axes = [Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ];
        for (var i = 0; i < 3; i++)
        {
            var tip = ToScreen(origin + axes[i]);
            tips[i] = tip is { } t ? new Vector2(t.X, t.Y) : new Vector2(o.X, o.Y);
        }
        return (new Vector2(o.X, o.Y), tips);
    }

    private Dictionary<string, Vec3> SelectionOrigins()
    {
        if (_state.SelectionKind == SelectionKind.StageObject && _state.SelectedObject is { } obj)
            return new() { [obj.Id] = obj.Position };
        return _state.SelectedFixtureIds.Select(_state.Project.FixtureById).OfType<Fixture>().ToDictionary(f => f.Id, f => f.Position);
    }

    private void MoveSelection(Dictionary<string, Vec3> origins, Vec3 delta)
    {
        foreach (var (id, start) in origins)
        {
            var target = StageMath.Round(start + delta);
            if (_state.Project.FixtureById(id) is { } fixture)
                fixture.Position = target;
            else if (_state.Project.Stage.Objects.Find(o => o.Id == id) is { } obj)
                obj.Position = target;
        }
        _state.Touch();
    }

    private double Step => _state.Project.Stage.GridStep;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;
        _lastPointer = point;
        e.Pointer.Capture(this);

        if (props.IsRightButtonPressed || props.IsMiddleButtonPressed)
        {
            _drag = new OrbitDrag(point, props.IsMiddleButtonPressed || e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            return;
        }
        if (!props.IsLeftButtonPressed)
            return;

        if (BuildMode && Tool == BuilderTool.Aim && _state.SelectedFixtureIds.Count > 0)
        {
            AimSelectionAt(point);
            _drag = new AimDrag(point);
            return;
        }

        if (BuildMode && Gizmo() is { } gizmo)
        {
            var p = new Vector2((float)point.X, (float)point.Y);
            for (var axis = 0; axis < 3; axis++)
            {
                if (DistanceToSegment(p, gizmo.Origin, gizmo.Tips[axis]) > 7)
                    continue;
                var screen = gizmo.Tips[axis] - gizmo.Origin;
                if (screen.Length() < 4)
                    continue;
                _drag = new AxisDrag(point, axis, screen, SelectionOrigins());
                return;
            }
        }

        var additive = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (Pick(point) is { } picked)
        {
            if (picked.IsFixture)
            {
                if (!_state.SelectedFixtureIds.Contains(picked.Id) || additive)
                    _state.SelectFixtures([picked.Id], additive);
            }
            else if (_state.SelectedObjectId != picked.Id)
                _state.SelectObject(picked.Id);

            if (BuildMode && !additive && GizmoOrigin is { } origin)
            {
                var (rayOrigin, direction) = RayAt(point);
                var vertical = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
                var grab = vertical ? origin : IntersectPlaneY(rayOrigin, direction, origin.Y) ?? origin;
                _drag = new PlaneDrag(point, origin.Y, grab, SelectionOrigins(), vertical);
            }
            return;
        }
        _drag = new ClickDrag(point);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        var delta = point - _lastPointer;
        _lastPointer = point;
        switch (_drag)
        {
            case OrbitDrag { Pan: true }:
                Camera.Pan((float)delta.X, (float)delta.Y, (float)Bounds.Height);
                break;
            case OrbitDrag or ClickDrag:
                if (_drag is ClickDrag click && Distance(point, click.Start) < 4)
                    break;
                Camera.Orbit((float)delta.X, (float)delta.Y);
                break;
            case AimDrag:
                AimSelectionAt(point);
                break;
            case AxisDrag axis:
            {
                var moved = new Vector2((float)(point.X - axis.Start.X), (float)(point.Y - axis.Start.Y));
                var length = axis.ScreenAxis.Length();
                var metres = StageMath.Snap(Vector2.Dot(moved, axis.ScreenAxis / length) / length, Step);
                var d = axis.Axis switch
                {
                    0 => new Vec3(metres, 0, 0),
                    1 => new Vec3(0, metres, 0),
                    _ => new Vec3(0, 0, metres),
                };
                axis.Moved = true;
                MoveSelection(axis.Origins, d);
                break;
            }
            case PlaneDrag plane:
            {
                var (origin, direction) = RayAt(point);
                Vec3 d;
                if (plane.Vertical)
                {
                    // Height drag: intersect a vertical plane facing the camera through the grab point.
                    var (_, _, forward) = Camera.Basis();
                    var normal = Vector3.Normalize(new Vector3(forward.X, 0, forward.Z));
                    var denom = Vector3.Dot(direction, normal);
                    if (MathF.Abs(denom) < 1e-4f)
                        return;
                    var t = Vector3.Dot(plane.Grab - origin, normal) / denom;
                    d = new Vec3(0, StageMath.Snap((origin + direction * t).Y - plane.Grab.Y, Step), 0);
                }
                else
                {
                    if (IntersectPlaneY(origin, direction, plane.PlaneY) is not { } hit)
                        return;
                    var raw = hit - plane.Grab;
                    d = new Vec3(StageMath.Snap(raw.X, Step), 0, StageMath.Snap(raw.Z, Step));
                }
                plane.Moved = true;
                MoveSelection(plane.Origins, d);
                break;
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
        var drag = _drag;
        _drag = null;
        switch (drag)
        {
            case ClickDrag click when Distance(e.GetPosition(this), click.Start) < 4:
                _state.Deselect();
                break;
            case PlaneDrag { Moved: true } or AxisDrag { Moved: true } or AimDrag:
                _state.Commit();
                break;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Camera.Zoom((float)e.Delta.Y);
        e.Handled = true;
    }

    private void AimSelectionAt(Point point)
    {
        if (SurfaceAt(point) is not { } target)
            return;
        AimTarget = target;
        foreach (var id in _state.SelectedFixtureIds)
            if (_state.Project.FixtureById(id) is { } fixture)
                StageMath.AimAt(fixture, ToVec(target));
        _state.Touch();
    }

    private static Vector3? IntersectPlaneY(Vector3 origin, Vector3 direction, float y)
    {
        if (MathF.Abs(direction.Y) < 1e-5f)
            return null;
        var t = (y - origin.Y) / direction.Y;
        return t > 0 ? origin + direction * t : null;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var t = Math.Clamp(Vector2.Dot(p - a, ab) / Math.Max(1e-5f, ab.LengthSquared()), 0, 1);
        return Vector2.Distance(p, a + ab * t);
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    public void ApplyPreset(ViewPreset preset)
    {
        Camera.Apply(preset);
        if (preset != ViewPreset.Perspective)
        {
            Camera.Target = new Vector3(0, preset == ViewPreset.Top ? 0 : 3, -1);
            Camera.Distance = 16;
        }
        else
            Camera.Target = new Vector3(0, 2.2f, -1);
    }

    public void FrameSelection()
    {
        if (GizmoOrigin is { } origin)
            Camera.Target = origin;
    }
}
