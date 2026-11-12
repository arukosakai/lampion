namespace Lampion.Core.Engine;

/// <summary>Something that produces DMX frames on its own clock, which other threads read the newest of.</summary>
public interface IDmxFrameSource
{
    /// <summary>Goes up by one per published frame; compare it to skip copying a frame you've already seen. Any thread.</summary>
    long Sequence { get; }

    /// <summary>Copies the newest frame into <paramref name="destination"/>, reusing its buffers. Any thread; allocation-free in steady state.</summary>
    /// <returns>The <see cref="Sequence"/> of the frame copied.</returns>
    long CopyLatestTo(DmxFrame destination);
}
