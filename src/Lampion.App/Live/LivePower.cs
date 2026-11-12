using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Lampion.App.Live;

/// <summary>
/// For the length of a live session: keeps Windows from sleeping or turning the screen off, and runs the process at
/// high priority so other programs (a browser, an updater) can't preempt the engine, the DMX sender or the audio.
/// Create and dispose on the UI thread: Windows ties the stay-awake request to the thread that made it.
/// </summary>
internal sealed class LivePower : IDisposable
{
    private const uint Continuous = 0x80000000;
    private const uint SystemRequired = 0x00000001;
    private const uint DisplayRequired = 0x00000002;

    private readonly ProcessPriorityClass? _previous;

    /// <summary>Raises the priority and asks the OS to stay awake. Failures (no permission) are ignored: they only cost headroom.</summary>
    public LivePower()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            _previous = process.PriorityClass;
            process.PriorityClass = ProcessPriorityClass.High;
        }
        catch (Exception ex) when (ex is Win32Exception or PlatformNotSupportedException or InvalidOperationException)
        {
            _previous = null;
        }
        if (OperatingSystem.IsWindows())
            SetThreadExecutionState(Continuous | SystemRequired | DisplayRequired);
    }

    /// <summary>Restores the priority and lets the OS sleep again.</summary>
    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
            SetThreadExecutionState(Continuous);
        if (_previous is not { } previous)
            return;
        try
        {
            using var process = Process.GetCurrentProcess();
            process.PriorityClass = previous;
        }
        catch (Exception ex) when (ex is Win32Exception or PlatformNotSupportedException or InvalidOperationException)
        {
            Trace.TraceWarning("Couldn't restore the process priority: " + ex.Message);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);
}
