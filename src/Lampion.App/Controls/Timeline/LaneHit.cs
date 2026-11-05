using Avalonia;
using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

/// <summary>A clip's span on a keyframe row under the pointer, and the key under it if there is one.</summary>
/// <param name="Clip">The clip that owns the lane.</param>
/// <param name="Lane">The lane drawn on the row for that clip.</param>
/// <param name="Key">The keyframe under the pointer, or null.</param>
/// <param name="Bounds">The clip's span on the row, in control coordinates.</param>
internal readonly record struct LaneHit(LightClip Clip, AutomationLane Lane, Keyframe? Key, Rect Bounds);
