namespace Lampion.App.Views.Inspector;

/// <summary>
/// A piece of the inspector that shows something time-dependent (a value at the playhead, a keyframe
/// indicator, live DMX). Registered with <see cref="InspectorContext.Track"/> and refreshed on every
/// playhead change, so <see cref="Refresh"/> must not allocate and must not rebuild controls.
/// </summary>
internal interface IInspectorRefreshable
{
    /// <summary>Re-reads the model at the current playhead and updates the shown values if they changed.</summary>
    void Refresh();
}
