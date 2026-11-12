using System.Diagnostics;
using Lampion.Core.Live;

namespace Lampion.Core.Tests;

public class ShowClockTests
{
    private static long At(double seconds) => (long)(seconds * Stopwatch.Frequency);

    [Fact]
    public void HoldsUntilStarted()
    {
        var clock = new ShowClock();
        clock.Hold(2);

        Assert.False(clock.IsRunning);
        Assert.Equal(2, clock.TimeAt(At(100)));
    }

    [Fact]
    public void RunsAtNormalSpeedOnceStarted()
    {
        var clock = new ShowClock();
        clock.Start(At(10), 1);

        Assert.Equal(4, clock.TimeAt(At(13)), 9);
    }

    [Fact]
    public void FirstMusicReportStartsAHoldingClock()
    {
        var clock = new ShowClock();
        clock.Sync(At(5), 0.02);

        Assert.True(clock.IsRunning);
        Assert.Equal(1.02, clock.TimeAt(At(6)), 9);
    }

    [Fact]
    public void SmallErrorsBendTheSpeedWithoutAStep()
    {
        var clock = new ShowClock();
        clock.Start(At(0), 0);

        var error = clock.Sync(At(10), 10.1);

        Assert.Equal(0.1, error, 9);
        Assert.Equal(10, clock.TimeAt(At(10)), 9);
        Assert.Equal(11 + ShowClock.MaxSlew, clock.TimeAt(At(11)), 9);
    }

    [Fact]
    public void LargeErrorsSnap()
    {
        var clock = new ShowClock();
        clock.Start(At(0), 0);

        clock.Sync(At(10), 12);

        Assert.Equal(12, clock.TimeAt(At(10)), 9);
    }

    [Fact]
    public void ConvergesOnNoisyMusicReportsWithoutVisibleSteps()
    {
        // Lights start 200 ms behind; the player reports four times a second with ±15 ms of jitter.
        var clock = new ShowClock();
        clock.Start(At(0), -0.2);
        var random = new Random(7);
        var previous = clock.TimeAt(0);
        var worstStep = 0.0;
        for (var frame = 1; frame <= 1000; frame++)
        {
            var t = frame / 100.0;
            if (frame % 25 == 0)
                clock.Sync(At(t), t + (random.NextDouble() - 0.5) * 0.03);
            var now = clock.TimeAt(At(t));
            worstStep = Math.Max(worstStep, Math.Abs(now - previous - 0.01));
            previous = now;
        }

        Assert.True(Math.Abs(clock.TimeAt(At(10)) - 10) < 0.02, $"still {clock.TimeAt(At(10)) - 10:0.000} s off");
        Assert.True(worstStep < 0.001, $"a frame stepped by {worstStep * 1000:0.00} ms more than it should");
    }
}
