using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Lampion.App.Controls.Preview;
using Lampion.App.Rendering;
using Lampion.Core.Model.Rig;
using Silk.NET.OpenGL;

namespace Lampion.App.Controls;

/// <summary>
/// One fixture in a small set, drawn by the real stage renderer, so a profile can be checked while it is
/// being written. Drag to orbit, scroll to zoom, double-click to reset the camera.
/// </summary>
internal sealed class FixturePreview : OpenGlControlBase, Avalonia.Rendering.ICustomHitTest
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Camera _camera = new();
    private readonly DispatcherTimer _timer;
    private readonly PreviewScene _builder = new();
    private readonly Cursor _orbitCursor = new(StandardCursorType.SizeAll);
    private readonly Cursor _aimCursor = new(StandardCursorType.Cross);
    private StageRenderer? _renderer;
    private GL? _gl;
    private SceneSnapshot? _scene;
    private Point? _dragFrom;
    private bool _aimMode;
    private Vector3? _aimTarget;

    /// <summary>Creates the preview; it starts drawing once attached to a window.</summary>
    public FixturePreview()
    {
        ClipToBounds = true;
        Cursor = _orbitCursor;
        ResetCamera();
        // The snapshot is built on the UI thread, where the profile is edited, never inside the GL callback.
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => UpdateScene());
    }

    /// <summary>The profile to draw, read every frame so edits show at once; null draws an empty set.</summary>
    public FixtureProfile? Profile { get; set; }

    /// <summary>Why the 3D view couldn't start or stopped drawing, or null while it works.</summary>
    public string? Error { get; private set; }

    /// <summary>In aim mode a click or drag on the set points a moving head there instead of orbiting.</summary>
    public bool AimMode
    {
        get => _aimMode;
        set
        {
            _aimMode = value;
            _aimTarget = null;
            Cursor = value ? _aimCursor : _orbitCursor;
        }
    }

    /// <summary>Raised with the pan/tilt (0..1) that reach the clicked spot; the owner writes them into its values.</summary>
    public event Action<double, double>? Aimed;

    /// <summary>
    /// Sets the test DMX values, one per profile channel (missing entries read as absent). The values are
    /// copied, so the caller may reuse its buffer; allocates only when the footprint grows.
    /// </summary>
    public void SetValues(ReadOnlySpan<byte> values) => _builder.SetValues(values);

    /// <summary>Hit anywhere inside the bounds, so drags on the GL surface reach the control.</summary>
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _renderer?.Dispose();
        _renderer = null;
    }

    /// <inheritdoc />
    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var width = Math.Max(1, (int)(Bounds.Width * scale));
        var height = Math.Max(1, (int)(Bounds.Height * scale));
        if (_renderer is not null && _scene is not null)
        {
            try
            {
                _renderer.Render(_scene, _camera, fb, width, height, StageFidelity.Quality);
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

    private void ResetCamera()
    {
        _camera.Target = new Vector3(0, 1.6f, -1.2f);
        (_camera.Yaw, _camera.Pitch, _camera.Distance) = (0.75f, 0.12f, 8.5f);
    }

    private void UpdateScene()
    {
        if (Profile is { } profile)
            _scene = _builder.Build(profile, (float)_clock.Elapsed.TotalSeconds, _aimMode ? _aimTarget : null);
    }

    private void AimAt(Point point)
    {
        if (Profile is not { HasPanTilt: true } profile)
            return;
        var (origin, direction) = _camera.Ray((float)point.X, (float)point.Y, (float)Bounds.Width, (float)Bounds.Height);
        if (_builder.AimAt(profile, origin, direction, out var hit) is not { } aim)
            return;
        _aimTarget = hit;
        Aimed?.Invoke(aim.Pan, aim.Tilt);
    }

    // ---- Camera ----------------------------------------------------------------------------------------

    /// <inheritdoc />
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
        // Right-drag still orbits in aim mode, so the view never gets stuck.
        if (_aimMode && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            AimAt(_dragFrom.Value);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragFrom is not { } from)
            return;
        var point = e.GetPosition(this);
        if (_aimMode && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            AimAt(point);
            _dragFrom = point;
            return;
        }
        _camera.Orbit((float)(point.X - from.X), (float)(point.Y - from.Y));
        _dragFrom = point;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragFrom = null;
        e.Pointer.Capture(null);
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _camera.Zoom((float)e.Delta.Y);
        e.Handled = true;
    }
}
