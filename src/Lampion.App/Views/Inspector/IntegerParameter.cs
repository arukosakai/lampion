using Avalonia.Controls;
using Lampion.App.Controls;
using LightEffect = Lampion.Core.Model.Effects.Effect;

namespace Lampion.App.Views.Inspector;

/// <summary>A whole-number effect parameter (0..99999), such as a random seed.</summary>
/// <typeparam name="TEffect">The effect type that declares the parameter.</typeparam>
/// <param name="label">Row label.</param>
/// <param name="get">Reads the parameter.</param>
/// <param name="set">Writes the parameter.</param>
internal sealed class IntegerParameter<TEffect>(string label, Func<TEffect, int> get, Action<TEffect, int> set) : EffectParameter(label)
    where TEffect : LightEffect
{
    private static readonly ScrubRange Range = new(0, 99999, 0.5, Format: "0");

    /// <inheritdoc />
    public override Control CreateEditor(LightEffect effect, InspectorFields fields)
    {
        var typed = (TEffect)effect;
        return fields.Scrub(get(typed), Range, v => set(typed, (int)Math.Round(v)));
    }
}
