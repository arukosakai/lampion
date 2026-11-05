using Lampion.Core.Color;

namespace Lampion.Core.Model.Effects;

/// <summary>What an effect asks of one fixture at one instant, before automation and fades.</summary>
/// <param name="Color">Hue and saturation of the light, channels 0..1 (not premultiplied).</param>
/// <param name="Intensity">Brightness multiplier, normally 0..1.</param>
public readonly record struct EffectSample(Rgb Color, double Intensity);
