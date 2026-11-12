using Lampion.Core.Model.Live;

namespace Lampion.Core.Persistence;

/// <summary>
/// A <c>.lamplive</c> file: a live console (set list, faders, pads, MIDI mappings, widgets) taken out of its project,
/// plus the fixtures and groups it refers to, so it can be loaded into a project with another patch.
/// Paths to shows and photos are absolute in the file.
/// </summary>
public sealed class LiveConsoleFile
{
    /// <summary>File format version; bump it and migrate on load when the saved shape changes.</summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>The console.</summary>
    public LiveSet Console { get; } = new();

    /// <summary>Every fixture the console's faders and pads target.</summary>
    public List<LiveFixtureRef> Fixtures { get; } = [];

    /// <summary>Every group the console's faders and pads target.</summary>
    public List<LiveGroupRef> Groups { get; } = [];
}
