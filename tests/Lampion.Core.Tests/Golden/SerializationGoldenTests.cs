using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Timeline;
using Lampion.Core.Persistence;

namespace Lampion.Core.Tests.Golden;

/// <summary>
/// User files must keep loading and keep saving the same way. Each golden is loaded and written back, and
/// the everything-project is rebuilt from code, both of which must produce the stored JSON exactly.
/// </summary>
public class SerializationGoldenTests
{
    private const string ProjectFile = "everything.lampion.json";
    private const string PresetFile = "chase.lampfx.json";
    private const string LayoutFile = "everything.lampstage.json";

    [Fact]
    public void ProjectSavesToTheGoldenJson() => GoldenFile.AssertMatches(ProjectFile, ProjectSerializer.Serialize(EverythingProject.Create()));

    [Fact]
    public void GoldenProjectRoundTrips() => GoldenFile.AssertMatches(ProjectFile, ProjectSerializer.Serialize(ProjectSerializer.Deserialize(GoldenFile.Read(ProjectFile))));

    [Fact]
    public void GoldenProjectLoadsTheSameShow()
    {
        var loaded = ProjectSerializer.Deserialize(GoldenFile.Read(ProjectFile));
        Assert.Equal(5, loaded.Stage.Objects.Count);
        Assert.Equal(3, Assert.IsType<GradientEffect>(((LightClip)loaded.Tracks[1].Clips[2]).Effect).Colors.Count);
        Assert.Empty(Assert.IsType<ColorCycleEffect>(((LightClip)loaded.Tracks[1].Clips[5]).Effect).Colors);
        Assert.Equal(["#FF0000", "#00FF00", "#0000FF"], Assert.IsType<ChaseEffect>(((LightClip)loaded.Tracks[2].Clips[0]).Effect).Colors);
    }

    [Fact]
    public void FilesFromBeforePitchAndRollLoadUnrotated()
    {
        var withoutRoll = string.Join('\n', GoldenFile.Read(ProjectFile).Split('\n').Where(line => !line.Contains("\"roll\"")));
        var loaded = ProjectSerializer.Deserialize(withoutRoll);
        Assert.All(loaded.Stage.Objects, o => Assert.Equal(0, o.Roll));
        Assert.All(loaded.Fixtures, f => Assert.Equal(0, f.Roll));
    }

    [Fact]
    public void PresetSavesToTheGoldenJson() => GoldenFile.AssertMatches(PresetFile, EffectPreset.Serialize(Preset()));

    [Fact]
    public void GoldenPresetRoundTrips() => GoldenFile.AssertMatches(PresetFile, EffectPreset.Serialize(EffectPreset.Deserialize(GoldenFile.Read(PresetFile))));

    [Fact]
    public void LayoutSavesToTheGoldenJson() => GoldenFile.AssertMatches(LayoutFile, StageLayout.Serialize(StageLayout.From(EverythingProject.Create(), "Everything venue")));

    [Fact]
    public void GoldenLayoutRoundTrips() => GoldenFile.AssertMatches(LayoutFile, StageLayout.Serialize(StageLayout.Deserialize(GoldenFile.Read(LayoutFile))));

    private static EffectPreset Preset()
    {
        var clip = (LightClip)EverythingProject.Create().Tracks[2].Clips[0];
        var preset = EffectPreset.From(clip, "Golden chase", "Bounce with pan and tilt");
        // Cloned lanes get fresh ids; pin them so the output is reproducible.
        for (var i = 0; i < preset.Automation.Count; i++)
            preset.Automation[i].Id = $"preset-lane-{i}";
        return preset;
    }
}
