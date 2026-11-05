using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;

namespace Lampion.Core.Tests;

public class TrussStructureTests
{
    private const double Section = TrussStructures.DefaultSection;

    [Fact]
    public void ArchIsTwoLegsUnderASpanOfTheOuterSize()
    {
        var arch = TrussStructures.Build(TrussShape.Arch, "Arch", new Vec3(1, 0, 2), width: 6, depth: 99, height: 4, yaw: 0);

        Assert.Equal(3, arch.Pieces.Count);
        var span = Assert.Single(arch.Spans);
        Assert.Equal(["Arch left leg", "Arch right leg", "Arch span"], arch.Pieces.Select(p => p.Name).ToArray());
        Assert.Equal(4, span.Position.Y + StageMath.HalfHeight(span), 6);
        Assert.Equal(6, span.Size.X, 6);
        foreach (var leg in arch.Pieces.Take(2))
        {
            Assert.Equal(0, leg.Position.Y - StageMath.HalfHeight(leg), 6);
            Assert.Equal(4 - Section, leg.Position.Y + StageMath.HalfHeight(leg), 6);
            Assert.Equal(2, leg.Position.Z, 6);
        }
        Assert.Equal(1 - 3 + Section / 2, arch.Pieces[0].Position.X, 6);
        Assert.Equal(1 + 3 - Section / 2, arch.Pieces[1].Position.X, 6);
    }

    [Fact]
    public void CubeSideSpansFitBetweenFrontAndBack()
    {
        var cube = TrussStructures.Build(TrussShape.Cube, "Cube", new Vec3(0, 0, 0), width: 4, depth: 3, height: 3, yaw: 0);

        Assert.Equal(8, cube.Pieces.Count);
        Assert.Equal(["Cube front span", "Cube back span", "Cube left span", "Cube right span"], cube.Spans.Select(s => s.Name).ToArray());
        Assert.Equal(1.5 - Section / 2, cube.Spans[0].Position.Z, 6);
        var left = cube.Spans[2];
        Assert.Equal(3 - 2 * Section, left.Size.X, 6);
        Assert.Equal(1.5 - Section, Math.Abs(StageMath.LocalToWorld(left, new Vec3(left.Size.X / 2, 0, 0)).Z), 6);
    }

    [Fact]
    public void TurningTurnsTheWholeStructureAroundItsCentre()
    {
        var straight = TrussStructures.Build(TrussShape.Cube, "Cube", new Vec3(2, 0, 1), 4, 3, 3, yaw: 0);
        var turned = TrussStructures.Build(TrussShape.Cube, "Cube", new Vec3(2, 0, 1), 4, 3, 3, yaw: 90);

        for (var i = 0; i < straight.Pieces.Count; i++)
        {
            var offset = straight.Pieces[i].Position - new Vec3(2, 0, 1);
            // Yaw 90 sends +X to -Z and +Z to +X.
            Assert.Equal(2 + offset.Z, turned.Pieces[i].Position.X, 3);
            Assert.Equal(1 - offset.X, turned.Pieces[i].Position.Z, 3);
            Assert.Equal(straight.Pieces[i].Yaw + 90, turned.Pieces[i].Yaw, 6);
        }
    }

    [Fact]
    public void LightsHangUnderTheArchSpan()
    {
        var arch = TrussStructures.Build(TrussShape.Arch, "Arch", new Vec3(0, 0, 0), 6, 0, 4, 0);
        var lights = Enumerable.Range(0, 3).Select(_ => new Fixture()).ToList();
        StageMath.DistributeAlong(arch.Spans[0], lights, drop: 0.35);
        Assert.Equal([-2, 0, 2], lights.Select(f => f.Position.X).ToArray());
        Assert.All(lights, f => Assert.Equal(4 - Section - 0.35, f.Position.Y, 6));
    }

    [Fact]
    public void TinySizesAreClampedToSomethingBuildable()
    {
        var arch = TrussStructures.Build(TrussShape.Arch, "Arch", default, width: 0.1, depth: 0, height: 0.1, yaw: 0);
        Assert.All(arch.Pieces, p => Assert.True(p.Size.X > 0));
    }
}
