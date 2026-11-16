using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Tests;

public class TimecodeParserTests
{
    [Theory]
    [InlineData("12", 12)]
    [InlineData("12.5", 12.5)]
    [InlineData("12,5", 12.5)]
    [InlineData("12s", 12)]
    [InlineData(" 1:30 ", 90)]
    [InlineData("1:02:03", 3723)]
    [InlineData("00:01:05:15", 65.5)]
    [InlineData("0:0:0:0", 0)]
    public void ReadsAbsoluteTimes(string text, double expected)
    {
        Assert.True(TimecodeParser.TryParse(text, current: 40, fps: 30, out var seconds));
        Assert.Equal(expected, seconds, 6);
    }

    [Theory]
    [InlineData("+5", 45)]
    [InlineData("-2.5", 37.5)]
    [InlineData("+0:10", 50)]
    [InlineData("-1:00", 0)]
    public void SignedTimesMoveFromThePlayheadAndStopAtZero(string text, double expected)
    {
        Assert.True(TimecodeParser.TryParse(text, current: 40, fps: 30, out var seconds));
        Assert.Equal(expected, seconds, 6);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1:")]
    [InlineData("1.5:00")]
    [InlineData("1:2:3:4:5")]
    [InlineData("--3")]
    public void RejectsWhatIsNotATime(string? text)
    {
        Assert.False(TimecodeParser.TryParse(text, current: 40, fps: 30, out var seconds));
        Assert.Equal(0, seconds);
    }

    [Fact]
    public void FramesFollowTheFrameRate()
    {
        Assert.True(TimecodeParser.TryParse("00:00:01:12", 0, fps: 24, out var seconds));
        Assert.Equal(1.5, seconds, 6);
    }
}
