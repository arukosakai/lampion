using System.Numerics;
using Lampion.App.Editor;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Controls.Stage3D;

/// <summary>Hit tests world rays against the rig and the set. Allocation-free.</summary>
internal sealed class StagePicker(EditorState state)
{
    // How close (metres) a ray must pass to a fixture's centre to grab it.
    private const float FixtureRadius = 0.32f;

    /// <summary>The nearest fixture along the ray, else the nearest set piece, else null.</summary>
    /// <param name="origin">Ray start, metres.</param>
    /// <param name="direction">Unit ray direction.</param>
    public StagePick? Pick(Vector3 origin, Vector3 direction)
    {
        StagePick? best = null;
        var fixtures = state.Project.Fixtures;
        for (var i = 0; i < fixtures.Count; i++)
        {
            var center = StageGeometry.ToVector(fixtures[i].Position);
            var t = Vector3.Dot(center - origin, direction);
            if (t <= 0)
                continue;
            var miss = Vector3.Distance(origin + direction * t, center);
            if (miss < FixtureRadius && (best is null || t < best.Value.Distance))
                best = new StagePick(fixtures[i].Id, true, t);
        }
        // Fixtures hang inside trusses; give them priority so a click on a lamp never grabs the bar.
        if (best is not null)
            return best;
        var objects = state.Project.Stage.Objects;
        var from = StageGeometry.ToVec(origin);
        var along = StageGeometry.ToVec(direction);
        for (var i = 0; i < objects.Count; i++)
            if (StageMath.RayBox(objects[i], from, along) is { } hit && (best is null || hit < best.Value.Distance))
                best = new StagePick(objects[i].Id, false, (float)hit);
        return best;
    }

    /// <summary>Where the ray first lands on the floor or a solid set piece (trusses excluded), or null.</summary>
    public Vector3? SurfaceAt(Vector3 origin, Vector3 direction)
    {
        var hit = StageMath.Raycast(state.Project.Stage.Objects, StageGeometry.ToVec(origin), StageGeometry.ToVec(direction), 300);
        return hit is { } t ? origin + direction * (float)t : null;
    }
}
