using Lampion.Core.Model;

namespace Lampion.Core.Tests;

public class StageMathTests
{
    [Fact]
    public void AimAtPointsTheBeamAtTheTarget()
    {
        var fixture = new Fixture { Position = new Vec3(2, 6, 2) };
        var target = new Vec3(-1, 1, -2);
        StageMath.AimAt(fixture, target);
        var direction = StageMath.Direction(fixture.Yaw, fixture.Pitch);
        var expected = (target - fixture.Position).Normalized();
        Assert.Equal(expected.X, direction.X, 3);
        Assert.Equal(expected.Y, direction.Y, 3);
        Assert.Equal(expected.Z, direction.Z, 3);
    }

    private static FixtureProfile MovingHead() =>
        new() { PanRange = 540, TiltRange = 270, Channels = [new() { Role = ChannelRole.Pan }, new() { Role = ChannelRole.Tilt }] };

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3, 0, -2)]
    [InlineData(-4, 1.5, 3)]
    [InlineData(1, 6, -5)]
    public void PanTiltTowardPointsTheBeamAtTheTarget(double x, double y, double z)
    {
        var profile = MovingHead();
        var fixture = new Fixture { Position = new Vec3(0.5, 5, 0.5), Yaw = 180, Pitch = -90 };
        var target = new Vec3(x, y, z);
        var (pan, tilt) = StageMath.PanTiltToward(fixture, profile, target, (0.5, 0.5))!.Value;
        var direction = StageMath.BeamDirection(fixture, profile, pan, tilt);
        var expected = (target - fixture.Position).Normalized();
        Assert.Equal(expected.X, direction.X, 3);
        Assert.Equal(expected.Y, direction.Y, 3);
        Assert.Equal(expected.Z, direction.Z, 3);
    }

    [Fact]
    public void PanTiltTowardStaysNearTheCurrentPose()
    {
        var profile = MovingHead();
        var fixture = new Fixture { Position = new Vec3(0, 5, 0), Yaw = 0, Pitch = -90 };
        // Two ways to reach a spot ahead: tilt forward, or pan round 180° and tilt back.
        var forward = StageMath.PanTiltToward(fixture, profile, new Vec3(0, 0, 3), (0.5, 0.6))!.Value;
        var flipped = StageMath.PanTiltToward(fixture, profile, new Vec3(0, 0, 3), (0.5 + 180.0 / 540, 0.4))!.Value;
        Assert.True(forward.Tilt > 0.5);
        Assert.True(flipped.Tilt < 0.5);
        Assert.NotEqual(forward.Pan, flipped.Pan, 3);
    }

    [Fact]
    public void DistributeCentresFixturesUnderTheTruss()
    {
        var truss = new StageObject { Kind = StageObjectKind.Truss, Position = new Vec3(0, 6, 1), Size = new Vec3(8, 0.3, 0.3) };
        var fixtures = Enumerable.Range(0, 4).Select(_ => new Fixture()).ToList();
        StageMath.DistributeAlong(truss, fixtures, drop: 0.35);
        Assert.Equal([-3, -1, 1, 3], fixtures.Select(f => f.Position.X).ToArray());
        Assert.All(fixtures, f => Assert.Equal(5.5, f.Position.Y, 6));
        Assert.All(fixtures, f => Assert.Equal(1, f.Position.Z, 6));
    }

    [Fact]
    public void RaycastHitsTheDeckBeforeTheFloor()
    {
        var deck = new StageObject { Kind = StageObjectKind.Platform, Position = new Vec3(0, 0.5, 0), Size = new Vec3(4, 1, 4) };
        Assert.Equal(5, StageMath.Raycast([deck], new Vec3(0, 6, 0), new Vec3(0, -1, 0))!.Value, 6);
        Assert.Equal(6, StageMath.Raycast([deck], new Vec3(10, 6, 0), new Vec3(0, -1, 0))!.Value, 6);
    }
}
