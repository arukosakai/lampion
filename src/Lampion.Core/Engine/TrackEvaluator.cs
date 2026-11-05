using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Engine;

/// <summary>One fixture's contribution from one track, compositing overlapping clips and transitions. Pure and allocation-free.</summary>
internal static class TrackEvaluator
{
    /// <summary>Overwrites <paramref name="state"/> with the track's light for fixture <paramref name="index"/> of <paramref name="count"/>.</summary>
    /// <param name="track">A light track.</param>
    /// <param name="time">Show time in seconds.</param>
    /// <param name="index">The fixture's place in the track order.</param>
    /// <param name="count">Number of fixtures the track drives.</param>
    /// <param name="state">Receives the composited state.</param>
    /// <returns>The track's coverage of this fixture, 0..1; 0 when no clip is active.</returns>
    public static double Evaluate(Track track, double time, int index, int count, ref FixtureState state)
    {
        state = default;
        var alpha = 0.0;
        var clips = track.Clips;
        FixtureState layer = default;
        for (var c = 0; c < clips.Count; c++)
        {
            if (clips[c] is not LightClip clip)
                continue;
            if (!TransitionEvaluator.TryEvaluate(clip, clips, time, index, count, ref layer, out var layerAlpha) || layerAlpha <= 0)
                continue;
            // Porter-Duff "over" on premultiplied light, so a half-faded clip on an empty track is half as bright.
            var outAlpha = layerAlpha + alpha * (1 - layerAlpha);
            state.Scale(alpha * (1 - layerAlpha));
            layer.Scale(layerAlpha);
            state.Blend(in layer, 1, BlendMode.Add);
            state.Scale(outAlpha > 0 ? 1 / outAlpha : 0);
            alpha = outAlpha;
        }
        return alpha;
    }
}
