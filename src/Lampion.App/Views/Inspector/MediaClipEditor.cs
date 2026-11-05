using Avalonia;
using Avalonia.Controls;
using Lampion.App.Controls;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views.Inspector;

/// <summary>Effect Controls for an audio or video clip: timing, in point and volume.</summary>
/// <param name="fields">Editor factory wired to the inspector.</param>
internal sealed class MediaClipEditor(InspectorFields fields)
{
    private const double NoSourceLimit = 36000;

    private static readonly ScrubRange StartRange = new(0, NoSourceLimit, 0.02, Unit: " s");
    private static readonly ScrubRange VolumeRange = new(0, 2, 0.005, 100, "0", "%");

    /// <summary>Adds the sections for <paramref name="clip"/> to <paramref name="root"/>.</summary>
    public void Build(Panel root, MediaClip clip)
    {
        InspectorLayout.Title(root, clip.Name, Path.GetFileName(clip.Path));
        var limit = clip.SourceDuration ?? NoSourceLimit;
        var section = InspectorLayout.Section(root, "media", "Clip");
        InspectorLayout.Row(section, "Name", fields.Text(clip.Name, v => clip.Name = v));
        InspectorLayout.Row(section, "Start", fields.Scrub(clip.Start, StartRange, v => clip.Start = Math.Max(0, v)));
        InspectorLayout.Row(section, "Duration", fields.Scrub(clip.Duration, new ScrubRange(0.05, limit, 0.02, Unit: " s"), v => clip.Duration = Math.Max(0.05, v)));
        InspectorLayout.Row(section, "In point", fields.Scrub(clip.MediaOffset, new ScrubRange(0, limit, 0.02, Unit: " s"), v => clip.MediaOffset = Math.Max(0, v)), "How far into the file the clip starts");

        var audio = InspectorLayout.Section(root, "media-audio", "Audio");
        InspectorLayout.Row(audio, "Volume", fields.Scrub(clip.Volume, VolumeRange, v => clip.Volume = v));
        if (clip.SourceDuration is { } source)
            audio.Body.Children.Add(InspectorLayout.Hint($"Source is {source:0.00} s long", new Thickness(20, 4, 0, 0), wrap: false));
    }
}
