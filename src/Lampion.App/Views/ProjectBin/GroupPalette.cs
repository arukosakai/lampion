namespace Lampion.App.Views.ProjectBin;

/// <summary>The colours new fixture groups cycle through, so neighbouring groups are easy to tell apart.</summary>
internal static class GroupPalette
{
    private static readonly string[] Colors = ["#E6A23A", "#3AA0E6", "#E63A8E", "#5BD16A", "#B05BE6", "#E6E03A"];

    /// <summary>Hex colour for the group that will sit at <paramref name="index"/> (0-based) in the project.</summary>
    public static string ForIndex(int index) => Colors[index % Colors.Length];
}
