using Avalonia.Controls;
using Lampion.App.Controls;

namespace Lampion.App.Views.Inspector;

/// <summary>Edits a palette in place: one swatch per colour (right-click to remove), and + to add one.</summary>
internal static class PaletteEditor
{
    /// <summary>Builds the editor. Adding or removing a colour rebuilds the inspector; at least one colour always stays.</summary>
    /// <param name="context">The inspector the editor belongs to.</param>
    /// <param name="colors">The effect's palette (<c>#RRGGBB</c>), edited in place.</param>
    public static Control Create(InspectorContext context, List<string> colors)
    {
        var panel = new WrapPanel();
        for (var i = 0; i < colors.Count; i++)
        {
            var index = i;
            var picker = Ui.ColorPicker(colors[i], hex => context.Edit(() => colors[index] = hex, commit: false), () => context.State.Commit());
            var remove = new MenuItem { Header = "Remove colour" };
            remove.Click += (_, _) =>
            {
                if (colors.Count <= 1)
                    return;
                context.Edit(() => colors.RemoveAt(index));
                context.Rebuild();
            };
            picker.ContextMenu = new ContextMenu { Items = { remove } };
            panel.Children.Add(picker);
        }
        panel.Children.Add(new GlyphButton("+", "Add a colour (right-click one to remove it)", () =>
        {
            context.Edit(() => colors.Add(colors.Count > 0 ? colors[^1] : "#FFFFFF"));
            context.Rebuild();
        }));
        return panel;
    }
}
