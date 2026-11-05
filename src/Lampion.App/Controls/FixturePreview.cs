using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Lampion.App.Rendering;
using Lampion.Core.Engine;
using Lampion.Core.Model;
using Silk.NET.OpenGL;

namespace Lampion.App.Controls;

/// <summary>
/// One fixture in a small set, drawn by the real stage renderer, so a profile can be checked while it is
/// being written. Drag to orbit, scroll to zoom, double-click to reset the camera.
/// </summary>
public sealed class FixturePreview : OpenGlControlBase, Avalonia.Rendering.ICustomHitTest
{
    private static readonly Vector3 HangPoint = new(0, 2.8f, 0);
    private static readonly Vector3 FloorPoint = new(0, 0.08f, 0);

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Camera _camera = new();
    private readonly DispatcherTimer _timer;
    private StageRenderer? _renderer;
    private GL? _gl;
    private SceneSnapshot? _scene;
    private Point? _dragFrom;

    public FixturePreview()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.SizeAll);
        ResetCamera();
        // The snapshot is built on the UI thread, where the profile is edited, never inside the GL callback.
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => UpdateScene());
    }

    public FixtureProfile? Profile { get; set; }

    /// <summary>Test DMX values, one per profile channel. Missing entries read as 0.</summary>
    public byte[] Values { get; set; } = [];

    public string? Error { get; private set; }

    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    private void ResetCamera()
    {
        _camera.Target = new Vector3(0, 1.6f, -1.2f);
        (_camera.Yaw, _camera.Pitch, _camera.Distance) = (0.75f, 0.12f, 8.5f);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        try
        {
            _gl = GL.GetApi(gl.GetProcAddress);
            _renderer = new StageRenderer(_gl, GlVersion.Type == GlProfileType.OpenGLES);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
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
                _renderer.Render(_scene, _camera, fb, width, height);
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
        RequestNextFrameRendering();
    }

    private void UpdateScene()
    {
        if (Profile is not { } profile)
            return;
        var time = (float)_clock.Elapsed.TotalSeconds;
        // Bars stand on the floor and uplight the wall; everything else hangs from the truss.
        var onFloor = profile.Beam == BeamKind.Bar;
        var position = onFloor ? FloorPoint : HangPoint;
        var fixture = new Fixture
        {
            Position = new Vec3(position.X, position.Y, position.Z),
            Yaw = 180,
            Pitch = onFloor ? 35 : profile.HasPanTilt ? -90 : -32,
        };

        var emitted = DmxMapper.Decode(fixture, profile, Values);
        var direction = StageMath.BeamDirection(fixture, profile, emitted.Pan, emitted.Tilt).Normalized();
        var light = new Vector3(MathF.Pow((float)emitted.Color.R, 2.2f), MathF.Pow((float)emitted.Color.G, 2.2f), MathF.Pow((float)emitted.Color.B, 2.2f));
        if (emitted.Strobe > 0.04 && (time * (1 + emitted.Strobe * 19)) % 1 > 0.18)
            light = Vector3.Zero;

        var objects = SetObjects(onFloor);
        var visual = new FixtureVisual("preview", profile.Beam, position, ToVector(direction), (float)profile.BeamAngle, light, 0, profile.HasPanTilt, false);
        var lens = visual.Lens;
        var throwDistance = StageMath.Raycast(objects, new Vec3(lens.X, lens.Y, lens.Z), direction, 40) ?? 30;
        var objectVisuals = objects.Select(o => new ObjectVisual(o.Id, o.Kind, ToVector(o.Position), ToVector(o.Size), 0, Linear(o.Color), false)).ToList();
        _scene = new SceneSnapshot([visual with { BeamLength = (float)Math.Min(throwDistance, 30) }], objectVisuals, 0.55f, 0.3f, 0.35f, Vector3.Zero, time, null, null);
    }

    private static List<StageObject> SetObjects(bool onFloor)
    {
        var wall = new StageObject { Kind = StageObjectKind.Wall, Position = new Vec3(0, 4, -4.5), Size = new Vec3(14, 8, 0.15), Color = "#26262C" };
        if (onFloor)
            return [wall];
        var truss = new StageObject { Kind = StageObjectKind.Truss, Position = new Vec3(0, HangPoint.Y + 0.42, 0), Size = new Vec3(3, 0.3, 0.3), Color = "#B8BCC4" };
        return [wall, truss];
    }

    private static Vector3 ToVector(Vec3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    private static Vector3 Linear(string hex)
    {
        var c = Core.Color.Rgb.FromHex(hex);
        return new Vector3(MathF.Pow((float)c.R, 2.2f), MathF.Pow((float)c.G, 2.2f), MathF.Pow((float)c.B, 2.2f));
    }

    // ---- Camera ----------------------------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.ClickCount == 2)
        {
            ResetCamera();
            return;
        }
        _dragFrom = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragFrom is not { } from)
            return;
        var point = e.GetPosition(this);
        _camera.Orbit((float)(point.X - from.X), (float)(point.Y - from.Y));
        _dragFrom = point;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragFrom = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _camera.Zoom((float)e.Delta.Y);
        e.Handled = true;
    }
}
