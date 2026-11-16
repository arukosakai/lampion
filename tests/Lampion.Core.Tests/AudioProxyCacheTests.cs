using Lampion.Core.Media;

namespace Lampion.Core.Tests;

public sealed class AudioProxyCacheTests : IDisposable
{
    // One second of silence, then one second of a 440 Hz tone at 1/8 amplitude, as a VBR MP3.
    private static readonly string SilentIntro = Path.Combine(AppContext.BaseDirectory, "Assets", "silent-intro.mp3");

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "lampion-proxy-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void Conformed_wav_keeps_the_silence_and_the_tone_where_they_were()
    {
        Directory.CreateDirectory(_folder);
        var wav = Path.Combine(_folder, "out.wav");

        Mp3Conformer.ToWav(SilentIntro, wav);
        var peaks = WavPeakReader.Read(wav, 100);

        Assert.InRange(peaks.Duration, 1.95, 2.1);
        Assert.All(peaks.Peaks[5..90], p => Assert.True(p < 0.01f, $"expected silence, got {p}"));
        Assert.All(peaks.Peaks[110..190], p => Assert.True(p > 0.1f, $"expected the tone, got {p}"));
        Assert.False(File.Exists(wav + ".part"));
    }

    [Fact]
    public async Task Resolve_hands_out_the_mp3_until_its_proxy_is_ready()
    {
        var cache = new AudioProxyCache(_folder);

        Assert.Equal(SilentIntro, cache.Resolve(SilentIntro));
        await cache.Prepare(SilentIntro);
        var proxy = cache.Resolve(SilentIntro);

        Assert.StartsWith(_folder, proxy);
        Assert.EndsWith(".wav", proxy);
        Assert.True(File.Exists(proxy));
    }

    [Fact]
    public async Task A_second_cache_reuses_the_proxy_already_on_disk()
    {
        var first = new AudioProxyCache(_folder);
        await first.Prepare(SilentIntro);
        var written = File.GetLastWriteTimeUtc(first.Resolve(SilentIntro));

        var second = new AudioProxyCache(_folder);
        await second.Prepare(SilentIntro);

        Assert.Equal(first.Resolve(SilentIntro), second.Resolve(SilentIntro));
        Assert.Equal(written, File.GetLastWriteTimeUtc(second.Resolve(SilentIntro)));
    }

    [Fact]
    public async Task Files_that_seek_exactly_already_are_left_alone()
    {
        var cache = new AudioProxyCache(_folder);

        await cache.Prepare("song.wav");

        Assert.Equal("song.wav", cache.Resolve("song.wav"));
        Assert.False(Directory.Exists(_folder));
    }

    [Fact]
    public async Task A_broken_mp3_reports_why_and_keeps_playing_the_original()
    {
        Directory.CreateDirectory(_folder);
        var broken = Path.Combine(_folder, "broken.mp3");
        await File.WriteAllTextAsync(broken, "not audio");
        var cache = new AudioProxyCache(Path.Combine(_folder, "proxies"));
        string? reported = null;
        cache.Failed += (_, message) => reported = message;

        await cache.Prepare(broken);

        Assert.Equal(broken, cache.Resolve(broken));
        Assert.Contains("broken.mp3", reported);
    }
}
