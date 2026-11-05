using Avalonia.Controls;
using LightEffect = Lampion.Core.Model.Effects.Effect;

namespace Lampion.App.Views.Inspector;

/// <summary>
/// One editable parameter of an effect type: its label and how to build an editor bound to an instance.
/// Descriptors are stateless and shared; see <see cref="EffectParameterTable"/>.
/// </summary>
/// <param name="label">Row label.</param>
internal abstract class EffectParameter(string label)
{
    /// <summary>Row label, e.g. "Step time".</summary>
    public string Label { get; } = label;

    /// <summary>Builds an editor that writes into <paramref name="effect"/>.</summary>
    /// <param name="effect">An instance of the effect type this descriptor was declared for.</param>
    /// <param name="fields">Editor factory wired to the inspector.</param>
    /// <exception cref="InvalidCastException"><paramref name="effect"/> is of another type.</exception>
    public abstract Control CreateEditor(LightEffect effect, InspectorFields fields);
}
