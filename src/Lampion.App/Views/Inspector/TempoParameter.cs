using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Lampion.App.Controls;
using Lampion.Core.Model.Timeline;
using LightEffect = Lampion.Core.Model.Effects.Effect;

namespace Lampion.App.Views.Inspector;

/// <summary>
/// A timing property (rate, speed, step time, interval) shown in its own unit or in BPM (<see cref="TempoUnits"/>),
/// with tap-tempo and a button that matches the show's BPM.
/// </summary>
/// <typeparam name="TEffect">The effect type that declares the parameter.</typeparam>
/// <param name="label">Row label.</param>
/// <param name="scale">What the stored value means and how it's shown.</param>
/// <param name="get">Reads the stored value.</param>
/// <param name="set">Writes the stored value.</param>
internal sealed class TempoParameter<TEffect>(string label, TempoScale scale, Func<TEffect, double> get, Action<TEffect, double> set) : EffectParameter(label)
    where TEffect : LightEffect
{
    /// <inheritdoc />
    public override Control CreateEditor(LightEffect effect, InspectorFields fields)
    {
        var typed = (TEffect)effect;
        Action<double> write = v => set(typed, Math.Round(v, 4));
        var scrub = fields.Scrub(get(typed), TempoUnits.Current(scale), write);
        var context = fields.Context;

        var unit = new GlyphButton(TempoUnits.InBpm ? "BPM" : scale.NativeUnit, "Switch every timing value between its own unit and beats per minute", () =>
        {
            TempoUnits.Toggle();
            context.Rebuild();
        });
        var match = new GlyphButton("♩", "Match the show's BPM (once per beat)", () =>
        {
            var value = scale.FromBpm(context.State.Project.Bpm, get(typed));
            scrub.Show(value);
            context.Edit(write, value);
        });

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { scrub, unit, TapButton(scrub, () => get(typed), write, context), match },
        };
    }

    // Each tap edits live; one undo step is recorded once the run ends, not one per tap.
    private GlyphButton TapButton(ScrubValue scrub, Func<double> current, Action<double> write, InspectorContext context)
    {
        var tempo = new TapTempo();
        var commit = new DispatcherTimer { Interval = TimeSpan.FromSeconds(TapTempo.MaxGap) };
        commit.Tick += (_, _) =>
        {
            commit.Stop();
            context.State.Commit();
        };
        return new GlyphButton("Tap", "Tap along to the beat (click, or focus and press Space) to set the timing", () =>
        {
            if (tempo.Tap(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency) is not { } bpm)
                return;
            commit.Stop();
            var value = scale.FromBpm(bpm, current());
            scrub.Show(value);
            context.Edit(write, value, commit: false);
            commit.Start();
        });
    }
}
