using Lampion.Core.Persistence;

namespace Lampion.App.Editor.Storage;

/// <summary>Where Lampion keeps per-user files: one folder under the roaming application data.</summary>
internal static class UserPaths
{
    /// <summary>The per-user Lampion folder. Not guaranteed to exist yet.</summary>
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lampion");

    /// <summary>Machine-level preferences (<see cref="UserSettings"/>).</summary>
    public static string Settings => Path.Combine(Root, "settings.json");

    /// <summary>The recently opened shows list.</summary>
    public static string Recent => Path.Combine(Root, "recent.json");

    /// <summary>The stage and rig new shows start from, when the user has set one.</summary>
    public static string DefaultLayout => Path.Combine(Root, "default" + StageLayout.Extension);

    /// <summary>Saved stage layout templates.</summary>
    public static string Templates => Path.Combine(Root, "Templates");

    /// <summary>Saved effect presets.</summary>
    public static string Presets => Path.Combine(Root, "Presets");

    /// <summary><paramref name="name"/> with characters the file system rejects replaced by '_'; <paramref name="fallback"/> when nothing is left.</summary>
    public static string SafeFileName(string name, string fallback)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
        return safe.Length == 0 ? fallback : safe;
    }

    /// <summary>True when both paths name the same file: case-sensitive on Linux, case-insensitive elsewhere.</summary>
    public static bool Same(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
}
