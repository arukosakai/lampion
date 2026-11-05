using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Lampion.App.Controls.Stage3D;
using Lampion.App.Editor;
using Lampion.App.Rendering;
using Silk.NET.OpenGL;

namespace Lampion.App.Controls;

/// <summary>
/// The real-time 3D stage, and the stage builder that edits it in metres. Wires the GL renderer, the scene
/// builder, picking and the drag interactions together; it draws and edits nothing itself.
/// </summary>
internal sealed class StageView : OpenGlControlBase, Avalonia.Rendering.ICustomHitTest
{
    private readonly EditorState _state;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly StageSceneBuilder _scene;
    private readonly SelectionEditor _editor;
    private readonly StageInteraction _interaction;
    private StageRenderer? _renderer;
    private GL? _gl;
    private bool _hasScene;
    private long _lastFrame;

    /// <summary>A view of <paramref name="state"/>'s stage; edits go through it so they can be undone.</summary>
    public StageView(EditorState state)
    {
        _state = state;
        _scene = new StageSceneBuilder(state);
        var picker = new StagePicker(state);
        _editor = new SelectionEditor(state, picker);
        _interaction = new StageInteraction(this, state, picker, _editor);
        Focusable = true;
        ClipToBounds = true;
    }

    // The GL surface is composited outside the normal render pass, so without this Avalonia sees nothing
    // drawn here and never routes pointer events to the view.
    /// <summary>Takes pointer events anywhere inside the view.</summary>
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    /// <summary>The viewing camera; menus and the scene gizmo drive it directly.</summary>
    public Camera Camera { get; } = new();

    /// <summary>The active tool.</summary>
    public BuilderTool Tool { get; set; } = BuilderTool.Move;

    /// <summary>Whether a tool that edits the rig is active (anything but Hand).</summary>
    public bool BuildMode => Tool != BuilderTool.Hand;

    /// <summary>Whether fixture and set-piece names are drawn over the stage.</summary>
    public bool ShowLabels { get; set; } = true;

    /// <summary>Master switch over the three gizmo kinds below, like Blender's viewport gizmo toggle.</summary>
    public bool ShowGizmos { get; set; } = true;

    /// <summary>Whether the navigation gizmo (axis balls) shows, when gizmos are on.</summary>
    public bool ShowNavigation { get; set; } = true;

    /// <summary>Whether the move arrows show on the selection, when gizmos are on.</summary>
    public bool ShowMoveHandles { get; set; } = true;

    /// <summary>Whether the metre readout and top-view ruler show, when gizmos are on.</summary>
    public bool ShowMeasurements { get; set; } = true;

    /// <summary>How much GPU work each frame spends; per machine, from the user settings.</summary>
    public StageFidelity Fidelity { get; set; } = StageFidelity.Quality;

    /// <summary>Whether the frame-rate badge shows.</summary>
    public bool ShowFps { get; set; }

    /// <summary>Frames actually drawn per second, smoothed over about half a second.</summary>
    public double FramesPerSecond { get; private set; }

    /// <summary>Picture shown on LED screens, or null for dark screens. Shared with the media player.</summary>
    public VideoFrame? Video { get; set; }

    /// <summary>GPU and driver description once GL is up, or null.</summary>
    public string? RendererInfo { get; private set; }

    /// <summary>Why the 3D preview isn't drawing, or null when it is.</summary>
    public string? Error { get; private set; }

    /// <summary>The scene last built by <see cref="UpdateScene"/>, or null before the first. Refilled in place every frame.</summary>
    public SceneSnapshot? Scene => _hasScene ? _scene.Scene : null;

    /// <summary>Axis the gizmo drag is on (0 X, 1 Y, 2 Z), so the overlay can highlight it.</summary>
    public int? ActiveAxis => _interaction.ActiveAxis;

    /// <summary>Where the aim tool last pointed, or null.</summary>
    public Vector3? AimTarget => _editor.AimTarget;

    /// <summary>Raised after every frame so the overlay can redraw labels in step with the picture.</summary>
    public event Action? Rendered;

    // ---- GL lifecycle ----------------------------------------------------------------------------------

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _renderer?.Dispose();
        _renderer = null;
    }

    /// <inheritdoc/>
    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var width = Math.Max(1, (int)(Bounds.Width * scale));
        var height = Math.Max(1, (int)(Bounds.Height * scale));
        if (_renderer is not null && Scene is { } scene)
        {
            try
            {
                _renderer.Render(scene, Camera, fb, width, height, Fidelity);
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
        var now = Stopwatch.GetTimestamp();
        if (_lastFrame != 0)
        {
            var instant = Stopwatch.Frequency / (double)Math.Max(1, now - _lastFrame);
            FramesPerSecond = FramesPerSecond <= 0 ? instant : FramesPerSecond * 0.95 + instant * 0.05;
        }
        _lastFrame = now;
        Rendered?.Invoke();
        // Haze drifts even when the show is paused, so the picture is always animating.
        RequestNextFrameRendering();
    }

    // ---- Scene -----------------------------------------------------------------------------------------

    /// <summary>Rebuilds the picture from the current DMX output. Called on the UI thread every frame; allocation-free between edits.</summary>
    public void UpdateScene()
    {
        _scene.Update((float)_clock.Elapsed.TotalSeconds, BuildMode ? 1f : 0.25f, Video, AimTarget);
        _hasScene = true;
    }

    // ---- Picking and the gizmo -------------------------------------------------------------------------

    /// <summary>World ray through a point of the view (pixels from its top-left).</summary>
    public (Vector3 Origin, Vector3 Direction) RayAt(Point p) => Camera.Ray((float)p.X, (float)p.Y, (float)Bounds.Width, (float)Bounds.Height);

    /// <summary>View pixel of a world point, or null when it is behind the camera.</summary>
    public (float X, float Y)? ToScreen(Vector3 world) => Camera.Project(world, (float)Bounds.Width, (float)Bounds.Height);

    /// <summary>The current view frozen for projecting many points this frame (pixels from the view's top-left).</summary>
    public ScreenProjector Projector() => Camera.Projector((float)Bounds.Width, (float)Bounds.Height);

    /// <summary>Where the selection's gizmo sits: the primary fixture or object.</summary>
    public Vector3? GizmoOrigin =>
        _state.SelectionKind switch
        {
            SelectionKind.Fixture when StageSelection.PrimaryFixture(_state) is { } f => StageGeometry.ToVector(f.Position),
            SelectionKind.StageObject when StageSelection.SelectedObject(_state) is { } o => StageGeometry.ToVector(o.Position),
            _ => null,
        };

    /// <summary>The move gizmo on screen, or null when move handles are hidden, nothing is selected or the selection is behind the camera.</summary>
    public GizmoHandles? Gizmo()
    {
        if (!BuildMode || !(ShowGizmos && ShowMoveHandles) || GizmoOrigin is not { } origin)
            return null;
        var projector = Projector();
        if (!projector.TryProject(origin, out var o))
            return null;
        return new GizmoHandles(o, Tip(projector, origin + Vector3.UnitX, o), Tip(projector, origin + Vector3.UnitY, o), Tip(projector, origin + Vector3.UnitZ, o));
    }

    private static Vector2 Tip(in ScreenProjector projector, Vector3 world, Vector2 fallback) => projector.TryProject(world, out var tip) ? tip : fallback;

    // ---- Input -----------------------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetPosition(this);
        e.Pointer.Capture(this);
        _interaction.Press(point, e.GetCurrentPoint(this).Properties, e.KeyModifiers);
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _interaction.Move(e.GetPosition(this));
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
        _interaction.Release(e.GetPosition(this));
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Camera.Zoom((float)e.Delta.Y);
        e.Handled = true;
    }

    // ---- View commands ---------------------------------------------------------------------------------

    /// <summary>Jumps to a named view, framing the stage for the axis views.</summary>
    public void ApplyPreset(ViewPreset preset)
    {
        Camera.Apply(preset);
        if (preset == ViewPreset.Free)
            return;
        Camera.Target = new Vector3(0, preset is ViewPreset.Top or ViewPreset.Bottom ? 0 : 3, -1);
        Camera.Distance = 16;
    }

    /// <summary>Centres the orbit on the selection (F).</summary>
    public void FrameSelection()
    {
        if (GizmoOrigin is { } origin)
            Camera.Target = origin;
    }
}
