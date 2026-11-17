using System.Runtime.CompilerServices;

namespace Lampion.Core.Model.Effects;

internal partial struct EffectParameterValues
{
    [InlineArray(Capacity)]
    private struct Buffer
    {
        private double _element;
    }
}
