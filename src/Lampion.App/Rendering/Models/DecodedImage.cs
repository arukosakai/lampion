namespace Lampion.App.Rendering.Models;

/// <summary>A texture decoded to straight (not premultiplied) RGBA, 4 bytes per pixel, top row first.</summary>
internal sealed record DecodedImage(int Width, int Height, byte[] Rgba);
