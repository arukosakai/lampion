namespace Lampion.Core.Midi;

/// <summary>How an APC mini mk2 grid pad is lit.</summary>
public enum PadGlow
{
    /// <summary>25% brightness: assigned, but off.</summary>
    Dim,

    /// <summary>Full brightness.</summary>
    Full,

    /// <summary>Blinking, e.g. while a show loads.</summary>
    Blink,
}
