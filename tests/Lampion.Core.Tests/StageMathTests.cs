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
