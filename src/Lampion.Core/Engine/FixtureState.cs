using Lampion.Core.Color;
using Lampion.Core.Model;

namespace Lampion.Core.Engine;

/// <summary>
/// What a fixture should do at one instant, before it is mapped to channels. <see cref="Light"/> is
/// premultiplied (colour × intensity), so blending and fading behave like real light.
/// </summary>
public sealed class FixtureState
{
    public Rgb Light;

    /// <summary>Explicit white level, or null to let the mapper derive white from <see cref="Light"/>.</summary>
    public double? White;

    public Dictionary<ChannelRole, double>? Roles;

    /// <summary>Keyed by 0-based channel offset within the fixture; wins over everything else.</summary>
    public Dictionary<int, double>? Raw;

    public void SetRole(ChannelRole role, double value) => (Roles ??= [])[role] = value;

    public void SetRaw(int offset, double value) => (Raw ??= [])[offset] = value;

    public FixtureState Copy() =>
        new()
        {
            Light = Light,
            White = White,
            Roles = Roles is null ? null : new(Roles),
            Raw = Raw is null ? null : new(Raw),
        };

    public void Scale(double k)
    {
        Light *= k;
        if (White is { } w)
            White = w * k;
    }

    /// <summary>Puts <paramref name="top"/> over this state with weight <paramref name="alpha"/>.</summary>
    public void Blend(FixtureState top, double alpha, BlendMode mode)
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
        if (top.Roles is not null)
            foreach (var (role, value) in top.Roles)
                SetRole(role, Roles is not null && Roles.TryGetValue(role, out var below) ? below + (value - below) * alpha : value);
        if (top.Raw is not null)
            foreach (var (offset, value) in top.Raw)
                SetRaw(offset, Raw is not null && Raw.TryGetValue(offset, out var below) ? below + (value - below) * alpha : value);
    }
}

/// <summary>A fixture state with the coverage it has within its track.</summary>
internal readonly record struct Layer(FixtureState State, double Alpha);
