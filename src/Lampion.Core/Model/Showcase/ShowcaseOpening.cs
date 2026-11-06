using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Model.Showcase;

/// <summary>Bars 0–16: a dark, blue intro with searchlights, then a build that ends in a riser.</summary>
internal static class ShowcaseOpening
{
    /// <summary>Spot pan positions for the build, one per bar, held until the next.</summary>
    private static readonly double[] BeatMarks = [0.46, 0.54, 0.48, 0.56, 0.44, 0.52, 0.5, 0.58];

    /// <summary>Adds the intro (bars 0–8) and the build (bars 8–16).</summary>
    public static void Add(ShowcaseTracks tracks)
    {
        Intro(tracks);
        Build(tracks);
        ShowcaseRiser.Add(tracks, start: 12);
    }

    private static void Intro(ShowcaseTracks tracks)
    {
        tracks.WallBars.Clips.Add(Cue.Clip("Deep tide", 0, 8, new ColorWaveEffect { Color = "#00B3FF", Base = "#050A40", Rate = 0.15, Wavelength = 6 }, fadeIn: 4));
        tracks.PixelTowers.Clips.Add(Cue.Clip("Starfield", 2, 6, new SparkleEffect { Color = "#9FD8FF", Rate = 1.2, Decay = 0.7, Intensity = 0.8 }, fadeIn: 2));
        tracks.FrontWash.Clips.Add(Cue.Clip("Night falls", 0, 8, new ColorFadeEffect { From = "#000000", To = "#1A2CFF", Easing = Easing.EaseIn, Offset = 0.15 }));
        tracks.WashMovers.Clips.Add(Cue.Clip("Haze glow", 4, 4, new SolidEffect { Color = "#2030A0", Intensity = 0.5 }, fadeIn: 3));
        foreach (var (track, side) in new[] { (tracks.SpotsLeft, 1), (tracks.SpotsRight, -1) })
            track.Clips.Add(
                Cue.Clip("Searchlights", 4, 4, new SolidEffect { Color = "#E8F0FF", Intensity = 0.85 }, fadeIn: 2)
                    .With(Cue.Swing(ChannelRole.Pan, 4, period: 4, 0.5, side * 0.05), Cue.Lane(ChannelRole.Tilt, (0, 0.52, Easing.Linear)))
            );
    }

    private static void Build(ShowcaseTracks tracks)
    {
        tracks.FrontWash.Clips.Add(
            Cue.Clip("Cyan tide", 8, 8, new ColorWaveEffect { Color = "#00E5FF", Base = "#6A00FF", Rate = 0.5, Wavelength = 4 }, transition: Cue.Into(TransitionKind.Crossfade, 2))
                .With(Cue.Lane(AutomationTarget.Intensity, (0, 0.55, Easing.EaseIn), (8, 1, Easing.Linear)))
        );
        tracks.WashMovers.Clips.Add(
            Cue.Clip("Slow rainbow", 8, 8, new RainbowEffect { Speed = 0.08, Spread = 1, Saturation = 0.85 }, transition: Cue.Into(TransitionKind.Crossfade, 1))
                .With(Cue.Swing(ChannelRole.Pan, 8, period: 8, 0.5, 0.04), Cue.Lane(ChannelRole.Tilt, (0, 0.5, Easing.EaseInOut), (8, 0.62, Easing.Linear)))
        );
        foreach (var (track, mirrored) in new[] { (tracks.SpotsLeft, false), (tracks.SpotsRight, true) })
        {
            var marks = BeatMarks.Select((pan, bar) => ((double)bar, mirrored ? 1 - pan : pan, Easing.Hold)).ToArray();
            track.Clips.Add(
                Cue.Clip("Beat marks", 8, 8, new ChaseEffect { Colors = ["#FFFFFF"], StepTime = Cue.Beat, Tail = 1, Background = 0.15 }, transition: Cue.Into(TransitionKind.Crossfade, 1))
                    .With(Cue.Lane(ChannelRole.Pan, marks), Cue.Lane(ChannelRole.Tilt, (0, 0.52, Easing.Linear), (6, 0.52, Easing.EaseIn), (8, 0.62, Easing.Linear)))
            );
        }
        tracks.FloorHeads.Clips.Add(
            Cue.Clip("Rising fan", 12, 4, new PulseEffect { Color = "#FFFFFF", Rate = Cue.BeatRate, Minimum = 0.1, Wave = Waveform.Saw }, fadeIn: 1)
                .With(Cue.Lane(ChannelRole.Tilt, (0, 0.35, Easing.EaseIn), (4, 0.6, Easing.Linear)))
        );
        tracks.WallBars.Clips.Add(
            Cue.Clip("Neon scroll", 8, 8, new GradientEffect { Colors = ["#2200FF", "#00E5FF", "#FF00C8"], Speed = 0.25 }, transition: Cue.Into(TransitionKind.Wipe, 2))
        );
        tracks.PixelTowers.Clips.Add(
            Cue.Clip("Arc chase", 8, 8, new ChaseEffect { Colors = ["#00E5FF"], StepTime = Cue.Beat / 2, Tail = 2 }, transition: Cue.Into(TransitionKind.Crossfade, 1))
        );
    }
}
