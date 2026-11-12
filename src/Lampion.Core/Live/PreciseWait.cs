using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Lampion.Core.Live;

/// <summary>
/// Sleeps until a <see cref="Stopwatch"/> timestamp with sub-millisecond accuracy. <see cref="Thread.Sleep(int)"/>
/// alone rounds up to the OS timer tick (15.6 ms on Windows by default), which is most of a frame; this uses a
/// high-resolution waitable timer on Windows and finishes the last stretch with a short spin everywhere.
/// One instance per thread.
/// </summary>
internal sealed class PreciseWait : IDisposable
{
    private const uint HighResolution = 0x2;
    private const uint TimerAllAccess = 0x1F0003;
    private const uint Infinite = 0xFFFFFFFF;

    // Spinning only covers the timer's own slop, so a frame costs well under a millisecond of CPU waiting.
    private static readonly long SpinWindow = Stopwatch.Frequency / 2000;
    private readonly IntPtr _timer;

    /// <summary>A waiter for the current thread.</summary>
    public PreciseWait()
    {
        if (OperatingSystem.IsWindows())
            _timer = CreateWaitableTimerExW(IntPtr.Zero, null, HighResolution, TimerAllAccess);
    }

    /// <summary>Returns at <paramref name="due"/> (a <see cref="Stopwatch.GetTimestamp"/> value), or at once if it's past, or when <paramref name="stop"/> is set.</summary>
    public void Until(long due, ref bool stop)
    {
        while (!Volatile.Read(ref stop))
        {
            var left = due - Stopwatch.GetTimestamp();
            if (left <= 0)
                return;
            if (left <= SpinWindow)
                Thread.SpinWait(20);
            else
                Block(left - SpinWindow);
        }
    }

    /// <summary>Closes the timer.</summary>
    public void Dispose()
    {
        if (_timer != IntPtr.Zero)
            CloseHandle(_timer);
    }

    private void Block(long ticks)
    {
        if (_timer == IntPtr.Zero)
        {
            Thread.Sleep(1);
            return;
        }
        // Relative due times are negative, in 100 ns units.
        var relative = -(long)(ticks * 10_000_000.0 / Stopwatch.Frequency);
        if (SetWaitableTimer(_timer, ref relative, 0, IntPtr.Zero, IntPtr.Zero, false))
            WaitForSingleObject(_timer, Infinite);
        else
            Thread.Sleep(1);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWaitableTimerExW(IntPtr attributes, string? name, uint flags, uint access);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWaitableTimer(IntPtr timer, ref long dueTime, int period, IntPtr completion, IntPtr argument, [MarshalAs(UnmanagedType.Bool)] bool resume);

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
