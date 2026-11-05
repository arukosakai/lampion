using System.Numerics;

namespace Lampion.App.Rendering;

/// <summary>
/// A picture of the stage for the GL renderer. The stage view refills one instance in place every frame,
/// so it is only consistent on the UI thread between two refills; spans it hands out follow the refill.
/// </summary>
internal sealed class SceneSnapshot
{
    private FixtureVisual[] _fixtures = [];
    private ObjectVisual[] _objects = [];
    private int _fixtureCount;
    private int _objectCount;

    /// <summary>Fixtures to draw, in no particular order.</summary>
    public ReadOnlySpan<FixtureVisual> Fixtures => _fixtures.AsSpan(0, _fixtureCount);

    /// <summary>Set pieces to draw.</summary>
    public ReadOnlySpan<ObjectVisual> Objects => _objects.AsSpan(0, _objectCount);

    /// <summary>Haze density 0..1.</summary>
    public float Haze { get; private set; }

    /// <summary>House light 0..1.</summary>
    public float Ambient { get; private set; }

    /// <summary>How strongly the floor's metre grid shows, 0..1.</summary>
    public float GridStrength { get; private set; }

    /// <summary>Light the rig spills into the room, linear RGB.</summary>
    public Vector3 Bounce { get; private set; }

    /// <summary>Seconds of wall-clock time, for drifting haze.</summary>
    public float Time { get; private set; }

    /// <summary>Picture for LED screens, or null.</summary>
    public VideoFrame? Video { get; private set; }

    /// <summary>Where the aim tool last pointed, or null.</summary>
    public Vector3? AimTarget { get; private set; }

    /// <summary>Empties the fixture list, keeping its storage.</summary>
    public void ClearFixtures() => _fixtureCount = 0;

    /// <summary>Appends a fixture; grows the storage only when it runs out.</summary>
    public void AddFixture(in FixtureVisual visual)
    {
        if (_fixtureCount == _fixtures.Length)
            Array.Resize(ref _fixtures, Math.Max(16, _fixtures.Length * 2));
        _fixtures[_fixtureCount++] = visual;
    }

    /// <summary>Replaces the set pieces with a copy of <paramref name="objects"/>.</summary>
    public void SetObjects(ReadOnlySpan<ObjectVisual> objects)
    {
        if (_objects.Length < objects.Length)
            _objects = new ObjectVisual[Math.Max(objects.Length, _objects.Length * 2)];
        objects.CopyTo(_objects);
        _objectCount = objects.Length;
    }

    /// <summary>Sets everything but the fixtures and set pieces.</summary>
    /// <param name="haze">Haze density 0..1; beams are skipped at 0.</param>
    /// <param name="ambient">House light 0..1.</param>
    /// <param name="gridStrength">How strongly the floor's metre grid shows, 0..1.</param>
    /// <param name="bounce">Light the rig spills into the room, linear RGB.</param>
    /// <param name="time">Seconds of wall-clock time, for drifting haze.</param>
    /// <param name="video">Picture for LED screens, or null for dark screens.</param>
    /// <param name="aimTarget">Where the aim tool last pointed, or null.</param>
    public void SetEnvironment(float haze, float ambient, float gridStrength, Vector3 bounce, float time, VideoFrame? video, Vector3? aimTarget)
    {
        Haze = haze;
        Ambient = ambient;
        GridStrength = gridStrength;
        Bounce = bounce;
        Time = time;
        Video = video;
        AimTarget = aimTarget;
    }
}
