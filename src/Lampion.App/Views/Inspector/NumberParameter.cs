using Avalonia.Controls;
using Lampion.App.Controls;
using LightEffect = Lampion.Core.Model.Effects.Effect;

namespace Lampion.App.Views.Inspector;

/// <summary>A floating-point effect parameter, edited with a <see cref="ScrubValue"/> and stored rounded to 4 decimals.</summary>
/// <typeparam name="TEffect">The effect type that declares the parameter.</typeparam>
/// <param name="label">Row label.</param>
/// <param name="range">Limits, drag speed and display.</param>
/// <param name="get">Reads the parameter.</param>
/// <param name="set">Writes the parameter.</param>
internal sealed class NumberParameter<TEffect>(string label, ScrubRange range, Func<TEffect, double> get, Action<TEffect, double> set) : EffectParameter(label)
    where TEffect : LightEffect
{
    /// <inheritdoc />
    public override Control CreateEditor(LightEffect effect, InspectorFields fields)
    {
        var typed = (TEffect)effect;
        return fields.Scrub(get(typed), range, v => set(typed, Math.Round(v, 4)));
    }
}
