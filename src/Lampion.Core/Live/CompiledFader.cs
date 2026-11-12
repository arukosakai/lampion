using Lampion.Core.Model.Live;
using Lampion.Core.Model.Rig;

namespace Lampion.Core.Live;

/// <summary>A <see cref="LiveFader"/> with its targets resolved to indices, ready for the engine.</summary>
/// <param name="Target">What it controls.</param>
/// <param name="Role">The role, for <see cref="LiveFaderTarget.Role"/>.</param>
/// <param name="Pad">The pad's position, for pad targets; -1 when missing or not a pad target.</param>
/// <param name="Fixtures">Rig fixture indices, for fixture targets.</param>
internal readonly record struct CompiledFader(LiveFaderTarget Target, ChannelRole Role, int Pad, int[] Fixtures);
