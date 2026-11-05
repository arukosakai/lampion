using Avalonia.Controls;
using LightEffect = Lampion.Core.Model.Effects.Effect;

namespace Lampion.App.Views.Inspector;

/// <summary>An enum effect parameter, edited with a drop-down of every value in declaration order.</summary>
/// <typeparam name="TEffect">The effect type that declares the parameter.</typeparam>
/// <typeparam name="TEnum">The parameter's enum type.</typeparam>
/// <param name="label">Row label.</param>
/// <param name="get">Reads the parameter.</param>
/// <param name="set">Writes the parameter.</param>
internal sealed class ChoiceParameter<TEffect, TEnum>(string label, Func<TEffect, TEnum> get, Action<TEffect, TEnum> set) : EffectParameter(label)
    where TEffect : LightEffect
    where TEnum : struct, Enum
{
    /// <inheritdoc />
    public override Control CreateEditor(LightEffect effect, InspectorFields fields)
    {
        var typed = (TEffect)effect;
        return fields.Choice(Enum.GetValues<TEnum>(), get(typed), v => set(typed, v));
    }
}
