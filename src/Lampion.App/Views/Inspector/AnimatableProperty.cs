using Avalonia.Controls;
using Avalonia.Layout;
using Lampion.App.Controls;
using Lampion.App.Controls.Icons;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views.Inspector;

/// <summary>
/// An automation-lane property row with the After Effects stopwatch and keyframe navigator
/// (previous, key, next). With the stopwatch on, changing the value at the playhead sets a keyframe there.
/// Tracks itself with the context, so the value and the key diamond follow the playhead without a rebuild.
/// </summary>
internal sealed class AnimatableProperty : IInspectorRefreshable
{
    private readonly LaneBinding _binding;
    private readonly ScrubValue _scrub;
    private readonly LucideIcon? _diamond;
    private bool _keyShown;

    /// <summary>Builds the row and registers it with <paramref name="context"/> for playhead refreshes.</summary>
    /// <param name="context">The inspector the row belongs to.</param>
    /// <param name="clip">The clip whose lane the row edits.</param>
    /// <param name="key">Which lane.</param>
    /// <param name="name">Row label.</param>
    /// <param name="range">Limits, drag speed and display of the value.</param>
    /// <param name="removable">Adds an X to delete the lane, for channel lanes the user added.</param>
    /// <param name="tip">Optional tooltip on the label.</param>
    /// <param name="parameter">The effect parameter behind an <see cref="AutomationTarget.EffectParameter"/> key, whose static value lives on the effect.</param>
    /// <param name="staticEditor">Shown instead of the plain scrub while the stopwatch is off, e.g. a tempo row with tap and BPM.</param>
    public AnimatableProperty(
        InspectorContext context,
        LightClip clip,
        LaneKey key,
        string name,
        ScrubRange range,
        bool removable = false,
        string? tip = null,
        AnimatableParameter? parameter = null,
        Control? staticEditor = null
    )
    {
        _binding = new LaneBinding(context.State, clip, key, parameter);
        var animated = _binding.IsAnimated;

        Action<double> set = _binding.Set;
        _scrub = new ScrubValue(_binding.Current, range);
        _scrub.Changing += v => context.Edit(set, v, commit: false);
        _scrub.Committed += v => context.Edit(set, v);

        var stopwatch = new GlyphButton(
            Lucide.Timer,
            animated ? "Stop animating — keyframes are removed, the current value stays" : "Animate this property: keyframes go at the playhead",
            () =>
            {
                context.Edit(() => _binding.ToggleStopwatch(keepLane: removable));
                context.Rebuild();
            },
            active: animated
        );

        var navigator = new StackPanel { Orientation = Orientation.Horizontal };
        if (animated)
        {
            _keyShown = _binding.KeyAtPlayhead() is not null;
            _diamond = new LucideIcon(Lucide.Diamond, 13) { Filled = _keyShown };
            var diamondButton = new GlyphButton("", "Add or remove a keyframe at the playhead (right-click: curve)", () =>
            {
                context.Edit(_binding.ToggleKeyAtPlayhead);
                context.Rebuild();
            }, active: true) { Content = _diamond };
            diamondButton.ContextMenu = CurveMenu(context);
            navigator.Children.Add(new GlyphButton(Lucide.ChevronLeft, "Previous keyframe", () => _binding.Jump(forward: false)));
            navigator.Children.Add(diamondButton);
            navigator.Children.Add(new GlyphButton(Lucide.ChevronRight, "Next keyframe", () => _binding.Jump(forward: true)));
        }
        if (removable)
            navigator.Children.Add(new GlyphButton(Lucide.X, "Stop driving this channel", () =>
            {
                context.Edit(_binding.Remove);
                context.Rebuild();
            }));

        Row = new PropertyRow(name, animated ? _scrub : staticEditor ?? _scrub, stopwatch, navigator, tip);
        context.Track(this);
    }

    /// <summary>The row to add to a section.</summary>
    public PropertyRow Row { get; }

    /// <inheritdoc />
    public void Refresh()
    {
        _scrub.Show(_binding.Current);
        if (_diamond is null)
            return;
        var keyHere = _binding.KeyAtPlayhead() is not null;
        if (keyHere == _keyShown)
            return;
        _keyShown = keyHere;
        _diamond.Filled = keyHere;
    }

    private ContextMenu CurveMenu(InspectorContext context)
    {
        var menu = new ContextMenu();
        foreach (var easing in Enum.GetValues<Easing>())
        {
            var item = new MenuItem { Header = $"{DisplayNames.Humanize(easing.ToString())} to next key" };
            item.Click += (_, _) =>
            {
                if (_binding.KeyAtPlayhead() is { } key)
                    context.Edit(() => key.Easing = easing);
            };
            menu.Items.Add(item);
        }
        return menu;
    }
}
