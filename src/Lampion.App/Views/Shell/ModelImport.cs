using Avalonia.Controls;
using Lampion.App.Editor;
using Lampion.Core.Meshes;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Views.Shell;

/// <summary>
/// Stage → Import 3D model, and model files dropped onto the window: copies each into the show's assets folder
/// (saving the show first if it has never been saved) and stands it on the stage as one undo step.
/// </summary>
internal sealed class ModelImport
{
    // Past this, the 3D view starts to struggle on modest GPUs; worth a word but not a refusal.
    private const int HeavyTriangles = 500_000;
    private const double Spacing = 0.5;
    private const string ModelColor = "#5A5A64";

    private readonly Window _owner;
    private readonly EditorState _state;
    private readonly FileOperations _files;
    private readonly StatusBar _status;

    /// <summary>Imports into <paramref name="state"/>, saving through <paramref name="files"/> and reporting to <paramref name="status"/>.</summary>
    public ModelImport(Window owner, EditorState state, FileOperations files, StatusBar status)
    {
        _owner = owner;
        _state = state;
        _files = files;
        _status = status;
    }

    /// <summary>Asks for a model file and imports it.</summary>
    public async Task Import()
    {
        var patterns = MeshImport.Extensions.Select(e => "*" + e).ToArray();
        if (await Dialogs.PickOpen(_owner, "Import 3D model", ("3D model (glTF, OBJ)", patterns)) is { } path)
            await Import([path]);
    }

    /// <summary>Imports every model file in <paramref name="paths"/> side by side at the centre of the stage, selecting them.</summary>
    /// <param name="paths">Files to import; ones that aren't glTF or OBJ are skipped.</param>
    /// <param name="at">Where to stand the first one (its X and Z; it sits on the floor); the stage centre when null.</param>
    public async Task Import(IReadOnlyList<string> paths, Vec3? at = null)
    {
        var models = paths.Where(MeshImport.CanImport).ToList();
        if (models.Count == 0 || await ShowPath() is not { } show)
            return;

        var added = new List<StageObject>();
        var notes = new List<string>();
        var x = at?.X ?? 0;
        foreach (var path in models)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            _status.Message = $"Importing {name}…";
            try
            {
                var result = await Task.Run(() => ModelAssets.Import(path, show));
                var obj = new StageObject
                {
                    Name = name,
                    Kind = StageObjectKind.Model,
                    Model = result.Model,
                    Size = result.Size,
                    Color = ModelColor,
                    Position = new Vec3(x + result.Size.X / 2, result.Size.Y / 2, at?.Z ?? 0),
                };
                x += result.Size.X + Spacing;
                added.Add(obj);
                notes.AddRange(Notes(name, result));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                notes.Add($"{name} couldn't be imported: {ex.Message}");
            }
        }

        if (added.Count > 0)
        {
            // Lay the row out centred on where it was asked for rather than starting there.
            var shift = new Vec3(-(x - Spacing - (at?.X ?? 0)) / 2, 0, 0);
            var objects = _state.Project.Stage.Objects;
            foreach (var obj in added)
            {
                obj.Position += shift;
                obj.Position = obj.Position with { Y = SurfaceBelow(objects, obj.Position) + obj.Size.Y / 2 };
                objects.Add(obj);
            }
            _state.Commit();
            _state.SelectObjects(added.Select(o => o.Id));
        }
        if (added.Count > 0)
            notes.Insert(0, Summary(added));
        _status.Message = string.Join(" ", notes);
    }

    // Models are copied next to the show file, so a show that was never saved has nowhere to put them yet.
    private async Task<string?> ShowPath()
    {
        if (_state.FilePath is { } path)
            return path;
        var choice = await Dialogs.Ask(_owner, "Save the show first", "Imported models are kept in a folder next to the show file, so the show needs to be saved before a model can be added.", "Save…", "Cancel");
        return choice == "Save…" && await _files.Save(saveAs: false) ? _state.FilePath : null;
    }

    // Stand it on whatever is there (a deck, a riser) rather than inside it; the floor when there's nothing.
    private static double SurfaceBelow(IReadOnlyList<StageObject> objects, Vec3 at)
    {
        const double above = 100;
        return StageMath.Raycast(objects, at with { Y = above }, new Vec3(0, -1, 0)) is { } hit ? above - hit : 0;
    }

    private static IEnumerable<string> Notes(string name, ModelImportResult result)
    {
        if (result.UnitNote is { } units)
            yield return $"{name}: {units}";
        if (result.Triangles > HeavyTriangles)
            yield return $"{name} has {result.Triangles:N0} triangles and may slow the 3D view down.";
        foreach (var warning in result.Warnings.Take(2))
            yield return $"{name}: {warning}";
        if (result.Warnings.Count > 2)
            yield return $"({result.Warnings.Count - 2} more problems with {name}.)";
    }

    private static string Summary(List<StageObject> added) =>
        added.Count == 1 ? $"Imported {added[0].Name}; resize it in Effect Controls." : $"Imported {added.Count} models; resize them in Effect Controls.";
}
