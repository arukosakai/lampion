using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Model.Showcase;

/// <summary>The two drops: bars 16–24 in magenta and cyan, bars 32–40 in full rainbow with a lightning storm.</summary>
internal static class ShowcaseDrops
{
    /// <summary>Adds both drops.</summary>
    public static void Add(ShowcaseTracks tracks)
    {
        First(tracks, start: 16);
        Second(tracks, start: 32);
    }

    private static void First(ShowcaseTracks tracks, double start)
    {
        tracks.Blinders.Clips.Add(Cue.Clip("Drop blinder", start, 8, new HitEffect { Color = "#FFFFFF", Decay = 0.9, Interval = Cue.Bars(2) }));
        // Claps on 2 and 4: the clip starts a beat in and hits every other beat.
        tracks.Strobes.Clips.Add(Cue.Clip("Backbeat", start + 0.25, 7.75, new HitEffect { Color = "#FFFFFF", Decay = 0.1, Interval = 2 * Cue.Beat }));
        foreach (var (track, side) in new[] { (tracks.SpotsLeft, 1), (tracks.SpotsRight, -1) })
            track.Clips.Add(
                Cue.Clip("Mirror sweep", start, 8, new ChaseEffect { Colors = ["#FF2A6D", "#00E5FF"], StepTime = Cue.Beat / 2, Direction = ChaseDirection.Bounce, Tail = 1.5, Background = 0.2 })
                    .With(Cue.Swing(ChannelRole.Pan, 8, period: 2, 0.5, side * 0.06), Cue.Swing(ChannelRole.Tilt, 8, period: 4, 0.54, 0.06))
            );
        tracks.FloorHeads.Clips.Add(
            Cue.Clip("Beat snaps", start, 8, new ColorCycleEffect { Colors = ["#FF2A6D", "#FFFFFF", "#00E5FF"], StepTime = Cue.Beat, FadeRatio = 0.2, FixtureOffset = 1 })
                .With(Cue.Steps(ChannelRole.Tilt, 8, step: 0.25, 0.42, 0.6), Cue.Steps(ChannelRole.Pan, 8, step: 1, 0.47, 0.53))
        );
        tracks.WashMovers.Clips.Add(
            Cue.Clip("Hue sweep", start, 8, new SolidEffect { Color = "#FF0080" })
                .With(
                    Cue.Lane(AutomationTarget.HueShift, (0, 0, Easing.Linear), (8, 2, Easing.Linear)),
                    Cue.Swing(ChannelRole.Pan, 8, period: 4, 0.5, 0.05),
                    Cue.Lane(ChannelRole.Tilt, (0, 0.6, Easing.Linear))
                )
        );
        tracks.FrontWash.Clips.Add(Cue.Clip("Neon gradient", start, 8, new GradientEffect { Colors = ["#FF0080", "#7000FF", "#00E5FF"], Speed = 0.5 }));
        tracks.FrontHits.Clips.Add(Cue.Clip("Rolling hits", start, 8, RollingHits()));
        tracks.PixelTowers.Clips.Add(Cue.Clip("Fast arc", start, 8, new ChaseEffect { Colors = ["#FF2A6D"], StepTime = Cue.Beat / 4, Tail = 3, Background = 0.05 }));
        tracks.WallBars.Clips.Add(Cue.Clip("Pump", start, 8, new PulseEffect { Color = "#FF00A0", Rate = Cue.BeatRate, Minimum = 0.15, Wave = Waveform.Saw }));
    }

    private static void Second(ShowcaseTracks tracks, double start)
    {
        tracks.Blinders.Clips.Add(Cue.Clip("Drop 2 blinder", start, 2, new HitEffect { Color = "#FFFFFF", Decay = 1.5 }));
        tracks.Strobes.Clips.Add(Cue.Clip("Storm", start, 8, new LightningEffect { Rate = 3, Decay = 0.08, Mode = LightningMode.Scattered }));
        foreach (var (track, side) in new[] { (tracks.SpotsLeft, 1), (tracks.SpotsRight, -1) })
            track.Clips.Add(
                Cue.Clip("Circles", start, 8, new ChaseEffect { Colors = ["#FFFFFF", "#FFD700"], StepTime = Cue.Beat / 4, Direction = ChaseDirection.Bounce, Tail = 2, Background = 0.3 })
                    .With(Cue.Swing(ChannelRole.Pan, 8, period: 2, 0.5, side * 0.06), Cue.Swing(ChannelRole.Tilt, 8, period: 2, 0.52, 0.07, late: true))
            );
        tracks.FloorHeads.Clips.Add(
            Cue.Clip("Rainbow snaps", start, 8, new RainbowEffect { Speed = 1, Spread = 1 })
                .With(Cue.Steps(ChannelRole.Tilt, 8, step: 0.25, 0.4, 0.62), Cue.Lane(AutomationTarget.Saturation, (0, 0.2, Easing.EaseIn), (2, 1, Easing.Linear)))
        );
        tracks.WashMovers.Clips.Add(
            Cue.Clip("Rainbow sweep", start, 8, new RainbowEffect { Speed = 0.3, Spread = 0.5, StartHue = 0.5 })
                .With(Cue.Swing(ChannelRole.Pan, 8, period: 2, 0.5, 0.06), Cue.Swing(ChannelRole.Tilt, 8, period: 2, 0.58, 0.05, late: true))
        );
        tracks.FrontWash.Clips.Add(Cue.Clip("Rainbow wipe", start, 8, new RainbowEffect { Speed = 0.5, Spread = 1 }, transition: Cue.Into(TransitionKind.Wipe, 1)));
        tracks.FrontHits.Clips.Add(Cue.Clip("Rolling hits", start, 8, RollingHits()));
        tracks.PixelTowers.Clips.Add(Cue.Clip("Rainbow arc", start, 8, new RainbowEffect { Speed = 1.5, Spread = 1 }));
        tracks.WallBars.Clips.Add(
            Cue.Clip("Square wave", start, 8, new ColorWaveEffect { Color = "#FFFFFF", Base = "#FF0080", Rate = Cue.BeatRate / 2, Wavelength = 2, Wave = Waveform.Square })
        );
    }

    /// <summary>A hit on every beat that rolls out from the centre pars.</summary>
    private static HitEffect RollingHits() =>
        new()
        {
            Color = "#FFFFFF",
            Decay = 0.25,
            Interval = Cue.Beat,
            Offset = 0.04,
            Intensity = 0.6,
        };
}
