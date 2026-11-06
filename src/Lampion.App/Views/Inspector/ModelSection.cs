using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lampion.App.Controls;
using Lampion.Core.Meshes;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Views.Inspector;

/// <summary>
/// Effect Controls for an imported 3D model: which file it is (and whether it is still there), its overall size
/// with proportions kept, and a way back to the size it was modelled at.
/// </summary>
/// <param name="fields">Editor factory wired to the inspector.</param>
internal sealed class ModelSection(InspectorFields fields)
{
    private static readonly ScrubRange OverallRange = new(0.05, 200, 0.01, Unit: " m");

    /// <summary>Adds the section for <paramref name="obj"/>, which must be a <see cref="StageObjectKind.Model"/>, to <paramref name="root"/>.</summary>
    public void Build(Panel root, StageObject obj)
    {
        var context = fields.Context;
        var section = InspectorLayout.Section(root, "model", "3D model");
        var file = File(context.State.FilePath, obj.Model);
        InspectorLayout.Row(section, "File", Ui.Label(Path.GetFileName(obj.Model) ?? "none"), obj.Model);
        InspectorLayout.Row(
            section,
            "Overall size",
            fields.Scrub(Largest(obj.Size), OverallRange, v => obj.Size = obj.Size * (v / Largest(obj.Size))),
            "The longest side; the others follow so it keeps its proportions");

        if (file is null || !System.IO.File.Exists(file))
        {
            section.Body.Children.Add(InspectorLayout.Hint(Missing(context.State.FilePath, obj.Model), new Thickness(20, 2, 0, 0)));
            return;
        }
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(20, 4, 0, 0) };
        var problem = InspectorLayout.Hint("", new Thickness(20, 2, 0, 0));
        problem.IsVisible = false;
        actions.Children.Add(Ui.Button("Natural size", async () =>
        {
            // A button handler can't be awaited, so a read failure has to be caught here or it would end the app.
            try
            {
                var size = await Task.Run(() => GltfReader.Read(file).Size());
                context.Edit(() => obj.Size = new Vec3(Math.Max(0.01, size.X), Math.Max(0.01, size.Y), Math.Max(0.01, size.Z)));
                context.Rebuild();
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                problem.Text = $"Couldn't read the model: {ex.Message}";
                problem.IsVisible = true;
            }
        }, "Back to the size it was modelled at"));
        section.Body.Children.Add(actions);
        section.Body.Children.Add(problem);
    }

    private static double Largest(Vec3 size) => Math.Max(size.X, Math.Max(size.Y, size.Z));

    private static string? File(string? show, string? model) => show is not null && model is not null ? ModelAssets.Resolve(show, model) : null;

    private static string Missing(string? show, string? model) =>
        show is null
            ? "The show isn't saved, so its models folder can't be found; save it next to its Assets folder."
            : $"The model file is missing; it should be {model} in the \"{Path.GetFileName(ModelAssets.AssetsDirectory(show))}\" folder next to the show. A box stands in for it until it's back.";
}
