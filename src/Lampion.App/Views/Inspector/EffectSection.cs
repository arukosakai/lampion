using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Controls;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Timeline;
using LightEffect = Lampion.Core.Model.Effects.Effect;

namespace Lampion.App.Views.Inspector;

/// <summary>The "fx" section of a light clip: change, reset or save the effect, and its parameters.</summary>
/// <param name="fields">Editor factory wired to the inspector.</param>
internal sealed class EffectSection(InspectorFields fields)
{
    private static readonly IBrush ChipBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x40));

    private InspectorContext Context => fields.Context;

    /// <summary>Adds the section for <paramref name="clip"/>'s effect to <paramref name="root"/>.</summary>
    public void Build(Panel root, LightClip clip)
    {
        var effect = clip.Effect;
        var changeButton = new GlyphButton("⇄", "Change effect", () => { }) { Flyout = ChangeMenu(clip) };
        var reset = new GlyphButton("Reset", "Reset the effect's parameters", () => Replace(clip, EffectCatalog.Create(effect.Kind), keepLook: false));
        var save = new GlyphButton("★", "Save this clip's look (effect, fades, light automation) as a preset", () =>
        {
            if (Context.Owner is { } owner)
                _ = PresetActions.SaveFromClip(owner, clip);
        });
        var section = new TwirlSection("effect", Header(effect.DisplayName), true, changeButton, save, reset);
        root.Children.Add(section);

        var parameters = EffectParameterTable.For(effect);
        for (var i = 0; i < parameters.Count; i++)
            InspectorLayout.Row(section, parameters[i].Label, parameters[i].CreateEditor(effect, fields));
    }

    private static StackPanel Header(string name) =>
        new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new Border { Background = ChipBrush, CornerRadius = new CornerRadius(3), Padding = new Thickness(4, 0), Child = new TextBlock { Text = "fx", FontStyle = FontStyle.Italic, FontSize = 11 } },
                new TextBlock { Text = name, FontWeight = FontWeight.SemiBold },
            },
        };

    private MenuFlyout ChangeMenu(LightClip clip)
    {
        var menu = new MenuFlyout();
        foreach (var (kind, name, _) in EffectCatalog.Entries)
        {
            var item = new MenuItem { Header = name };
            item.Click += (_, _) => Replace(clip, EffectCatalog.Create(kind), keepLook: true);
            menu.Items.Add(item);
        }
        return menu;
    }

    private void Replace(LightClip clip, LightEffect replacement, bool keepLook)
    {
        Context.Edit(() => EffectSwap.Replace(clip, replacement, keepLook));
        Context.Rebuild();
    }
}
