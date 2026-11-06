using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;

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
        new() { PanRange = 540, TiltRange = 270, Channels = { new FixtureChannel { Role = ChannelRole.Pan }, new FixtureChannel { Role = ChannelRole.Tilt } } };

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

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(30, 0, 0)]
    [InlineData(0, 25, 0)]
    [InlineData(0, 0, 90)]
    [InlineData(-40, 15, 70)]
    public void ObjectRotationMatchesTheRenderer(double yaw, double pitch, double roll)
    {
        var obj = new StageObject { Position = new Vec3(1, 2, 3), Yaw = yaw, Pitch = pitch, Roll = roll };
        var rotation = System.Numerics.Quaternion.CreateFromYawPitchRoll((float)(yaw * Math.PI / 180), (float)(pitch * Math.PI / 180), (float)(roll * Math.PI / 180));
        var local = new Vec3(0.7, -1.3, 2.1);
        var expected = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(0.7f, -1.3f, 2.1f), rotation);
        var actual = StageMath.LocalToWorld(obj, local) - obj.Position;
        Assert.Equal(expected.X, actual.X, 4);
        Assert.Equal(expected.Y, actual.Y, 4);
        Assert.Equal(expected.Z, actual.Z, 4);
    }

    [Fact]
    public void RolledTrussStandsUpright()
    {
        var tower = new StageObject { Kind = StageObjectKind.Truss, Position = new Vec3(0, 2.5, 0), Size = new Vec3(5, 0.3, 0.3), Roll = 90 };
        Assert.Equal(2.5, StageMath.HalfHeight(tower), 6);
        Assert.Equal(5, StageMath.LocalToWorld(tower, new Vec3(2.5, 0, 0)).Y, 6);
        // A sideways ray at 4 m only hits it if it really stands upright.
        Assert.Equal(1.85, StageMath.RayBox(tower, new Vec3(-2, 4, 0), new Vec3(1, 0, 0))!.Value, 6);
        Assert.Null(StageMath.RayBox(tower, new Vec3(-2, 5.5, 0), new Vec3(1, 0, 0)));
    }

    [Fact]
    public void HalfExtentsFollowRotation()
    {
        var bar = new StageObject { Position = new Vec3(1, 2, 3), Size = new Vec3(4, 0.3, 0.2) };
        Assert.Equal(new Vec3(2, 0.15, 0.1), StageMath.HalfExtents(bar));

        bar.Yaw = 90;
        var turned = StageMath.HalfExtents(bar);
        Assert.Equal(0.1, turned.X, 6);
        Assert.Equal(0.15, turned.Y, 6);
        Assert.Equal(2, turned.Z, 6);

        bar.Yaw = 45;
        var diagonal = StageMath.HalfExtents(bar);
        Assert.Equal((2 + 0.1) * Math.Sqrt(0.5), diagonal.X, 6);
        Assert.Equal(diagonal.X, diagonal.Z, 6);
    }

    [Fact]
    public void DistributeStacksFixturesOnTheAudienceSideOfATower()
    {
        var tower = new StageObject { Kind = StageObjectKind.Truss, Position = new Vec3(-4, 2.5, 1), Size = new Vec3(4, 0.3, 0.3), Roll = 90 };
        var fixtures = Enumerable.Range(0, 4).Select(_ => new Fixture()).ToList();
        StageMath.DistributeAlong(tower, fixtures, drop: 0.35);
        Assert.Equal([1, 2, 3, 4], fixtures.Select(f => f.Position.Y).ToArray());
        Assert.All(fixtures, f => Assert.Equal(-4, f.Position.X, 6));
        Assert.All(fixtures, f => Assert.Equal(1.5, f.Position.Z, 6));
    }

    [Fact]
    public void RollSpinsAMovingHeadsPanAxisButNotItsRestAim()
    {
        var profile = MovingHead();
        var upright = new Fixture { Yaw = 0, Pitch = 0 };
        var sideways = new Fixture { Yaw = 0, Pitch = 0, Roll = 90 };
        var rest = StageMath.BeamDirection(sideways, profile, 0.5, 0.5);
        Assert.Equal(1, rest.Z, 6);
        var panned = 0.5 + 45.0 / profile.PanRange;
        Assert.Equal(0, StageMath.BeamDirection(upright, profile, panned, 0.5).Y, 6);
        var sidewaysPan = StageMath.BeamDirection(sideways, profile, panned, 0.5);
        Assert.Equal(0, sidewaysPan.X, 6);
        Assert.Equal(Math.Sqrt(0.5), Math.Abs(sidewaysPan.Y), 6);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3, 0, -2)]
    [InlineData(-4, 1.5, 3)]
    public void PanTiltTowardWorksOnARolledHead(double x, double y, double z)
    {
        var profile = MovingHead();
        var fixture = new Fixture { Position = new Vec3(-5, 3, 0), Yaw = 90, Pitch = 0, Roll = 90 };
        var target = new Vec3(x, y, z);
        var (pan, tilt) = StageMath.PanTiltToward(fixture, profile, target, (0.5, 0.5))!.Value;
        var direction = StageMath.BeamDirection(fixture, profile, pan, tilt);
        var expected = (target - fixture.Position).Normalized();
        Assert.Equal(expected.X, direction.X, 3);
        Assert.Equal(expected.Y, direction.Y, 3);
        Assert.Equal(expected.Z, direction.Z, 3);
    }

    [Fact]
    public void RaycastHitsTheDeckBeforeTheFloor()
    {
        var deck = new StageObject { Kind = StageObjectKind.Platform, Position = new Vec3(0, 0.5, 0), Size = new Vec3(4, 1, 4) };
        Assert.Equal(5, StageMath.Raycast([deck], new Vec3(0, 6, 0), new Vec3(0, -1, 0))!.Value, 6);
        Assert.Equal(6, StageMath.Raycast([deck], new Vec3(10, 6, 0), new Vec3(0, -1, 0))!.Value, 6);
    }
}
