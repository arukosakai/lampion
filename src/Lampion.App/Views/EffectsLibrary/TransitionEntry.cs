using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views.EffectsLibrary;

/// <summary>A transition as listed in the library's Transitions folder.</summary>
/// <param name="Kind">The transition it applies.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">One-line tooltip.</param>
internal readonly record struct TransitionEntry(TransitionKind Kind, string Name, string Description)
{
    /// <summary>Every transition, in library order.</summary>
    public static IReadOnlyList<TransitionEntry> All { get; } =
    [
        new(TransitionKind.Crossfade, "Crossfade", "Blend straight from one clip into the next"),
        new(TransitionKind.DipToBlack, "Dip to black", "Fade out, then the next clip fades in"),
        new(TransitionKind.Wipe, "Wipe", "The next clip sweeps across the fixtures in track order"),
    ];
}
