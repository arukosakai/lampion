using Lampion.App.Editor.Storage;
using Lampion.Core.Media;

namespace Lampion.App.Media;

/// <summary>The one proxy cache shared by editor playback and live shows, so each MP3 is conformed once per machine.</summary>
internal static class AudioProxies
{
    /// <summary>The cache under <see cref="UserPaths.AudioProxies"/>.</summary>
    public static AudioProxyCache Cache { get; } = new(UserPaths.AudioProxies);
}
