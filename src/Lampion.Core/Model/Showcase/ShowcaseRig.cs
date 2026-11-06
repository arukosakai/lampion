using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;

namespace Lampion.Core.Model.Showcase;

/// <summary>
/// The showcase's 48 fixtures on <see cref="ShowcaseVenue"/>, patched back to back in universe 1, and the groups
/// the timeline drives. The cube and each pixel tower become structures, so moving them takes their lights along.
/// </summary>
internal sealed class ShowcaseRig
{
    private readonly Project _project;
    private int _nextAddress = 1;

    private ShowcaseRig(Project project) => _project = project;

    /// <summary>Eight RGBWA+UV washes on the front span, aimed at the deck.</summary>
    public FixtureGroup FrontPars { get; } = new() { Name = "Front pars", Color = "#E6A23A" };

    /// <summary>Four strobes between the front pars.</summary>
    public FixtureGroup Strobes { get; } = new() { Name = "Strobes", Color = "#E8E8F0" };

    /// <summary>Four tungsten blinders on the front legs, facing the audience.</summary>
    public FixtureGroup Blinders { get; } = new() { Name = "Blinders", Color = "#FFB45A" };

    /// <summary>Six wash movers on the mid truss.</summary>
    public FixtureGroup WashMovers { get; } = new() { Name = "Wash movers", Color = "#3AE6C8" };

    /// <summary>The four spots on the left half of the back span, outermost first.</summary>
    public FixtureGroup SpotsLeft { get; } = new() { Name = "Spots L", Color = "#3AA0E6" };

    /// <summary>The four spots on the right half of the back span, outermost first.</summary>
    public FixtureGroup SpotsRight { get; } = new() { Name = "Spots R", Color = "#7A5AF0" };

    /// <summary>Four spots standing on the deck behind the band, fanned out toward the audience.</summary>
    public FixtureGroup FloorHeads { get; } = new() { Name = "Floor heads", Color = "#E63A8E" };

    /// <summary>Six LED bars on the deck grazing the back wall.</summary>
    public FixtureGroup WallBars { get; } = new() { Name = "Wall bars", Color = "#4AE65A" };

    /// <summary>Eight RGB pars on the towers, listed up the left tower and down the right one so chases arc over.</summary>
    public FixtureGroup PixelTowers { get; } = new() { Name = "Pixel towers", Color = "#E6E03A" };

    /// <summary>Patches and hangs every fixture on <paramref name="venue"/> and adds the groups and structures.</summary>
    public static ShowcaseRig Build(Project project, ShowcaseVenue venue)
    {
        var rig = new ShowcaseRig(project);
        var cube = new List<Fixture>();
        cube.AddRange(rig.HangFrontSpan(venue.FrontSpan));
        cube.AddRange(rig.HangBlinders(venue.FrontLeftLeg, venue.FrontRightLeg));
        cube.AddRange(rig.HangWashMovers(venue.MidTruss));
        cube.AddRange(rig.HangSpots(venue.BackSpan));
        rig.StandFloorHeads();
        rig.StandWallBars();
        var towerLeft = rig.HangTower(venue.TowerLeft, "L");
        var towerRight = rig.HangTower(venue.TowerRight, "R");
        towerRight.Reverse();
        rig.PixelTowers.FixtureIds.AddRange([.. towerLeft.Select(f => f.Id), .. towerRight.Select(f => f.Id)]);

        StructureEdits.Create(project, "Ground support", [.. venue.GroundSupport.Pieces, venue.MidTruss], cube);
        StructureEdits.Create(project, "Pixel tower L", [venue.TowerLeft], towerLeft);
        StructureEdits.Create(project, "Pixel tower R", [venue.TowerRight], towerRight);
        project.Groups.AddRange([rig.FrontPars, rig.Strobes, rig.Blinders, rig.WashMovers, rig.SpotsLeft, rig.SpotsRight, rig.FloorHeads, rig.WallBars, rig.PixelTowers]);
        return rig;
    }

    private IEnumerable<Fixture> HangFrontSpan(StageObject span)
    {
        var pars = Enumerable.Range(1, 8).Select(i => Patch($"Front par {i}", BuiltInProfiles.HexParId, FrontPars)).ToList();
        var strobes = Enumerable.Range(1, 4).Select(i => Patch($"Strobe {i}", BuiltInProfiles.StrobeId, Strobes)).ToList();
        // A strobe after every second par, mirrored: P S P P S P P S P P S P.
        Fixture[] bar = [pars[0], strobes[0], pars[1], pars[2], strobes[1], pars[3], pars[4], strobes[2], pars[5], pars[6], strobes[3], pars[7]];
        StageMath.DistributeAlong(span, bar);
        foreach (var par in pars)
            StageMath.AimAt(par, new Vec3(par.Position.X * 0.55, ShowcaseVenue.DeckTop + 0.6, -1.5));
        foreach (var strobe in strobes)
            StageMath.AimAt(strobe, new Vec3(strobe.Position.X * 0.5, ShowcaseVenue.DeckTop, 0.5));
        return bar;
    }

    private IEnumerable<Fixture> HangBlinders(StageObject leftLeg, StageObject rightLeg)
    {
        var blinders = new List<Fixture>();
        foreach (var leg in new[] { leftLeg, rightLeg })
            foreach (var height in new[] { 5.6, 3.2 })
            {
                var blinder = Patch($"Blinder {blinders.Count + 1}", ShowcaseProfiles.BlinderId, Blinders);
                blinder.GelColor = "#FFB45A";
                blinder.Position = new Vec3(leg.Position.X, height, leg.Position.Z + 0.4);
                StageMath.AimAt(blinder, new Vec3(leg.Position.X * 0.4, 1.7, 16));
                blinders.Add(blinder);
            }
        return blinders;
    }

    private IEnumerable<Fixture> HangWashMovers(StageObject truss)
    {
        var movers = Enumerable.Range(1, 6).Select(i => Patch($"Wash {i}", ShowcaseProfiles.WashMoverId, WashMovers)).ToList();
        StageMath.DistributeAlong(truss, movers);
        foreach (var mover in movers)
            mover.Pitch = -75;
        return movers;
    }

    private IEnumerable<Fixture> HangSpots(StageObject span)
    {
        var spots = Enumerable.Range(1, 8).Select(i => Patch($"Spot {i}", BuiltInProfiles.MovingHeadId, i <= 4 ? SpotsLeft : SpotsRight)).ToList();
        StageMath.DistributeAlong(span, spots);
        foreach (var spot in spots)
        {
            // Rigged fanned out, so one pan curve for the whole group still reads as a spread of beams.
            spot.Yaw = Math.Round(spot.Position.X * 2, 2);
            spot.Pitch = -60;
        }
        SpotsRight.FixtureIds.Reverse();
        return spots;
    }

    private void StandFloorHeads()
    {
        foreach (var x in new[] { -6.4, -3.2, 3.2, 6.4 })
        {
            var head = Patch($"Floor head {FloorHeads.FixtureIds.Count + 1}", BuiltInProfiles.MovingHeadId, FloorHeads);
            head.Position = new Vec3(x, ShowcaseVenue.DeckTop + 0.25, -4.8);
            head.Yaw = x * 3;
            head.Pitch = 55;
        }
    }

    private void StandWallBars()
    {
        for (var i = 0; i < 6; i++)
        {
            var bar = Patch($"Wall bar {i + 1}", BuiltInProfiles.LedBarId, WallBars);
            bar.Position = new Vec3(-4 + i * 1.6, ShowcaseVenue.DeckTop + 0.08, -5.45);
            bar.Yaw = 180;
            bar.Pitch = 78;
        }
    }

    private List<Fixture> HangTower(StageObject tower, string side)
    {
        var pars = Enumerable.Range(1, 4).Select(i => Patch($"Pixel {side}{i}", BuiltInProfiles.RgbParId, group: null)).ToList();
        StageMath.DistributeAlong(tower, pars);
        foreach (var par in pars)
            StageMath.AimAt(par, new Vec3(par.Position.X * 1.5, par.Position.Y + 3, 20));
        return pars;
    }

    private Fixture Patch(string name, string profileId, FixtureGroup? group)
    {
        var profile = _project.Profiles.First(p => p.Id == profileId);
        var fixture = new Fixture { Name = name, ProfileId = profileId, Address = _nextAddress };
        _nextAddress += profile.Channels.Count;
        _project.Fixtures.Add(fixture);
        group?.FixtureIds.Add(fixture.Id);
        return fixture;
    }
}
