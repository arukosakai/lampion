using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Tests;

public class TapTempoTests
{
    [Fact]
    public void FirstTapHasNoTempoAndSecondGivesIt()
    {
        var tap = new TapTempo();
        Assert.Null(tap.Tap(10));
        Assert.Equal(120, tap.Tap(10.5)!.Value, 6);
    }

    [Fact]
    public void AveragesTheIntervalsOfTheRun()
    {
        var tap = new TapTempo();
        tap.Tap(0);
        tap.Tap(0.45);
        tap.Tap(1.05);
        Assert.Equal(60 / 0.525, tap.Tap(1.575)!.Value, 6);
    }

    [Fact]
    public void LongPauseStartsANewRun()
    {
        var tap = new TapTempo();
        tap.Tap(0);
        tap.Tap(1);
        Assert.Null(tap.Tap(1 + TapTempo.MaxGap + 0.1));
        Assert.Equal(1, tap.Count);
        Assert.Equal(128, tap.Tap(3.1 + 60 / 128.0)!.Value, 6);
    }

    [Fact]
    public void ClockGoingBackwardsStartsANewRun()
    {
        var tap = new TapTempo();
        tap.Tap(5);
        tap.Tap(5.5);
        Assert.Null(tap.Tap(5.2));
    }

    [Fact]
    public void OnlyTheLatestWindowCounts()
    {
        var tap = new TapTempo();
        var t = 0.0;
        for (var i = 0; i < TapTempo.Window; i++, t += 1)
            tap.Tap(t);
        double? bpm = null;
        for (var i = 0; i < TapTempo.Window; i++, t += 0.5)
            bpm = tap.Tap(t);
        Assert.Equal(TapTempo.Window, tap.Count);
        Assert.Equal(120, bpm!.Value, 6);
    }
}
