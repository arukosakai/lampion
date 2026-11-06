using System.Text.Json;
using Lampion.App.Output;
using Lampion.App.Rendering;

namespace Lampion.App.Editor.Storage;

/// <summary>Reads and writes <see cref="UserSettings"/>. Never throws: settings are a convenience, not a requirement.</summary>
internal static class SettingsStore
{
    /// <summary>The saved preferences, or defaults when the file is missing or unreadable.</summary>
    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(UserPaths.Settings) && JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(UserPaths.Settings)) is { } settings)
                // JSON can say "Fidelity": null (or "Output": null) whatever the type claims.
                return settings with
                {
                    Fidelity = (settings.Fidelity as StageFidelity)?.Sanitised() ?? StageFidelity.Quality,
                    Output = (settings.Output as OutputSettings) ?? new OutputSettings(),
                };
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Fall through to defaults: a broken settings file must never stop the app from starting.
        }
        return new UserSettings();
    }

    /// <summary>Writes <paramref name="settings"/>; failures are ignored.</summary>
    public static void Save(UserSettings settings)
    {
        try
        {
            Directory.CreateDirectory(UserPaths.Root);
            File.WriteAllText(UserPaths.Settings, JsonSerializer.Serialize(settings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing a preference is harmless; the session keeps working with what is in memory.
        }
    }
}
