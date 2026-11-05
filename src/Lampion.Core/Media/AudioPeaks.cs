namespace Lampion.Core.Media;

/// <summary>Peak envelope of an audio file, for drawing it on the timeline.</summary>
/// <param name="Peaks">Absolute peak per bucket across all channels, 0..1; the caller owns the array.</param>
/// <param name="PeaksPerSecond">Buckets per second of audio.</param>
/// <param name="Duration">Length of the decoded audio in seconds.</param>
public sealed record AudioPeaks(float[] Peaks, double PeaksPerSecond, double Duration);
