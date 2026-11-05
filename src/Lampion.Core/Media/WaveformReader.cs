using NLayer;

namespace Lampion.Core.Media;

/// <summary>Decodes audio files into a peak envelope for the timeline. Blocking I/O: call it off the UI thread.</summary>
public static class WaveformReader
{
    /// <summary>Default bucket rate, enough for the timeline's closest zoom.</summary>
    public const double DefaultPeaksPerSecond = 200;

    /// <summary>WAV and MP3 are decoded in managed code; anything else, or a file that can't be decoded, returns null and plays without a waveform.</summary>
    public static AudioPeaks? TryRead(string path, double peaksPerSecond = DefaultPeaksPerSecond)
    {
        try
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".wav" or ".wave" => WavPeakReader.Read(path, peaksPerSecond),
                ".mp3" => ReadMp3(path, peaksPerSecond),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or NotSupportedException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    private static AudioPeaks ReadMp3(string path, double peaksPerSecond)
    {
        using var mp3 = new MpegFile(path);
        var builder = new PeakBuilder(mp3.SampleRate, mp3.Channels, peaksPerSecond);
        var buffer = new float[16384];
        int read;
        while ((read = mp3.ReadSamples(buffer, 0, buffer.Length)) > 0)
            builder.Add(buffer.AsSpan(0, read));
        return builder.Build();
    }
}
