using Lampion.Core.Model.Stage;

namespace Lampion.Core.Meshes;

/// <summary>
/// Where a show keeps its imported models: a "&lt;Show&gt; Assets" folder next to the .lampion file, with models as
/// self-contained .glb files under <see cref="ModelsFolder"/>. Projects store paths relative to that folder, so a
/// show folder can be moved or copied to another machine whole.
/// </summary>
public static class ModelAssets
{
    /// <summary>Sub-folder of the assets folder that holds models.</summary>
    public const string ModelsFolder = "Models";

    // Flat planes have no depth; a sliver keeps them pickable and scalable.
    private const double SmallestSide = 0.01;

    /// <summary>The assets folder of the show saved at <paramref name="projectPath"/>.</summary>
    public static string AssetsDirectory(string projectPath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(projectPath)) ?? ".", Path.GetFileNameWithoutExtension(projectPath) + " Assets");

    /// <summary>The file a stage object's <see cref="StageObject.Model"/> path means, for the show saved at <paramref name="projectPath"/>.</summary>
    public static string Resolve(string projectPath, string model) =>
        Path.IsPathFullyQualified(model) ? model : Path.GetFullPath(Path.Combine(AssetsDirectory(projectPath), model));

    /// <summary>
    /// Reads the model at <paramref name="sourcePath"/>, puts it in metres, centres it and writes it as a .glb into the
    /// assets folder of the show at <paramref name="projectPath"/>, under a name that doesn't clash with earlier imports.
    /// Slow for big models; call it off the UI thread.
    /// </summary>
    /// <exception cref="IOException">The model can't be read or the assets folder can't be written.</exception>
    /// <exception cref="InvalidDataException">The model is damaged, has no triangles, or isn't a format Lampion reads.</exception>
    public static ModelImportResult Import(string sourcePath, string projectPath)
    {
        var model = MeshImport.Read(sourcePath);
        var unitNote = MeshImport.Normalize(model);
        var folder = Path.Combine(AssetsDirectory(projectPath), ModelsFolder);
        Directory.CreateDirectory(folder);
        var name = UniqueName(folder, SafeName(Path.GetFileNameWithoutExtension(sourcePath)));
        GlbWriter.Write(model, Path.Combine(folder, name));
        var size = model.Size();
        return new ModelImportResult(
            $"{ModelsFolder}/{name}",
            new Vec3(Math.Max(SmallestSide, size.X), Math.Max(SmallestSide, size.Y), Math.Max(SmallestSide, size.Z)),
            model.TriangleCount,
            model.Warnings,
            unitNote);
    }

    /// <summary>
    /// Copies the assets folder of the show at <paramref name="fromProject"/> to where the show at
    /// <paramref name="toProject"/> looks for it, e.g. on Save As; files already there are overwritten.
    /// Nothing happens when the two are the same folder or there are no assets.
    /// </summary>
    /// <returns>How many files were copied.</returns>
    /// <exception cref="IOException">A file can't be copied.</exception>
    public static int CopyAssets(string fromProject, string toProject)
    {
        var from = AssetsDirectory(fromProject);
        var to = AssetsDirectory(toProject);
        if (!Directory.Exists(from) || string.Equals(Path.GetFullPath(from), Path.GetFullPath(to), StringComparison.OrdinalIgnoreCase))
            return 0;
        var copied = 0;
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
            copied++;
        }
        return copied;
    }

    private static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray()).Trim();
        return safe.Length == 0 ? "Model" : safe;
    }

    private static string UniqueName(string folder, string name)
    {
        var candidate = name + ".glb";
        for (var n = 2; File.Exists(Path.Combine(folder, candidate)); n++)
            candidate = $"{name} {n}.glb";
        return candidate;
    }
}
