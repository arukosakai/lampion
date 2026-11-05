using Avalonia.Controls;
using LightEffect = Lampion.Core.Model.Effects.Effect;

namespace Lampion.App.Views.Inspector;

/// <summary>A palette parameter (a list of <c>#RRGGBB</c> colours edited in place), shown as a <see cref="PaletteEditor"/>.</summary>
/// <typeparam name="TEffect">The effect type that declares the parameter.</typeparam>
/// <param name="label">Row label.</param>
/// <param name="colors">Returns the effect's palette list, which is edited in place.</param>
internal sealed class PaletteParameter<TEffect>(string label, Func<TEffect, List<string>> colors) : EffectParameter(label)
    where TEffect : LightEffect
{
    /// <inheritdoc />
    public override Control CreateEditor(LightEffect effect, InspectorFields fields) => PaletteEditor.Create(fields.Context, colors((TEffect)effect));
}
