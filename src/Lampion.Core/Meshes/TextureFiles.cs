namespace Lampion.Core.Meshes;

/// <summary>Finds and loads colour textures referenced by model files, which often carry paths from another machine.</summary>
internal static class TextureFiles
{
    /// <summary>
    /// The file <paramref name="reference"/> points at: as written relative to <paramref name="dir"/>, else by name next
    /// to the material or the model in <paramref name="modelDir"/>; null when it is nowhere.
    /// </summary>
    public static string? Find(string dir, string modelDir, string reference)
    {
        var cleaned = reference.Trim().Trim('"').Replace('\\', '/');
        var name = Path.GetFileName(cleaned);
        string[] candidates = [Path.Combine(dir, cleaned), Path.Combine(dir, name), Path.Combine(modelDir, name)];
        return Array.Find(candidates, File.Exists);
    }

    /// <summary>The bytes of a PNG or JPEG texture, or null with a warning when it's missing or in another format.</summary>
    public static byte[]? Load(string dir, string modelDir, string reference, List<string> warnings)
    {
        if (Find(dir, modelDir, reference) is not { } path)
        {
            warnings.Add($"Texture \"{reference}\" wasn't found; that surface is drawn in its plain colour.");
            return null;
        }
        var bytes = File.ReadAllBytes(path);
        if (IsPngOrJpeg(bytes))
            return bytes;
        warnings.Add($"Texture \"{Path.GetFileName(path)}\" isn't PNG or JPEG; convert it to use it.");
        return null;
    }

    /// <summary>True for the PNG file signature or the JPEG start-of-image marker.</summary>
    public static bool IsPngOrJpeg(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G']) || bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]);
}
