using Avalonia.Controls;
using LightEffect = Lampion.Core.Model.Effects.Effect;

namespace Lampion.App.Views.Inspector;

/// <summary>A single-colour effect parameter (<c>#RRGGBB</c>), edited with the colour picker.</summary>
/// <typeparam name="TEffect">The effect type that declares the parameter.</typeparam>
/// <param name="label">Row label.</param>
/// <param name="get">Reads the colour, <c>#RRGGBB</c>.</param>
/// <param name="set">Writes the colour, <c>#RRGGBB</c>.</param>
internal sealed class ColorParameter<TEffect>(string label, Func<TEffect, string> get, Action<TEffect, string> set) : EffectParameter(label)
    where TEffect : LightEffect
{
    /// <inheritdoc />
    public override Control CreateEditor(LightEffect effect, InspectorFields fields)
    {
        var typed = (TEffect)effect;
        return fields.Color(get(typed), hex => set(typed, hex));
    }
}
