using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Model.Showcase;

/// <summary>Bars 24–32: everything dips to black, then a red heartbeat under UV, stars, embers, and a riser into drop 2.</summary>
internal static class ShowcaseBreakdown
{
    private const double Start = 24;

    /// <summary>Adds the breakdown and the riser at its end.</summary>
    public static void Add(ShowcaseTracks tracks)
    {
        var dip = Cue.Into(TransitionKind.DipToBlack, 1);
        tracks.FrontWash.Clips.Add(
            Cue.Clip("Heartbeat", Start, 8, new HeartbeatEffect { Color = "#FF1744", Rate = Cue.BeatRate / 2, Minimum = 0.05 }, transition: dip.Clone())
                .With(Cue.Lane(ChannelRole.UV, (0, 0.7, Easing.Linear)))
        );
        tracks.WashMovers.Clips.Add(
            Cue.Clip("Stars", Start, 8, new SparkleEffect { Color = "#FFFFFF", Rate = 3, Decay = 0.5, Intensity = 0.7 }, transition: dip.Clone())
                .With(Cue.Lane(ChannelRole.Pan, (0, 0.5, Easing.Linear)), Cue.Lane(ChannelRole.Tilt, (0, 0.6, Easing.EaseInOut), (8, 0.45, Easing.Linear)))
        );
        foreach (var (track, side) in new[] { (tracks.SpotsLeft, 1), (tracks.SpotsRight, -1) })
            track.Clips.Add(
                Cue.Clip("Warm drift", Start, 8, new SolidEffect { Color = "#FFD7A0", Intensity = 0.8 }, transition: dip.Clone())
                    .With(Cue.Swing(ChannelRole.Pan, 8, period: 8, 0.5, side * 0.03), Cue.Lane(ChannelRole.Tilt, (0, 0.5, Easing.Linear)))
            );
        tracks.FloorHeads.Clips.Add(
            Cue.Clip("Flames", Start + 2, 6, new FireEffect { Ember = "#7A0A00", Flame = "#FF8A2A", Rate = 6 }, fadeIn: 2)
                .With(Cue.Lane(ChannelRole.Tilt, (0, 0.3, Easing.EaseInOut), (6, 0.55, Easing.Linear)))
        );
        tracks.WallBars.Clips.Add(Cue.Clip("Embers", Start, 8, new FireEffect(), transition: Cue.Into(TransitionKind.Crossfade, 1)));
        tracks.PixelTowers.Clips.Add(Cue.Clip("Breathing arc", Start, 8, new PulseEffect { Color = "#FFC890", Rate = 0.25, Phase = 0.1 }, transition: dip.Clone()));
        ShowcaseRiser.Add(tracks, start: Start + 4);
    }
}
