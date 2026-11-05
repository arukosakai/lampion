using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Lampion.App.Controls;

namespace Lampion.App.Views.Inspector;

/// <summary>
/// Editors for static (non-animated) properties, wired so every change goes through
/// <see cref="InspectorContext.Edit(Action, bool)"/>: live while dragging, one undo step when done.
/// </summary>
/// <param name="context">The inspector the editors write through.</param>
internal sealed class InspectorFields(InspectorContext context)
{
    /// <summary>The context the editors write through.</summary>
    public InspectorContext Context { get; } = context;

    /// <summary>A number that scrubs live and commits on release.</summary>
    /// <param name="value">Initial value, in stored units.</param>
    /// <param name="range">Limits, drag speed and display.</param>
    /// <param name="set">Writes the (already clamped) value to the model.</param>
    public ScrubValue Scrub(double value, ScrubRange range, Action<double> set)
    {
        var scrub = new ScrubValue(value, range);
        scrub.Changing += v => Context.Edit(set, v, commit: false);
        scrub.Committed += v => Context.Edit(set, v);
        return scrub;
    }

    /// <summary>A compact text box that commits on Enter or when it loses focus.</summary>
    public TextBox Text(string value, Action<string> set)
    {
        var box = Ui.Text(value, v => Context.Edit(set, v));
        box.MinHeight = 22;
        box.Padding = new Thickness(4, 1);
        return box;
    }

    /// <summary>A check box that commits each toggle.</summary>
    public CheckBox Check(bool value, Action<bool> set)
    {
        var box = new CheckBox { IsChecked = value, MinHeight = 22 };
        box.IsCheckedChanged += (_, _) => Context.Edit(set, box.IsChecked == true);
        return box;
    }

    /// <summary>A colour swatch that previews live and commits when its flyout closes.</summary>
    /// <param name="hex">Initial colour, <c>#RRGGBB</c>.</param>
    /// <param name="set">Writes the picked colour, <c>#RRGGBB</c>.</param>
    public Button Color(string hex, Action<string> set) => Ui.ColorPicker(hex, h => Context.Edit(set, h, commit: false), () => Context.State.Commit());

    /// <summary>A compact drop-down that commits each pick.</summary>
    /// <param name="items">The choices, in display order.</param>
    /// <param name="selected">The current choice.</param>
    /// <param name="set">Writes the picked item.</param>
    /// <param name="display">Item text; null uses <see cref="object.ToString"/>.</param>
    public Control Choice<T>(IEnumerable<T> items, T selected, Action<T> set, Func<T, string>? display = null) =>
        Compact(Ui.Combo(items, selected, v => Context.Edit(set, v), display));

    /// <summary>Left-aligns <paramref name="control"/> at the inspector's compact size, so it doesn't stretch across the row.</summary>
    public static Control Compact(Control control)
    {
        control.HorizontalAlignment = HorizontalAlignment.Left;
        control.MinWidth = 130;
        if (control is ComboBox combo)
            combo.MinHeight = 22;
        return control;
    }
}
