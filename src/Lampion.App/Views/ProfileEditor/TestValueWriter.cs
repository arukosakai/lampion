using Lampion.Core.Color;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>
/// Writes a function (a role) into test values: every channel of that role gets the value, as a console
/// would. Values past the end of the profile, or channels past the end of the values, are skipped.
/// </summary>
internal static class TestValueWriter
{
    /// <summary>Sets every channel of <paramref name="role"/> to <paramref name="value"/>, DMX 0–255.</summary>
    public static void Write(FixtureProfile profile, Span<byte> values, ChannelRole role, byte value)
    {
        var channels = profile.Channels;
        for (var i = 0; i < channels.Count && i < values.Length; i++)
            if (channels[i].Role == role)
                values[i] = value;
    }

    /// <summary>Sets every channel of <paramref name="role"/> to a 0..1 <paramref name="level"/> (clamped).</summary>
    public static void WriteLevel(FixtureProfile profile, Span<byte> values, ChannelRole role, double level) => Write(profile, values, role, Rgb.ToByte(level));

    /// <summary>
    /// Sets a 0..1 position (clamped) as 16 bits across <paramref name="coarse"/> and <paramref name="fine"/>
    /// when the profile has the fine channel, else as 8 bits on the coarse one.
    /// </summary>
    public static void WriteWide(FixtureProfile profile, Span<byte> values, ChannelRole coarse, ChannelRole fine, double value)
    {
        var wide = (int)Math.Round(Math.Clamp(value, 0, 1) * 65535);
        var hasFine = profile.Has(fine);
        var channels = profile.Channels;
        for (var i = 0; i < channels.Count && i < values.Length; i++)
        {
            if (channels[i].Role == coarse)
                values[i] = hasFine ? (byte)(wide >> 8) : (byte)Math.Round(value * 255);
            else if (channels[i].Role == fine)
                values[i] = (byte)(wide & 0xFF);
        }
    }
}
