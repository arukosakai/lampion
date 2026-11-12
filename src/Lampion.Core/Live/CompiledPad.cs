using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Live;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Live;

/// <summary>A <see cref="LivePad"/> ready for the engine. The effect is the patch's private copy, only the engine samples it.</summary>
/// <param name="Effect">The look.</param>
/// <param name="Mode">Toggle or flash.</param>
/// <param name="Blend">How it combines with what's below.</param>
/// <param name="Fixtures">Rig fixture indices in the order the effect walks them.</param>
internal readonly record struct CompiledPad(Effect Effect, PadMode Mode, BlendMode Blend, int[] Fixtures);
