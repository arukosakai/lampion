using Lampion.Core.Model;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;
using Lampion.Core.Persistence;

namespace Lampion.App.Editor;

/// <summary>Ways to begin a show: blank, from a stage layout, or the demo. Each call returns a new project.</summary>
internal static class ProjectFactory
{
    private const string FirstLightTrack = "Lights 1";

    /// <summary>A bare show: the built-in profiles, one light track and empty audio and video tracks.</summary>
    public static Project Empty()
    {
        var project = WithBuiltInProfiles();
        project.Tracks.AddRange([new Track { Name = FirstLightTrack }, MediaTrack(TrackKind.Audio), MediaTrack(TrackKind.Video)]);
        return project;
    }

    /// <summary>A blank show on a ready venue, with one light track per group so the first clip can go straight on.</summary>
    public static Project FromLayout(StageLayout layout)
    {
        var project = WithBuiltInProfiles();
        layout.ApplyTo(project, includeRig: true);
        var tracks = project.Groups.Select(g => new Track { Name = g.Name, GroupIds = { g.Id }, Order = FixtureOrder.LeftToRight }).ToList();
        if (tracks.Count == 0)
            tracks.Add(new Track { Name = FirstLightTrack });
        project.Tracks.AddRange([.. tracks, MediaTrack(TrackKind.Audio), MediaTrack(TrackKind.Video)]);
        return project;
    }

    /// <summary>The demo show: pars, moving heads and a rainbow.</summary>
    public static Project Demo()
    {
        var project = DemoProject.Create();
        project.Name = "Demo show";
        return project;
    }

    private static Project WithBuiltInProfiles()
    {
        var project = new Project();
        project.Profiles.AddRange(BuiltInProfiles.All().Select(p => p.Clone(newId: false)));
        return project;
    }

    private static Track MediaTrack(TrackKind kind) => new() { Name = kind.ToString(), Kind = kind };
}
