using Avalonia.Controls;
using Lampion.App.Controls;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views.Inspector;

/// <summary>A light clip's "Transition in" section: the kind (or a hard cut), its duration and curve.</summary>
/// <param name="fields">Editor factory wired to the inspector.</param>
internal sealed class TransitionSection(InspectorFields fields)
{
    private static readonly ScrubRange DurationRange = new(0.05, 60, 0.02, Unit: " s");
    private static readonly TransitionKind?[] Kinds = [null, .. Enum.GetValues<TransitionKind>().Select(k => (TransitionKind?)k)];

    private InspectorContext Context => fields.Context;

    /// <summary>Adds the section for <paramref name="clip"/> to <paramref name="root"/>; it starts open only when there is a transition.</summary>
    public void Build(Panel root, LightClip clip)
    {
        var title = clip.TransitionIn is { } t ? $"Transition in · {DisplayNames.Spaced(t.Kind)}" : "Transition in";
        var section = InspectorLayout.Section(root, "transition", title, open: clip.TransitionIn is not null);
        var type = Ui.Combo(Kinds, clip.TransitionIn?.Kind, k => SetKind(clip, k), k => k is { } kind ? DisplayNames.Spaced(kind) : "Cut (none)");
        InspectorLayout.Row(section, "Type", InspectorFields.Compact(type), "Blends from the clip ending where this one starts. Drag one from the Effects panel onto a clip too.");
        if (clip.TransitionIn is not { } transition)
            return;
        InspectorLayout.Row(section, "Duration", fields.Scrub(transition.Duration, DurationRange, v => transition.Duration = Math.Max(0.05, v)));
        InspectorLayout.Row(section, "Curve", fields.Choice(Enum.GetValues<Easing>(), transition.Easing, v => transition.Easing = v));
    }

    private void SetKind(LightClip clip, TransitionKind? kind)
    {
        Context.Edit(() =>
        {
            if (kind is null)
                clip.TransitionIn = null;
            else if (clip.TransitionIn is { } existing)
                existing.Kind = kind.Value;
            else
                clip.TransitionIn = new Transition { Kind = kind.Value, Duration = Math.Min(clip.Duration, Context.State.SecondsPerBeat * 2) };
        });
        Context.Rebuild();
    }
}
