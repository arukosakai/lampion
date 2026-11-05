namespace Lampion.App.Rendering;

/// <summary>
/// How much GPU work the 3D stage spends per frame. It belongs to the machine, not the show, so it is kept
/// in the user's settings rather than the project file.
/// </summary>
/// <param name="RenderScale">Fraction of the view's pixels actually rendered, then scaled up (0.5..1).</param>
/// <param name="Samples">MSAA samples; below 2 means no anti-aliasing. Capped by what the GPU offers.</param>
/// <param name="HazeSteps">Ray-march steps through each beam; fewer is grainier haze, not dimmer.</param>
/// <param name="MaxLights">How many of the brightest fixtures light each surface.</param>
public sealed record StageFidelity(double RenderScale, int Samples, int HazeSteps, int MaxLights)
{
    public static readonly StageFidelity Performance = new(0.5, 0, 8, 8);
    public static readonly StageFidelity Balanced = new(0.75, 2, 16, 24);
    public static readonly StageFidelity Quality = new(1, 4, Shaders.MaxHazeSteps, Shaders.MaxLights);

    public static IReadOnlyList<(string Name, StageFidelity Preset)> Presets { get; } =
        [("Performance", Performance), ("Balanced", Balanced), ("Quality", Quality)];

    /// <summary>The preset these settings match, or null when they were tuned by hand.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? PresetName => Presets.FirstOrDefault(p => p.Preset == this).Name;

    /// <summary>Clamps values read from a settings file into what the renderer supports.</summary>
    public StageFidelity Sanitised() =>
        new(Math.Clamp(RenderScale, 0.25, 1), Math.Clamp(Samples, 0, 8), Math.Clamp(HazeSteps, 1, Shaders.MaxHazeSteps), Math.Clamp(MaxLights, 1, Shaders.MaxLights));
}
