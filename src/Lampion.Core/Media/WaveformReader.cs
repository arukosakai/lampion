using NLayer;

namespace Lampion.Core.Media;

/// <summary>Peak envelope of an audio file, for drawing it on the timeline.</summary>
public sealed record AudioPeaks(float[] Peaks, double PeaksPerSecond, double Duration);

public static class WaveformReader
{
    public const double DefaultPeaksPerSecond = 200;

    /// <summary>WAV and MP3 are decoded in managed code; anything else returns null and plays without a waveform.</summary>
    public static AudioPeaks? TryRead(string path, double peaksPerSecond = DefaultPeaksPerSecond)
    {
        try
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".wav" or ".wave" => ReadWav(path, peaksPerSecond),
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

    private static AudioPeaks ReadWav(string path, double peaksPerSecond)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (new string(reader.ReadChars(4)) != "RIFF")
            throw new InvalidDataException("Not a RIFF file.");
        reader.ReadInt32();
        if (new string(reader.ReadChars(4)) != "WAVE")
            throw new InvalidDataException("Not a WAVE file.");

        int format = 0, channels = 0, rate = 0, bits = 0;
        while (stream.Position + 8 <= stream.Length)
        {
            var id = new string(reader.ReadChars(4));
            var size = reader.ReadInt32();
            var next = stream.Position + size + (size & 1);
            if (id == "fmt ")
            {
                format = reader.ReadInt16();
                channels = reader.ReadInt16();
                rate = reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt16();
                bits = reader.ReadInt16();
                // WAVE_FORMAT_EXTENSIBLE keeps the real format in the sub-format GUID's first two bytes.
                if (format == 0xFFFE && size >= 26)
                {
                    reader.ReadInt16();
                    reader.ReadInt16();
                    reader.ReadInt32();
                    format = reader.ReadInt16();
                }
            }
            else if (id == "data")
            {
                if (channels <= 0 || rate <= 0)
                    throw new InvalidDataException("WAV data before fmt chunk.");
                var builder = new PeakBuilder(rate, channels, peaksPerSecond);
                var bytesPerSample = bits / 8;
                var frameBytes = bytesPerSample * channels;
                var remaining = Math.Min(size, stream.Length - stream.Position);
                var raw = new byte[Math.Max(frameBytes, 65536 / frameBytes * frameBytes)];
                var samples = new float[raw.Length / bytesPerSample];
                while (remaining >= frameBytes)
                {
                    var want = (int)Math.Min(raw.Length, remaining / frameBytes * frameBytes);
                    var got = stream.ReadAtLeast(raw.AsSpan(0, want), want, throwOnEndOfStream: false);
                    if (got < frameBytes)
                        break;
                    got -= got % frameBytes;
                    var n = got / bytesPerSample;
                    for (var i = 0; i < n; i++)
                        samples[i] = Decode(raw, i * bytesPerSample, format, bits);
                    builder.Add(samples.AsSpan(0, n));
                    remaining -= got;
                }
                return builder.Build();
            }
            stream.Position = next;
        }
        throw new InvalidDataException("WAV file has no data chunk.");
    }

    private static float Decode(byte[] b, int o, int format, int bits) =>
        (format, bits) switch
        {
            (3, 32) => BitConverter.ToSingle(b, o),
            (_, 8) => (b[o] - 128) / 128f,
            (_, 16) => BitConverter.ToInt16(b, o) / 32768f,
            (_, 24) => ((b[o] << 8) | (b[o + 1] << 16) | (b[o + 2] << 24)) / 2147483648f,
            (_, 32) => BitConverter.ToInt32(b, o) / 2147483648f,
            _ => throw new NotSupportedException($"{bits}-bit WAV (format {format}) is not supported."),
        };

    private sealed class PeakBuilder(int sampleRate, int channels, double peaksPerSecond)
    {
        private readonly List<float> _peaks = [];
        private readonly int _framesPerPeak = Math.Max(1, (int)(sampleRate / peaksPerSecond));
        private int _frameInPeak;
        private int _channel;
        private float _current;
        private long _frames;

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

        public AudioPeaks Build()
        {
            if (_frameInPeak > 0)
                _peaks.Add(Math.Min(1, _current));
            return new AudioPeaks([.. _peaks], sampleRate / (double)_framesPerPeak, _frames / (double)sampleRate);
        }
    }
}
