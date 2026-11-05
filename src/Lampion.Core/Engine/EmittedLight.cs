using Lampion.Core.Color;

namespace Lampion.Core.Engine;

/// <summary>What the preview draws for one fixture, decoded from its DMX.</summary>
/// <param name="Color">Visible light, channels 0..1, dimmer applied.</param>
/// <param name="Pan">0..1 with 0.5 at centre (0.5 when the fixture has no pan).</param>
/// <param name="Tilt">0..1 with 0.5 at centre (0.5 when the fixture has no tilt).</param>
/// <param name="Strobe">Strobe channel level 0..1; 0 when absent.</param>
/// <param name="Zoom">Zoom channel level 0..1; 0.5 when absent.</param>
public readonly record struct EmittedLight(Rgb Color, double Pan, double Tilt, double Strobe, double Zoom);
