using System.Numerics;

namespace Lampion.Core.Meshes;

/// <summary>Turns a model file from elsewhere into Lampion's terms: read whatever format it is, put it in metres and centre it.</summary>
public static class MeshImport
{
    // Anything this big was almost certainly modelled in centimetres or millimetres; no set piece is 40 m wide.
    private const float CentimetreThreshold = 40;
    private const float MillimetreThreshold = 400;

    /// <summary>File extensions that can be imported, lower case with the dot.</summary>
    public static IReadOnlyList<string> Extensions { get; } = [".glb", ".gltf", ".obj"];

    /// <summary>True when <paramref name="path"/> has one of the <see cref="Extensions"/>.</summary>
    public static bool CanImport(string path) => Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>Reads <paramref name="path"/> with the reader for its extension, without changing units or position.</summary>
    /// <exception cref="IOException">The file can't be read.</exception>
    /// <exception cref="InvalidDataException">The file is damaged, or not a format Lampion reads.</exception>
    public static MeshModel Read(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".obj" => ObjReader.Read(path),
            ".gltf" or ".glb" => GltfReader.Read(path),
            var other => throw new InvalidDataException($"{other} files can't be imported; use glTF (.glb, .gltf) or OBJ."),
        };

    /// <summary>
    /// Scales <paramref name="model"/> to metres when its size gives away centimetres or millimetres, and centres its
    /// bounding box on the origin, in place.
    /// </summary>
    /// <returns>What was rescaled, in words for the status bar; null when the model was already in metres.</returns>
    /// <exception cref="InvalidDataException">The model has no triangles.</exception>
    public static string? Normalize(MeshModel model)
    {
        if (model.TriangleCount == 0)
            throw new InvalidDataException("The file has no triangles to show.");
        var size = model.Size();
        var largest = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
        var (scale, unit) = largest > MillimetreThreshold ? (0.001f, "millimetres") : largest > CentimetreThreshold ? (0.01f, "centimetres") : (1f, null);
        var (min, max) = model.Bounds();
        model.Transform(scale, -(min + max) * 0.5f * scale);
        return unit is null ? null : $"It looked like it was modelled in {unit}, so it was scaled to {Metres(largest * scale)} across.";
    }

    private static string Metres(float m) => m.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " m";
}
