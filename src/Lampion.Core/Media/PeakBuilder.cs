namespace Lampion.Core.Media;

/// <summary>Accumulates interleaved samples into <see cref="AudioPeaks"/>.</summary>
/// <param name="sampleRate">Frames per second of the source.</param>
/// <param name="channels">Interleaved channels per frame.</param>
/// <param name="peaksPerSecond">Requested bucket rate; the actual rate is rounded to whole frames per bucket.</param>
internal sealed class PeakBuilder(int sampleRate, int channels, double peaksPerSecond)
{
    private readonly List<float> _peaks = [];
    private readonly int _framesPerPeak = Math.Max(1, (int)(sampleRate / peaksPerSecond));
    private int _frameInPeak;
    private int _channel;
    private float _current;
    private long _frames;

    /// <summary>Adds interleaved samples in -1..1; a buffer may end mid-frame.</summary>
    public void Add(ReadOnlySpan<float> interleaved)
    {
        foreach (var s in interleaved)
        {
            _current = Math.Max(_current, Math.Abs(s));
            if (++_channel < channels)
                continue;
            _channel = 0;
            _frames++;
            if (++_frameInPeak < _framesPerPeak)
                continue;
            _peaks.Add(Math.Min(1, _current));
            _current = 0;
            _frameInPeak = 0;
        }
    }

    /// <summary>The envelope so far, including a final partial bucket.</summary>
    public AudioPeaks Build()
    {
        if (_frameInPeak > 0)
            _peaks.Add(Math.Min(1, _current));
        return new AudioPeaks([.. _peaks], sampleRate / (double)_framesPerPeak, _frames / (double)sampleRate);
    }
}
