using System.Numerics;
using Lampion.App.Editor;
using Lampion.App.Rendering;
using Lampion.Core.Engine;
using Lampion.Core.Model;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Controls.Stage3D;

/// <summary>
/// Turns the editor's current DMX output into the <see cref="SceneSnapshot"/> the renderer draws. What only
/// changes with an edit or a new selection (profile lookups, set-piece visuals) is worked out again only then;
/// per frame it just decodes DMX and casts the beams, without allocating.
/// </summary>
internal sealed partial class StageSceneBuilder
{
    private const float MaxThrow = 30;

    private readonly EditorState _state;
    private readonly SceneSnapshot _scene = new();
    private RigEntry[] _rig = [];
    private int _rigCount;
    private ObjectVisual[] _objects = [];
    private Project? _builtFor;
    private int _builtVersion = -1;
    private bool _selectionChanged = true;

    /// <summary>Builds scenes from <paramref name="state"/>, watching it for selection changes.</summary>
    public StageSceneBuilder(EditorState state)
    {
        _state = state;
        state.SelectionChanged += OnSelectionChanged;
    }

    /// <summary>The scene, refilled in place by every <see cref="Update"/>.</summary>
    public SceneSnapshot Scene => _scene;

    private void OnSelectionChanged() => _selectionChanged = true;

    /// <summary>Refills <see cref="Scene"/> from the DMX at the playhead. Call on the UI thread.</summary>
    /// <param name="time">Wall-clock seconds, for strobes and drifting haze.</param>
    /// <param name="gridStrength">How strongly the floor grid shows, 0..1.</param>
    /// <param name="video">Picture for LED screens, or null.</param>
    /// <param name="aimTarget">Where the aim tool last pointed, or null.</param>
    public void Update(float time, float gridStrength, VideoFrame? video, Vector3? aimTarget)
    {
        var project = _state.Project;
        if (!ReferenceEquals(project, _builtFor) || _state.Version != _builtVersion || _selectionChanged)
            Rebuild(project);

        var frame = _state.CurrentFrame;
        var objects = project.Stage.Objects;
        _scene.ClearFixtures();
        var bounce = Vector3.Zero;
        for (var i = 0; i < _rigCount; i++)
        {
            ref readonly var rig = ref _rig[i];
            var visual = Emit(in rig, frame, objects, time);
            _scene.AddFixture(in visual);
            bounce += visual.Light * rig.BounceWeight;
        }
        _scene.SetEnvironment((float)project.Stage.Haze, (float)project.Stage.Ambient, gridStrength, project.Stage.LightBounce ? RoomBounce(bounce) : Vector3.Zero, time, video, aimTarget);
    }

    private static FixtureVisual Emit(in RigEntry rig, DmxFrame frame, IReadOnlyList<StageObject> objects, float time)
    {
        var fixture = rig.Fixture;
        var profile = rig.Profile;
        var emitted = DmxMapper.Decode(fixture, profile, frame.Slice(fixture, profile.Channels.Count));
        var direction = StageMath.BeamDirection(fixture, profile, emitted.Pan, emitted.Tilt).Normalized();
        // DMX levels are perceptual; light adds up linearly.
        var light = new Vector3(MathF.Pow((float)emitted.Color.R, 2.2f), MathF.Pow((float)emitted.Color.G, 2.2f), MathF.Pow((float)emitted.Color.B, 2.2f));
        if (emitted.Strobe > 0.04)
        {
            var rate = 1 + emitted.Strobe * 19;
            if ((time * rate) % 1 > 0.18)
                light = Vector3.Zero;
        }
        var position = StageGeometry.ToVector(fixture.Position);
        var beam = StageGeometry.ToVector(direction);
        var lens = position + beam * rig.LensOffset;
        var throwDistance = StageMath.Raycast(objects, new Vec3(lens.X, lens.Y, lens.Z), direction, 40) ?? MaxThrow;
        return new FixtureVisual(fixture.Id, rig.Kind, position, beam, rig.BeamAngle, light, (float)Math.Min(throwDistance, MaxThrow), rig.MovingHead, rig.Selected);
    }

    /// <summary>The rig's summed spill, scaled into a gentle room tint that never passes 0.6.</summary>
    private static Vector3 RoomBounce(Vector3 sum)
    {
        var bounce = sum * 0.08f;
        var peak = MathF.Max(bounce.X, MathF.Max(bounce.Y, bounce.Z));
        return peak > 0.6f ? bounce * (0.6f / peak) : bounce;
    }

    private void Rebuild(Project project)
    {
        var fixtures = project.Fixtures;
        if (_rig.Length < fixtures.Count)
            _rig = new RigEntry[fixtures.Count];
        _rigCount = 0;
        for (var i = 0; i < fixtures.Count; i++)
            if (project.ProfileOf(fixtures[i]) is { } profile)
                _rig[_rigCount++] = new RigEntry(fixtures[i], profile, _state.SelectedFixtureIds.Contains(fixtures[i].Id));

        var objects = project.Stage.Objects;
        if (_objects.Length != objects.Count)
            _objects = new ObjectVisual[objects.Count];
        var selected = _state.SelectedObjectId;
        for (var i = 0; i < objects.Count; i++)
        {
            var o = objects[i];
            _objects[i] = new ObjectVisual(o.Id, o.Kind, StageGeometry.ToVector(o.Position), StageGeometry.ToVector(o.Size), (float)o.Yaw, StageGeometry.LinearColor(o.Color), o.Id == selected);
        }
        _scene.SetObjects(_objects);

        _builtFor = project;
        _builtVersion = _state.Version;
        _selectionChanged = false;
    }
}
