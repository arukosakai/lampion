using System.Buffers.Binary;
using NLayer;

namespace Lampion.Core.Media;

/// <summary>Decodes MP3 files into 16-bit PCM WAV, which media players can seek to the exact sample. Blocking I/O: call it off the UI thread.</summary>
internal static class Mp3Conformer
{
    private const int HeaderBytes = 44;

    /// <summary>Writes <paramref name="mp3Path"/>, decoded, to <paramref name="wavPath"/>, replacing any file there. A half-written WAV never appears under that name.</summary>
    /// <exception cref="IOException">Either file can't be read or written.</exception>
    /// <exception cref="InvalidDataException">The MP3 holds no audio.</exception>
    public static void ToWav(string mp3Path, string wavPath)
    {
        var partial = wavPath + ".part";
        try
        {
            // NLayer leaks the handle when its path constructor throws on a bad file, so the stream is owned here.
            using (var input = File.OpenRead(mp3Path))
            using (var mp3 = new MpegFile(input))
            using (var output = File.Create(partial))
                Write(mp3, output);
            File.Move(partial, wavPath, overwrite: true);
        }
        finally
        {
            File.Delete(partial);
        }
    }

    private static void Write(MpegFile mp3, Stream output)
    {
        var rate = mp3.SampleRate;
        var channels = mp3.Channels;
        output.Write(stackalloc byte[HeaderBytes]);
        var samples = new float[16384];
        var pcm = new byte[samples.Length * 2];
        long dataBytes = 0;
        int read;
        while ((read = mp3.ReadSamples(samples, 0, samples.Length)) > 0)
        {
            for (var i = 0; i < read; i++)
                BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2), (short)Math.Round(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue));
            output.Write(pcm, 0, read * 2);
            dataBytes += read * 2;
        }
        if (dataBytes == 0 || rate <= 0 || channels <= 0)
            throw new InvalidDataException("The MP3 has no audio frames.");
        output.Position = 0;
        output.Write(Header(rate, channels, (uint)dataBytes));
    }

    private static byte[] Header(int rate, int channels, uint dataBytes)
    {
        var header = new byte[HeaderBytes];
        var span = header.AsSpan();
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], dataBytes + HeaderBytes - 8);
        "WAVEfmt "u8.CopyTo(span[8..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], (short)channels);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], rate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], rate * channels * 2);
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], (short)(channels * 2));
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], 16);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[40..], dataBytes);
        return header;
    }
}
