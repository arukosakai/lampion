using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;

namespace Lampion.Core.Model.Showcase;

/// <summary>The four-bar build before a drop: front hits doubling in speed, a strobe ramp and a blinder swell.</summary>
internal static class ShowcaseRiser
{
    /// <summary>Adds a riser running from bar <paramref name="start"/> to <paramref name="start"/> + 4.</summary>
    public static void Add(ShowcaseTracks tracks, double start)
    {
        tracks.FrontHits.Clips.AddRange(
            [
                Cue.Clip("Riser ¼", start, 2, Hits(Cue.Beat, decay: 0.2, intensity: 0.6)),
                Cue.Clip("Riser ⅛", start + 2, 1, Hits(Cue.Beat / 2, decay: 0.12, intensity: 0.8)),
                Cue.Clip("Riser 1/16", start + 3, 1, Hits(Cue.Beat / 4, decay: 0.08, intensity: 1))
                    .With(Cue.Lane(AutomationTarget.Intensity, (0, 0.5, Easing.EaseIn), (1, 1, Easing.Linear))),
            ]
        );
        tracks.Strobes.Clips.Add(
            Cue.Clip("Strobe ramp", start + 3, 1, new StrobeEffect { Rate = 12, Duty = 0.25 })
                .With(Cue.Lane(AutomationTarget.Intensity, (0, 0, Easing.EaseIn), (1, 1, Easing.Linear)))
        );
        tracks.Blinders.Clips.Add(
            Cue.Clip("Swell", start + 2, 2, new SolidEffect { Color = "#FFFFFF" })
                .With(Cue.Lane(AutomationTarget.Intensity, (0, 0, Easing.EaseIn), (2, 0.8, Easing.Linear)))
        );
    }

    private static HitEffect Hits(double interval, double decay, double intensity) =>
        new()
        {
            Color = "#FFFFFF",
            Interval = interval,
            Decay = decay,
            Intensity = intensity,
        };
}
