namespace Lampion.Core.Export;

/// <summary>Settings for one QLC+ export.</summary>
public sealed class QlcExportOptions
{
    /// <summary>Samples per second taken before the show is reduced to linear fades; clamped to 1..100.</summary>
    public double SampleRate { get; set; } = 25;

    /// <summary>Largest DMX error (in DMX steps, 0–255 scale) a fade may introduce in place of the sampled values.</summary>
    public int Tolerance { get; set; } = 1;

    /// <summary>A QLC+ project to add the show to; null builds a new workspace.</summary>
    public string? BaseWorkspacePath { get; set; }

    /// <summary>Folder the .qxw will be saved in; media paths are written relative to it when possible. Null writes them as stored.</summary>
    public string? OutputDirectory { get; set; }

    /// <summary>Folder the Lampion project lives in, to resolve relative media paths; null leaves them relative.</summary>
    public string? ProjectDirectory { get; set; }
}
