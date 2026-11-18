using Avalonia.Controls;
using LightEffect = Lampion.Core.Model.Effects.Effect;

namespace Lampion.App.Views.Inspector;

/// <summary>A free-text effect parameter, such as the message a Morse clip blinks; commits on Enter or losing focus.</summary>
/// <typeparam name="TEffect">The effect type that declares the parameter.</typeparam>
/// <param name="label">Row label.</param>
/// <param name="get">Reads the text.</param>
/// <param name="set">Writes the text.</param>
internal sealed class TextParameter<TEffect>(string label, Func<TEffect, string> get, Action<TEffect, string> set) : EffectParameter(label)
    where TEffect : LightEffect
{
    /// <inheritdoc />
    public override Control CreateEditor(LightEffect effect, InspectorFields fields)
    {
        var typed = (TEffect)effect;
        return fields.Text(get(typed), v => set(typed, v));
    }
}
