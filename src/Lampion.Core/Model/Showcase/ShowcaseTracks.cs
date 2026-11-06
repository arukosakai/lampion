using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Model.Showcase;

/// <summary>The showcase's light tracks, one per group, plus an additive hit layer over the front wash.</summary>
internal sealed class ShowcaseTracks
{
    private ShowcaseTracks(ShowcaseRig rig)
    {
        Blinders = Light("Blinders", rig.Blinders, BlendMode.Htp, FixtureOrder.OutsideIn);
        Strobes = Light("Strobes", rig.Strobes, BlendMode.Add, FixtureOrder.LeftToRight);
        SpotsLeft = Light("Spots L", rig.SpotsLeft, BlendMode.Normal, FixtureOrder.AsListed);
        SpotsRight = Light("Spots R", rig.SpotsRight, BlendMode.Normal, FixtureOrder.AsListed);
        FloorHeads = Light("Floor heads", rig.FloorHeads, BlendMode.Normal, FixtureOrder.CenterOut);
        WashMovers = Light("Wash movers", rig.WashMovers, BlendMode.Normal, FixtureOrder.LeftToRight);
        FrontHits = Light("Front hits", rig.FrontPars, BlendMode.Add, FixtureOrder.CenterOut);
        FrontWash = Light("Front wash", rig.FrontPars, BlendMode.Normal, FixtureOrder.CenterOut);
        PixelTowers = Light("Pixel towers", rig.PixelTowers, BlendMode.Normal, FixtureOrder.AsListed);
        WallBars = Light("Wall bars", rig.WallBars, BlendMode.Normal, FixtureOrder.LeftToRight);
    }

    /// <summary>Audience blinders; HTP so a swell and a hit can overlap without one hiding the other.</summary>
    public Track Blinders { get; }

    /// <summary>Strobes, added on top of whatever else lights the deck.</summary>
    public Track Strobes { get; }

    /// <summary>Left half of the back-span spots, outermost first, so chases mirror <see cref="SpotsRight"/>.</summary>
    public Track SpotsLeft { get; }

    /// <summary>Right half of the back-span spots, outermost first.</summary>
    public Track SpotsRight { get; }

    /// <summary>The deck spots behind the band.</summary>
    public Track FloorHeads { get; }

    /// <summary>The mid-truss wash movers.</summary>
    public Track WashMovers { get; }

    /// <summary>Beat hits added over <see cref="FrontWash"/> on the same pars.</summary>
    public Track FrontHits { get; }

    /// <summary>The front pars' base look.</summary>
    public Track FrontWash { get; }

    /// <summary>The pars on the two towers, chasing up one and down the other.</summary>
    public Track PixelTowers { get; }

    /// <summary>The LED bars grazing the back wall.</summary>
    public Track WallBars { get; }

    /// <summary>Creates the tracks and adds them to <paramref name="project"/>, with empty audio and video tracks below.</summary>
    public static ShowcaseTracks AddTo(Project project, ShowcaseRig rig)
    {
        var tracks = new ShowcaseTracks(rig);
        project.Tracks.AddRange(
            [
                tracks.Blinders,
                tracks.Strobes,
                tracks.SpotsLeft,
                tracks.SpotsRight,
                tracks.FloorHeads,
                tracks.WashMovers,
                tracks.FrontHits,
                tracks.FrontWash,
                tracks.PixelTowers,
                tracks.WallBars,
                new Track { Name = "Audio", Kind = TrackKind.Audio },
                new Track { Name = "Video", Kind = TrackKind.Video },
            ]
        );
        return tracks;
    }

    private static Track Light(string name, FixtureGroup group, BlendMode blend, FixtureOrder order) =>
        new()
        {
            Name = name,
            GroupIds = { group.Id },
            Blend = blend,
            Order = order,
        };
}
