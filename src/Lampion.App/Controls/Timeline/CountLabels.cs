using System.Globalization;

namespace Lampion.App.Controls.Timeline;

/// <summary>Cached digit strings for small counts drawn every frame, e.g. a clip's keyed-lane count. UI thread only.</summary>
internal static class CountLabels
{
    private static readonly string[] Cache = new string[100];

    /// <summary>"<paramref name="n"/>"; allocation-free for 0..99 after the first call.</summary>
    public static string Of(int n) =>
        (uint)n < Cache.Length ? Cache[n] ??= n.ToString(CultureInfo.InvariantCulture) : n.ToString(CultureInfo.InvariantCulture);
}
