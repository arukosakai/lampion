namespace Lampion.Core.Media;

/// <summary>Reads the peak envelope of a RIFF/WAVE file (PCM 8/16/24/32-bit and 32-bit float) in managed code.</summary>
internal static class WavPeakReader
{
    /// <summary>The file's peaks.</summary>
    /// <exception cref="InvalidDataException">Not a WAVE file, or no fmt/data chunk.</exception>
    /// <exception cref="NotSupportedException">An unsupported sample format.</exception>
    /// <exception cref="IOException">The file can't be read.</exception>
    public static AudioPeaks Read(string path, double peaksPerSecond)
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
                return ReadData(stream, size, format, channels, rate, bits, peaksPerSecond);
            }
            stream.Position = next;
        }
        throw new InvalidDataException("WAV file has no data chunk.");
    }

    private static AudioPeaks ReadData(Stream stream, int size, int format, int channels, int rate, int bits, double peaksPerSecond)
    {
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
}
