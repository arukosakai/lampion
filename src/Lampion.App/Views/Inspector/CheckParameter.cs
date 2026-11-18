using Avalonia.Controls;
using LightEffect = Lampion.Core.Model.Effects.Effect;

namespace Lampion.App.Views.Inspector;

/// <summary>An on/off effect parameter, edited with a check box.</summary>
/// <typeparam name="TEffect">The effect type that declares the parameter.</typeparam>
/// <param name="label">Row label.</param>
/// <param name="get">Reads the parameter.</param>
/// <param name="set">Writes the parameter.</param>
internal sealed class CheckParameter<TEffect>(string label, Func<TEffect, bool> get, Action<TEffect, bool> set) : EffectParameter(label)
    where TEffect : LightEffect
{
    /// <inheritdoc />
    public override Control CreateEditor(LightEffect effect, InspectorFields fields)
    {
        var typed = (TEffect)effect;
        return fields.Check(get(typed), v => set(typed, v));
    }
}
