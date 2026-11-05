namespace Lampion.Core.Model;

/// <summary>Short random ids for model objects.</summary>
public static class Ids
{
    /// <summary>A new 12-character lowercase hex id (48 random bits from a GUID). Thread-safe.</summary>
    public static string New() => Guid.NewGuid().ToString("N")[..12];
}
