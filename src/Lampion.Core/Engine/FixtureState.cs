using Lampion.Core.Color;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Engine;

/// <summary>
/// What a fixture should do at one instant, before it is mapped to channels. <see cref="Light"/> is
/// premultiplied (colour × intensity), so blending and fading behave like real light. <c>default</c> is black
/// with nothing else set.
/// </summary>
/// <remarks>
/// A self-contained value (about 0.5 KB, no heap references) so the evaluator can keep one per fixture in a
/// reused array and blend in place. Pass it by <c>ref</c>/<c>in</c>; copying is correct but not free.
/// Roles keep the order they were first set in, because a coarse pan/tilt write also fills its fine channel
/// and whichever of the two is written last wins.
/// </remarks>
public struct FixtureState
{
    /// <summary>Most distinct raw channel offsets one state holds; further offsets are dropped (see <see cref="SetRaw"/>).</summary>
    public const int MaxRawOverrides = 16;

    private RoleValues _roles;
    private RoleOrder _roleOrder;
    private RawOverrides _raw;
    private uint _roleMask;
    private byte _roleCount;
    private byte _rawCount;

    /// <summary>Premultiplied light, channels nominally 0..1 (may exceed 1 mid-blend; clamped when mapped).</summary>
    public Rgb Light { readonly get; set; }

    /// <summary>Explicit white level, or null to let the mapper derive white from <see cref="Light"/>.</summary>
    public double? White { readonly get; set; }

    /// <summary>Number of roles set, in first-set order (see <see cref="RoleAt"/>).</summary>
    internal readonly int RoleCount => _roleCount;

    /// <summary>Number of raw overrides held (see <see cref="RawAt"/>).</summary>
    internal readonly int RawCount => _rawCount;

    /// <summary>The <paramref name="i"/>-th role set, in first-set order.</summary>
    internal readonly ChannelRole RoleAt(int i) => (ChannelRole)_roleOrder[i];

    /// <summary>The <paramref name="i"/>-th raw override, in first-set order.</summary>
    internal readonly RawOverride RawAt(int i) => _raw[i];

    /// <summary>Sets a 0..1 value for a channel role (automation). Undefined role values are ignored.</summary>
    public void SetRole(ChannelRole role, double value)
    {
        if (!ChannelRoles.IsValid(role))
            return;
        var i = (int)role;
        var bit = 1u << i;
        if ((_roleMask & bit) == 0)
        {
            _roleMask |= bit;
            _roleOrder[_roleCount++] = (byte)i;
        }
        _roles[i] = value;
    }

    /// <summary>The value set for <paramref name="role"/>, if any.</summary>
    public readonly bool TryGetRole(ChannelRole role, out double value)
    {
        if (ChannelRoles.IsValid(role) && (_roleMask & (1u << (int)role)) != 0)
        {
            value = _roles[(int)role];
            return true;
        }
        value = 0;
        return false;
    }

    /// <summary>
    /// Forces a 0..1 value onto the channel at 0-based <paramref name="offset"/>; it wins over everything else
    /// when mapped. An offset already held is updated in place.
    /// </summary>
    /// <returns>False when the offset is new but <see cref="MaxRawOverrides"/> others are already held, so it was dropped.</returns>
    public bool SetRaw(int offset, double value)
    {
        for (var i = 0; i < _rawCount; i++)
            if (_raw[i].Offset == offset)
            {
                _raw[i] = new RawOverride(offset, value);
                return true;
            }
        if (_rawCount == MaxRawOverrides)
            return false;
        _raw[_rawCount++] = new RawOverride(offset, value);
        return true;
    }

    /// <summary>The raw override for 0-based <paramref name="offset"/>, if any.</summary>
    public readonly bool TryGetRaw(int offset, out double value)
    {
        for (var i = 0; i < _rawCount; i++)
            if (_raw[i].Offset == offset)
            {
                value = _raw[i].Value;
                return true;
            }
        value = 0;
        return false;
    }

    /// <summary>Scales the light (and an explicit white) by <paramref name="k"/>; roles and raw channels are positional and stay as they are.</summary>
    public void Scale(double k)
    {
        Light *= k;
        if (White is { } w)
            White = w * k;
    }

    /// <summary>Puts <paramref name="top"/> over this state with weight <paramref name="alpha"/> (clamped to 0..1; 0 does nothing).</summary>
    public void Blend(in FixtureState top, double alpha, BlendMode mode)
    {
        alpha = Rgb.Clamp01(alpha);
        if (alpha <= 0)
            return;
        switch (mode)
        {
            case BlendMode.Htp:
                var t = top.Light * alpha;
                Light = new Rgb(Math.Max(Light.R, t.R), Math.Max(Light.G, t.G), Math.Max(Light.B, t.B));
                if (top.White is { } tw)
                    White = Math.Max(White ?? 0, tw * alpha);
                break;
            case BlendMode.Add:
                Light += top.Light * alpha;
                if (top.White is { } aw)
                    White = (White ?? 0) + aw * alpha;
                break;
            default:
                Light = Rgb.Lerp(Light, top.Light, alpha);
                if (top.White is not null || White is not null)
                    White = (White ?? 0) + ((top.White ?? 0) - (White ?? 0)) * alpha;
                break;
        }
        // Positional and special channels have no "darker" value to blend through, so the upper layer wins
        // as soon as it is visible at all; only lerp when both layers define the channel.
        for (var i = 0; i < top._roleCount; i++)
        {
            var role = top._roleOrder[i];
            var value = top._roles[role];
            SetRole((ChannelRole)role, (_roleMask & (1u << role)) != 0 ? _roles[role] + (value - _roles[role]) * alpha : value);
        }
        for (var i = 0; i < top._rawCount; i++)
        {
            var (offset, value) = top._raw[i];
            SetRaw(offset, TryGetRaw(offset, out var below) ? below + (value - below) * alpha : value);
        }
    }
}
