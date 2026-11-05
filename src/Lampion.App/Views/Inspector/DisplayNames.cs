using System.Text;

namespace Lampion.App.Views.Inspector;

/// <summary>Turns identifiers into the sentence-case labels the inspector shows. Allocates; build time only.</summary>
internal static class DisplayNames
{
    /// <summary>"FadeRatio" → "Fade ratio": a space and lower case before every capital after the first letter.</summary>
    public static string Humanize(string name)
    {
        var text = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c))
                text.Append(' ').Append(char.ToLowerInvariant(c));
            else
                text.Append(c);
        }
        return text.ToString();
    }

    /// <summary>An enum value as a label, e.g. <c>DipToBlack</c> → "Dip to Black".</summary>
    public static string Spaced<T>(T value)
        where T : struct, Enum => Humanize(value.ToString()).Replace("to black", "to Black");
}
