using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Lampion.Core.Midi;

/// <summary>The bits of the Windows multimedia MIDI API Lampion uses.</summary>
[SupportedOSPlatform("windows")]
internal static class WinMm
{
    /// <summary><c>CALLBACK_FUNCTION</c> for <see cref="midiInOpen"/>.</summary>
    public const uint CallbackFunction = 0x30000;

    /// <summary><c>MIM_DATA</c>: a short message arrived.</summary>
    public const uint MimData = 0x3C3;

    /// <summary>The driver's input callback.</summary>
    public delegate void MidiInProc(IntPtr handle, uint message, IntPtr instance, IntPtr param1, IntPtr param2);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MidiInCaps
    {
        public ushort Mid;
        public ushort Pid;
        public uint DriverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Name;
        public uint Support;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MidiOutCaps
    {
        public ushort Mid;
        public ushort Pid;
        public uint DriverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Name;
        public ushort Technology;
        public ushort Voices;
        public ushort Notes;
        public ushort ChannelMask;
        public uint Support;
    }

    [DllImport("winmm.dll")]
    private static extern uint midiInGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "midiInGetDevCapsW")]
    private static extern int midiInGetDevCaps(UIntPtr id, ref MidiInCaps caps, uint size);

    [DllImport("winmm.dll")]
    private static extern uint midiOutGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "midiOutGetDevCapsW")]
    private static extern int midiOutGetDevCaps(UIntPtr id, ref MidiOutCaps caps, uint size);

    [DllImport("winmm.dll")]
    public static extern int midiInOpen(out IntPtr handle, uint id, MidiInProc callback, IntPtr instance, uint flags);

    [DllImport("winmm.dll")]
    public static extern int midiInStart(IntPtr handle);

    [DllImport("winmm.dll")]
    public static extern int midiInStop(IntPtr handle);

    [DllImport("winmm.dll")]
    public static extern int midiInReset(IntPtr handle);

    [DllImport("winmm.dll")]
    public static extern int midiInClose(IntPtr handle);

    [DllImport("winmm.dll")]
    public static extern int midiOutOpen(out IntPtr handle, uint id, IntPtr callback, IntPtr instance, uint flags);

    [DllImport("winmm.dll")]
    public static extern int midiOutShortMsg(IntPtr handle, uint message);

    [DllImport("winmm.dll")]
    public static extern int midiOutReset(IntPtr handle);

    [DllImport("winmm.dll")]
    public static extern int midiOutClose(IntPtr handle);

    /// <summary>Every input device's name, by device id.</summary>
    public static List<string> InputNames()
    {
        var names = new List<string>();
        var count = midiInGetNumDevs();
        for (uint i = 0; i < count; i++)
        {
            var caps = new MidiInCaps();
            names.Add(midiInGetDevCaps(i, ref caps, (uint)Marshal.SizeOf<MidiInCaps>()) == 0 ? caps.Name : $"MIDI input {i + 1}");
        }
        return names;
    }

    /// <summary>Every output device's name, by device id.</summary>
    public static List<string> OutputNames()
    {
        var names = new List<string>();
        var count = midiOutGetNumDevs();
        for (uint i = 0; i < count; i++)
        {
            var caps = new MidiOutCaps();
            names.Add(midiOutGetDevCaps(i, ref caps, (uint)Marshal.SizeOf<MidiOutCaps>()) == 0 ? caps.Name : $"MIDI output {i + 1}");
        }
        return names;
    }

    /// <summary>Throws when <paramref name="result"/> is an error, naming the device and what failed.</summary>
    /// <exception cref="IOException"><paramref name="result"/> is not 0.</exception>
    public static void Check(int result, string action, string device)
    {
        if (result != 0)
            throw new IOException($"Couldn't {action} {device} (MMRESULT {result}); is another program using it?");
    }
}
