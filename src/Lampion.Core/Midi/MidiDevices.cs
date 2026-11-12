namespace Lampion.Core.Midi;

/// <summary>
/// Lists and opens MIDI devices. Windows only for now (WinMM); elsewhere the lists are empty and
/// <see cref="Supported"/> is false, so the UI can say so instead of showing nothing.
/// </summary>
public static class MidiDevices
{
    /// <summary>True when this OS has a MIDI backend.</summary>
    public static bool Supported => OperatingSystem.IsWindows();

    /// <summary>Input device names; a device's position is what <see cref="OpenInput"/> takes.</summary>
    public static IReadOnlyList<string> InputNames() => OperatingSystem.IsWindows() ? WinMm.InputNames() : [];

    /// <summary>Output device names; a device's position is what <see cref="OpenOutput"/> takes.</summary>
    public static IReadOnlyList<string> OutputNames() => OperatingSystem.IsWindows() ? WinMm.OutputNames() : [];

    /// <summary>Opens input <paramref name="index"/> and starts delivering its messages to <paramref name="handler"/>.</summary>
    /// <exception cref="IOException">The device is missing or in use.</exception>
    /// <exception cref="PlatformNotSupportedException">Not on Windows.</exception>
    public static IMidiInput OpenInput(int index, MidiHandler handler) =>
        OperatingSystem.IsWindows() ? new WinMmMidiInput(index, handler) : throw new PlatformNotSupportedException("MIDI input needs Windows for now.");

    /// <summary>Opens output <paramref name="index"/>.</summary>
    /// <exception cref="IOException">The device is missing or in use.</exception>
    /// <exception cref="PlatformNotSupportedException">Not on Windows.</exception>
    public static IMidiOutput OpenOutput(int index) =>
        OperatingSystem.IsWindows() ? new WinMmMidiOutput(index) : throw new PlatformNotSupportedException("MIDI output needs Windows for now.");
}
