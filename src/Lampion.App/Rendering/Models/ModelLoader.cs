using Lampion.Core.Meshes;
using SkiaSharp;

namespace Lampion.App.Rendering.Models;

/// <summary>Reads a show's .glb and decodes its textures, so the render thread only has to upload. Thread-safe.</summary>
internal static class ModelLoader
{
    // Bigger textures are scaled down: a set piece never fills enough of the view to need more, and VRAM is shared with the GPU's real work.
    private const int MaxTextureSide = 2048;

    /// <summary>Loads <paramref name="path"/>; slow for big models, so call it off the render thread.</summary>
    /// <exception cref="IOException">The file can't be read.</exception>
    /// <exception cref="InvalidDataException">The file isn't a model Lampion can read.</exception>
    public static LoadedModel Load(string path)
    {
        var model = MeshImport.Read(path);
        var parts = model.Parts.Select(p => (p, p.Material.BaseColorImage is { } bytes ? Decode(bytes) : null)).ToList();
        return new LoadedModel(model.Size(), parts);
    }

    private static DecodedImage? Decode(byte[] bytes)
    {
        using var source = SKBitmap.Decode(bytes);
        if (source is null)
            return null;
        var scale = Math.Min(1.0, MaxTextureSide / (double)Math.Max(source.Width, source.Height));
        var info = new SKImageInfo(Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale)), SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var target = new SKBitmap(info);
        if (!source.ScalePixels(target, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)))
            return null;
        return new DecodedImage(info.Width, info.Height, target.Bytes);
    }
}
