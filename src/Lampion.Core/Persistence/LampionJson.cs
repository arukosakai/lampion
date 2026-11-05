using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lampion.Core.Persistence;

/// <summary>
/// The JSON settings shared by every Lampion user file. Get-only collections are filled in place
/// (<see cref="JsonObjectCreationHandling.Populate"/>); properties whose defaults must be replaced rather than
/// filled (non-empty defaults, polymorphic effects) opt out with <see cref="JsonObjectCreationHandlingAttribute"/>.
/// </summary>
internal static class LampionJson
{
    /// <summary>Indented, camelCase; what is written to disk.</summary>
    public static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowOutOfOrderMetadataProperties = true,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    };

    /// <summary>The same settings on one line, for snapshots and deep copies.</summary>
    public static readonly JsonSerializerOptions Compact = new(Indented) { WriteIndented = false };

    /// <summary>Deserializes a user file.</summary>
    /// <param name="json">The file's text.</param>
    /// <param name="emptyMessage">What to tell the user when the file holds <c>null</c>.</param>
    /// <exception cref="JsonException">The text is not valid JSON for <typeparamref name="T"/>.</exception>
    /// <exception cref="InvalidDataException">The file is empty, or sets a list to <c>null</c>.</exception>
    public static T Read<T>(string json, string emptyMessage)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Indented) ?? throw new InvalidDataException(emptyMessage);
        }
        catch (InvalidOperationException ex)
        {
            // Populating a get-only list from an explicit null throws this instead of a JsonException.
            throw new InvalidDataException($"The file is damaged: {ex.Message}", ex);
        }
    }
}
