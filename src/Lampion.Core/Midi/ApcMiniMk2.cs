using Lampion.Core.Color;

namespace Lampion.Core.Midi;

/// <summary>
/// The Akai APC mini mk2 (communications protocol v1.0), on its first port: an 8×8 RGB pad grid, 9 faders, 8 red track
/// buttons along the bottom, 8 green scene buttons down the right and Shift. Everything is channel 1; pad LEDs take a
/// palette colour as velocity and their brightness or blink rate as the note-on channel.
/// </summary>
public static class ApcMiniMk2
{
    /// <summary>The device name of the controller's first port, the one Lampion uses.</summary>
    public const string DeviceName = "APC mini mk2";

    /// <summary>Note of the bottom-left pad; the grid counts up left to right, then bottom to top.</summary>
    public const int FirstPad = 0x00;

    /// <summary>Note of the top-right pad.</summary>
    public const int LastPad = 0x3F;

    /// <summary>Note of the leftmost track button; the other seven follow.</summary>
    public const int FirstTrack = 0x64;

    /// <summary>Note of the top scene button; the other seven follow downwards.</summary>
    public const int FirstScene = 0x70;

    /// <summary>The Shift button's note. It has no LED.</summary>
    public const int Shift = 0x7A;

    /// <summary>Controller number of fader 1; faders 2–8 and the master (9) follow.</summary>
    public const int FirstFader = 0x30;

    /// <summary>Pads per row and rows in the grid.</summary>
    public const int GridSize = 8;

    /// <summary>Faders, the master included.</summary>
    public const int Faders = 9;

    // Note-on channel (0-based) per light: 25% solid, 100% solid, blinking in 1/4 notes of the device's own clock.
    private const int DimChannel = 1;
    private const int FullChannel = 6;
    private const int BlinkChannel = 14;

    // The fixed velocity → colour table from the protocol document.
    private static readonly int[] Palette =
    [
        0x000000, 0x1E1E1E, 0x7F7F7F, 0xFFFFFF, 0xFF4C4C, 0xFF0000, 0x590000, 0x190000,
        0xFFBD6C, 0xFF5400, 0x591D00, 0x271B00, 0xFFFF4C, 0xFFFF00, 0x595900, 0x191900,
        0x88FF4C, 0x54FF00, 0x1D5900, 0x142B00, 0x4CFF4C, 0x00FF00, 0x005900, 0x001900,
        0x4CFF5E, 0x00FF19, 0x00590D, 0x001902, 0x4CFF88, 0x00FF55, 0x00591D, 0x001F12,
        0x4CFFB7, 0x00FF99, 0x005935, 0x001912, 0x4CC3FF, 0x00A9FF, 0x004152, 0x001019,
        0x4C88FF, 0x0055FF, 0x001D59, 0x000819, 0x4C4CFF, 0x0000FF, 0x000059, 0x000019,
        0x874CFF, 0x5400FF, 0x190064, 0x0F0030, 0xFF4CFF, 0xFF00FF, 0x590059, 0x190019,
        0xFF4C87, 0xFF0054, 0x59001D, 0x220013, 0xFF1500, 0x993500, 0x795100, 0x436400,
        0x033900, 0x005735, 0x00547F, 0x0000FF, 0x00454F, 0x2500CC, 0x7F7F7F, 0x202020,
        0xFF0000, 0xBDFF2D, 0xAFED06, 0x64FF09, 0x108B00, 0x00FF87, 0x00A9FF, 0x002AFF,
        0x3F00FF, 0x7A00FF, 0xB21A7D, 0x402100, 0xFF4A00, 0x88E106, 0x72FF15, 0x00FF00,
        0x3BFF26, 0x59FF71, 0x38FFCC, 0x5B8AFF, 0x3151C6, 0x877FE9, 0xD31DFF, 0xFF005D,
        0xFF7F00, 0xB9B000, 0x90FF00, 0x835D07, 0x392B00, 0x144C10, 0x0D5038, 0x15152A,
        0x16205A, 0x693C1C, 0xA8000A, 0xDE513D, 0xD86A1C, 0xFFE126, 0x9EE12F, 0x67B50F,
        0x1E1E30, 0xDCFF6B, 0x80FFBD, 0x9A99FF, 0x8E66FF, 0x404040, 0x757575, 0xE0FFFF,
        0xA00000, 0x350000, 0x1AD000, 0x074200, 0xB9B000, 0x3F3100, 0xB35F00, 0x4B1502,
    ];

    /// <summary>Palette index of pure white.</summary>
    public const int White = 3;

    /// <summary>Palette index of pure green.</summary>
    public const int Green = 21;

    /// <summary>
    /// True for the controller's first port. Windows numbers extra units ("2- APC mini mk2"); the second port
    /// ("MIDIIN2 (APC mini mk2)") is its note mode, which Lampion doesn't use.
    /// </summary>
    public static bool Matches(string? deviceName) =>
        deviceName is not null && deviceName.EndsWith(DeviceName, StringComparison.OrdinalIgnoreCase) && !deviceName.Contains('(');

    /// <summary>The first name in <paramref name="deviceNames"/> that <see cref="Matches"/>, by position; −1 when none does.</summary>
    public static int Find(IReadOnlyList<string> deviceNames)
    {
        for (var i = 0; i < deviceNames.Count; i++)
            if (Matches(deviceNames[i]))
                return i;
        return -1;
    }

    /// <summary>The pad at <paramref name="row"/> (0 = top) and <paramref name="column"/> (0 = left), each 0–7.</summary>
    public static int PadNote(int row, int column) => (GridSize - 1 - row) * GridSize + column;

    /// <summary>True for the notes of the RGB grid.</summary>
    public static bool IsPad(int note) => note is >= FirstPad and <= LastPad;

    /// <summary>True for the single-colour track and scene buttons.</summary>
    public static bool IsButton(int note) => note is >= FirstTrack and < FirstTrack + GridSize or >= FirstScene and < FirstScene + GridSize;

    /// <summary>Lights grid pad <paramref name="note"/> in palette colour <paramref name="colour"/>.</summary>
    public static MidiMessage PadLight(int note, PadGlow glow, int colour)
    {
        var channel = glow switch
        {
            PadGlow.Dim => DimChannel,
            PadGlow.Blink => BlinkChannel,
            _ => FullChannel,
        };
        return new MidiMessage((byte)(0x90 | channel), (byte)(note & 0x7F), (byte)(colour & 0x7F));
    }

    /// <summary>Lights a track or scene button (red or green, the hardware decides).</summary>
    public static MidiMessage ButtonLight(int note, bool on, bool blink = false) =>
        new(0x90, (byte)(note & 0x7F), (byte)(blink ? 2 : on ? 1 : 0));

    /// <summary>Switches LED <paramref name="note"/> off, pad or button.</summary>
    public static MidiMessage Off(int note) => new(0x90, (byte)(note & 0x7F), 0);

    /// <summary>
    /// The palette entry closest in hue and saturation to <paramref name="hex"/> ("#RRGGBB"). Brightness comes from the
    /// note-on channel, so the colour is compared at full brightness and the brightest matching entry wins; black gives white.
    /// </summary>
    public static int NearestColour(string? hex)
    {
        var c = Rgb.FromHex(hex);
        var peak = Math.Max(c.R, Math.Max(c.G, c.B));
        if (peak <= 0)
            return White;
        var (r, g, b) = (c.R / peak, c.G / peak, c.B / peak);
        var best = White;
        var bestDistance = double.MaxValue;
        for (var i = 1; i < Palette.Length; i++)
        {
            var (pr, pg, pb) = Channels(Palette[i]);
            var max = Math.Max(pr, Math.Max(pg, pb));
            // Dim duplicates of a hue lose ties to the bright one, which reads better on stage.
            var distance = Sq(pr / max - r) + Sq(pg / max - g) + Sq(pb / max - b) - max * 1e-6;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }
        return best;
    }

    private static (double R, double G, double B) Channels(int rgb) => ((rgb >> 16 & 0xFF) / 255.0, (rgb >> 8 & 0xFF) / 255.0, (rgb & 0xFF) / 255.0);

    private static double Sq(double v) => v * v;
}
