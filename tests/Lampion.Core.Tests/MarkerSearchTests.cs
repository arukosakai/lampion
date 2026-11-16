using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Tests;

public class MarkerSearchTests
{
    // Deliberately out of time order: markers stay in the order they were added.
    private static readonly List<Marker> Markers =
    [
        new() { Time = 10, Label = "B" },
        new() { Time = 2, Label = "A" },
        new() { Time = 25, Label = "C" },
    ];

    [Theory]
    [InlineData(2.3, 0.5, "A")]
    [InlineData(9.6, 0.5, "B")]
    [InlineData(24, 2, "C")]
    public void NearestFindsTheClosestMarkerInReach(double time, double tolerance, string expected) =>
        Assert.Equal(expected, MarkerSearch.Nearest(Markers, time, tolerance)?.Label);

    [Theory]
    [InlineData(5, 0.5)]
    [InlineData(10.5, 0.5)]
    [InlineData(10, 0)]
    public void NearestIsNullOutOfReach(double time, double tolerance) =>
        Assert.Null(MarkerSearch.Nearest(Markers, time, tolerance));

    [Fact]
    public void NearestPicksTheCloserOfTwo()
    {
        List<Marker> close = [new() { Time = 4, Label = "far" }, new() { Time = 5, Label = "near" }];
        Assert.Equal("near", MarkerSearch.Nearest(close, 4.8, 2)?.Label);
    }

    [Theory]
    [InlineData(0, "A")]
    [InlineData(2, "B")]
    [InlineData(10, "C")]
    [InlineData(25, null)]
    public void NextSkipsTheMarkerUnderThePlayhead(double time, string? expected) =>
        Assert.Equal(expected, MarkerSearch.Next(Markers, time)?.Label);

    [Theory]
    [InlineData(30, "C")]
    [InlineData(25, "B")]
    [InlineData(10, "A")]
    [InlineData(2, null)]
    public void PreviousSkipsTheMarkerUnderThePlayhead(double time, string? expected) =>
        Assert.Equal(expected, MarkerSearch.Previous(Markers, time)?.Label);

    [Fact]
    public void NoMarkersFindsNothing()
    {
        Assert.Null(MarkerSearch.Nearest([], 1, 10));
        Assert.Null(MarkerSearch.Next([], 1));
        Assert.Null(MarkerSearch.Previous([], 1));
    }
}
