using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Model.Showcase;

/// <summary>
/// The full-production demo: a festival stage with 48 fixtures on ground-support truss and pixel towers, running
/// a 90 s, 128 bpm show (intro, build, two drops, a breakdown and an outro) that uses every stock effect,
/// every transition and blend mode, and pan/tilt, hue, saturation, white, UV and opacity automation.
/// </summary>
public static class ShowcaseProject
{
    /// <summary>A fresh showcase (new random ids every call), with every built-in profile plus its own two.</summary>
    public static Project Create()
    {
        var project = new Project
        {
            Name = "Festival showcase",
            Bpm = Cue.Bpm,
            Duration = Cue.Bars(48),
        };
        project.Profiles.AddRange(BuiltInProfiles.All().Select(p => p.Clone(newId: false)));
        project.Profiles.AddRange(ShowcaseProfiles.All());

        var venue = ShowcaseVenue.Build(project);
        var rig = ShowcaseRig.Build(project, venue);
        var tracks = ShowcaseTracks.AddTo(project, rig);
        ShowcaseOpening.Add(tracks);
        ShowcaseDrops.Add(tracks);
        ShowcaseBreakdown.Add(tracks);
        ShowcaseFinale.Add(tracks);
        foreach (var track in project.Tracks)
            track.SortClips();

        foreach (var (bar, label) in new[] { (0.0, "Intro"), (8, "Build"), (16, "Drop"), (24, "Breakdown"), (32, "Drop 2"), (40, "Outro"), (ShowcaseFinale.Button, "Button") })
            project.Markers.Add(new Marker { Time = Cue.Bars(bar), Label = label });
        return project;
    }
}
