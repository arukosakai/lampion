using System.Numerics;
using Lampion.App.Rendering;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Controls.Preview;

/// <summary>
/// The last two fixture visuals the preview drew, by <see cref="PreviewBeamKey"/>. Two slots, because a strobe
/// flips between a lit and a dark look every few frames; a steady or strobing fixture then allocates nothing.
/// </summary>
internal sealed class PreviewBeamCache
{
    private readonly PreviewBeamKey[] _keys = new PreviewBeamKey[2];
    private readonly FixtureVisual[]?[] _visuals = new FixtureVisual[]?[2];
    private int _next;

    /// <summary>
    /// A one-element list with the visual for <paramref name="key"/>, built only when neither slot has it.
    /// Lists are never modified once returned, so a snapshot holding one stays valid on the render thread.
    /// </summary>
    public IReadOnlyList<FixtureVisual> Get(in PreviewBeamKey key)
    {
        for (var i = 0; i < _keys.Length; i++)
            if (_visuals[i] is { } cached && _keys[i] == key)
                return cached;
        FixtureVisual[] built = [Build(key)];
        _keys[_next] = key;
        _visuals[_next] = built;
        _next = 1 - _next;
        return built;
    }

    private static FixtureVisual Build(in PreviewBeamKey key)
    {
        var position = key.OnFloor ? PreviewSet.FloorPoint : PreviewSet.HangPoint;
        var visual = new FixtureVisual("preview", key.Kind, position, PreviewSet.ToVector(key.Direction), key.BeamAngle, key.Light, 0, key.MovingHead, false, Quaternion.Identity);
        var lens = visual.Lens;
        var throwDistance = StageMath.Raycast(PreviewSet.Objects(key.OnFloor), new Vec3(lens.X, lens.Y, lens.Z), key.Direction, 40) ?? 30;
        return visual with { BeamLength = (float)Math.Min(throwDistance, 30) };
    }
}
