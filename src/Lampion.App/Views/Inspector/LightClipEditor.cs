using Avalonia.Controls;
using Lampion.App.Controls;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views.Inspector;

/// <summary>Effect Controls for a light clip: clip timing and fades, animatable light properties, the effect, its transition and channel lanes.</summary>
internal sealed class LightClipEditor
{
    private static readonly ScrubRange StartRange = new(0, 36000, 0.02, Unit: " s");
    private static readonly ScrubRange DurationRange = new(0.05, 36000, 0.02, Unit: " s");
    private static readonly ScrubRange FadeRange = new(0, 3600, 0.02, Unit: " s");
    private static readonly ScrubRange IntensityRange = new(0, 4, 0.005, 100, "0", "%");
    private static readonly ScrubRange HueRange = new(-100, 100, 1 / 360.0, 360, "0", "°");
    private static readonly ScrubRange SaturationRange = new(0, 2, 0.005, 100, "0", "%");

    private readonly InspectorFields _fields;
    private readonly EffectSection _effect;
    private readonly TransitionSection _transition;
    private readonly ChannelLanesSection _channels;

    /// <summary>Creates the editor and its section components.</summary>
    /// <param name="fields">Editor factory wired to the inspector.</param>
    public LightClipEditor(InspectorFields fields)
    {
        _fields = fields;
        _effect = new EffectSection(fields);
        _transition = new TransitionSection(fields);
        _channels = new ChannelLanesSection(fields.Context);
    }

    /// <summary>Adds every section for <paramref name="clip"/> to <paramref name="root"/>; with several clips selected it shows the first.</summary>
    public void Build(Panel root, LightClip clip)
    {
        var context = _fields.Context;
        var count = context.State.SelectedClipIds.Count;
        var track = context.State.FindClip(clip.Id)?.Track;
        InspectorLayout.Title(
            root,
            $"{track?.Name ?? "Track"} • {clip.Name}",
            count > 1 ? $"{count} clips selected — showing the first" : $"{TimelineControl.Timecode(clip.Start)} – {TimelineControl.Timecode(clip.End)}"
        );

        var timing = InspectorLayout.Section(root, "clip", "Clip");
        InspectorLayout.Row(timing, "Name", _fields.Text(clip.Name, v => clip.Name = v));
        InspectorLayout.Row(timing, "Start", _fields.Scrub(clip.Start, StartRange, v => clip.Start = Math.Max(0, v)));
        InspectorLayout.Row(timing, "Duration", _fields.Scrub(clip.Duration, DurationRange, v => clip.Duration = Math.Max(0.05, v)));
        InspectorLayout.Row(timing, "Fade in", _fields.Scrub(clip.FadeIn, FadeRange, v => clip.FadeIn = Math.Clamp(v, 0, clip.Duration)));
        InspectorLayout.Row(timing, "Fade out", _fields.Scrub(clip.FadeOut, FadeRange, v => clip.FadeOut = Math.Clamp(v, 0, clip.Duration)));
        InspectorLayout.Row(timing, "Fade curve", _fields.Choice(Enum.GetValues<Easing>(), clip.FadeEasing, v => clip.FadeEasing = v));

        var light = InspectorLayout.Section(root, "light", "Light");
        Animatable(light, clip, AutomationTarget.Opacity, "Opacity", ScrubRange.Percent);
        Animatable(light, clip, AutomationTarget.Intensity, "Intensity", IntensityRange);
        Animatable(light, clip, AutomationTarget.HueShift, "Hue shift", HueRange, "Animate 0° → 360° for a rainbow; every full turn is another trip round the wheel");
        Animatable(light, clip, AutomationTarget.Saturation, "Saturation", SaturationRange);
        Animatable(light, clip, AutomationTarget.White, "White", ScrubRange.Percent, "Drives the white LED of fixtures that have one");

        _effect.Build(root, clip);
        _transition.Build(root, clip);
        _channels.Build(root, clip);
    }

    private void Animatable(TwirlSection section, LightClip clip, AutomationTarget target, string name, ScrubRange range, string? tip = null) =>
        section.Body.Children.Add(new AnimatableProperty(_fields.Context, clip, new LaneKey(target), name, range, tip: tip).Row);
}
