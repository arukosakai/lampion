using Lampion.Core.Live;
using Lampion.Core.Model.Live;

namespace Lampion.Core.Tests;

public class PadEdgesTests
{
    private readonly LiveControls _controls = new();
    private readonly PadEdges _edges = new();

    [Fact]
    public void NothingHappensUntilAPress()
    {
        Assert.Equal(PadEdge.None, _edges.Poll(_controls, 0));
    }

    [Fact]
    public void ToggleOnThenOff()
    {
        _controls.PressPad(0, PadMode.Toggle);
        Assert.Equal(PadEdge.On, _edges.Poll(_controls, 0));
        Assert.Equal(PadEdge.None, _edges.Poll(_controls, 0));

        _controls.PressPad(0, PadMode.Toggle);
        Assert.Equal(PadEdge.Off, _edges.Poll(_controls, 0));
    }

    [Fact]
    public void ATapBetweenTwoPollsStillCountsAsOn()
    {
        _controls.PressPad(3, PadMode.Flash);
        _controls.ReleasePad(3, PadMode.Flash);

        Assert.False(_controls.IsPadOn(3));
        Assert.Equal(PadEdge.On, _edges.Poll(_controls, 3));
    }

    [Fact]
    public void AnOffOnRetriggerOfALitPadCountsAsOn()
    {
        _controls.PressPad(0, PadMode.Toggle);
        _edges.Poll(_controls, 0);

        _controls.PressPad(0, PadMode.Toggle);
        _controls.PressPad(0, PadMode.Toggle);

        Assert.Equal(PadEdge.On, _edges.Poll(_controls, 0));
    }

    [Fact]
    public void SkippedChangesAreNotPresses()
    {
        _controls.SetPad(1, on: true);
        _edges.Skip(_controls);

        Assert.Equal(PadEdge.None, _edges.Poll(_controls, 1));
    }

    [Fact]
    public void PadsOutOfRangeNeverFire()
    {
        Assert.Equal(PadEdge.None, _edges.Poll(_controls, LiveControls.MaxPads));
        Assert.Equal(PadEdge.None, _edges.Poll(_controls, -1));
    }
}
