using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Model.Showcase;

/// <summary>Bars 40–48: an amber sunset wipes in and fades away, then one last full-rig hit on bar 46.</summary>
internal static class ShowcaseFinale
{
    private const double Start = 40;

    /// <summary>The bar the final hit lands on.</summary>
    public const double Button = 46;

    /// <summary>Adds the outro and the final hit.</summary>
    public static void Add(ShowcaseTracks tracks)
    {
        Outro(tracks);
        FinalHit(tracks);
    }

    private static void Outro(ShowcaseTracks tracks)
    {
        tracks.FrontWash.Clips.Add(
            Cue.Clip("Sunset", Start, 6, new ColorFadeEffect { From = "#FF8C3A", To = "#FF2A00", Offset = 0.1 }, fadeOut: 2, transition: Cue.Into(TransitionKind.Wipe, 2))
                .With(Cue.Lane(AutomationTarget.White, (0, 0, Easing.EaseInOut), (3, 0.35, Easing.EaseInOut), (6, 0, Easing.Linear)))
        );
        tracks.WashMovers.Clips.Add(
            Cue.Clip("Fading amber", Start, 6, new SolidEffect { Color = "#FF6A1A" }, transition: Cue.Into(TransitionKind.Crossfade, 1))
                .With(
                    Cue.Lane(AutomationTarget.Saturation, (0, 1, Easing.EaseIn), (6, 0.3, Easing.Linear)),
                    Cue.Lane(AutomationTarget.Opacity, (0, 1, Easing.Linear), (4, 1, Easing.EaseIn), (6, 0, Easing.Linear)),
                    Cue.Lane(ChannelRole.Pan, (0, 0.5, Easing.Linear)),
                    Cue.Lane(ChannelRole.Tilt, (0, 0.58, Easing.EaseInOut), (6, 0.45, Easing.Linear))
                )
        );
        foreach (var (track, side) in new[] { (tracks.SpotsLeft, 1), (tracks.SpotsRight, -1) })
            track.Clips.Add(
                Cue.Clip("Last light", Start, 6, new SolidEffect { Color = "#FFFFFF", Intensity = 0.9 }, fadeOut: 3, transition: Cue.Into(TransitionKind.Crossfade, 1))
                    .With(
                        Cue.Lane(ChannelRole.Pan, (0, 0.5 - side * 0.03, Easing.EaseInOut), (6, 0.5, Easing.Linear)),
                        Cue.Lane(ChannelRole.Tilt, (0, 0.55, Easing.EaseInOut), (6, 0.4, Easing.Linear))
                    )
            );
        tracks.WallBars.Clips.Add(Cue.Clip("Afterglow", Start, 6, new FireEffect(), fadeOut: 3, transition: Cue.Into(TransitionKind.Crossfade, 2)));
        tracks.PixelTowers.Clips.Add(
            Cue.Clip("Last stars", Start, 6, new SparkleEffect { Color = "#FFB060", Rate = 1, Decay = 0.8 }, fadeOut: 3, transition: Cue.Into(TransitionKind.Crossfade, 1))
        );
    }

    private static void FinalHit(ShowcaseTracks tracks)
    {
        tracks.Blinders.Clips.Add(Cue.Clip("Button", Button, 2, new HitEffect { Color = "#FFFFFF", Decay = 1.6 }));
        tracks.Strobes.Clips.Add(Cue.Clip("Button", Button, 1, new HitEffect { Color = "#FFFFFF", Decay = 0.3 }));
        tracks.FrontHits.Clips.Add(Cue.Clip("Button", Button, 2, new HitEffect { Color = "#FFFFFF", Decay = 1.2 }));
        tracks.FloorHeads.Clips.Add(Cue.Clip("Button", Button, 2, new HitEffect { Color = "#FFFFFF", Decay = 1.2 }).With(Cue.Lane(ChannelRole.Tilt, (0, 0.6, Easing.Linear))));
    }
}
