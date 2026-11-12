namespace Lampion.Core.Live;

/// <summary>
/// What the live engine should be doing: the rig and console, and the show playing on it (if any). Swapped as a
/// whole so the engine never sees a cue mapped for a different patch.
/// </summary>
/// <param name="Patch">The compiled rig and console.</param>
/// <param name="Cue">The show to play, mapped onto <paramref name="Patch"/>; null for console only.</param>
public sealed record LiveScene(LivePatch Patch, ShowCue? Cue);
