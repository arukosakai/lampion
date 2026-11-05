using Lampion.Core.Model;
using Lampion.Core.Persistence;

namespace Lampion.Core.Tests;

public class EffectTests
{
    [Fact]
    public void EveryCatalogEntryCreatesItsOwnKindAndSurvivesJson()
    {
        foreach (var (kind, name, _) in Effect.Catalog)
        {
            var effect = Effect.Create(kind);
            Assert.Equal(name, effect.DisplayName);
            Assert.Equal(kind, effect.Kind);

            var clip = new LightClip { Effect = effect };
            var preset = EffectPreset.Deserialize(EffectPreset.Serialize(EffectPreset.From(clip, "x")));
            Assert.Equal(effect.GetType(), preset.Effect.GetType());
        }
    }

    [Fact]
    public void NewEffectsStayInRangeAndRepeatExactly()
    {
        foreach (var kind in new[] { "gradient", "wave", "fire", "lightning", "heartbeat", "hit" })
        {
            var effect = Effect.Create(kind);
            for (var t = 0.0; t < 20; t += 0.037)
                for (var i = 0; i < 6; i++)
                {
                    var ctx = new EffectContext(t, 20, i, 6);
                    var sample = effect.Sample(ctx);
                    Assert.InRange(sample.Intensity, 0, 1);
                    Assert.InRange(sample.Color.Max, 0, 1);
                    // Scrubbing and QLC+ export must see exactly what playback did.
                    Assert.Equal(sample, effect.Sample(ctx));
                }
        }
    }

    [Fact]
    public void GradientSpreadsThePaletteAcrossFixtures()
    {
        var gradient = new GradientEffect { Colors = ["#FF0000", "#0000FF"] };
        Assert.Equal("#FF0000", gradient.Sample(new EffectContext(0, 4, 0, 2)).Color.ToHex());
        Assert.Equal("#0000FF", gradient.Sample(new EffectContext(0, 4, 1, 2)).Color.ToHex());
    }

    [Fact]
    public void HitFiresOnceUnlessGivenAnInterval()
    {
        var once = new HitEffect { Decay = 0.5 };
        Assert.Equal(1, once.Sample(new EffectContext(0, 4, 0, 1)).Intensity);
        Assert.Equal(0, once.Sample(new EffectContext(2, 4, 0, 1)).Intensity);

        var repeating = new HitEffect { Decay = 0.5, Interval = 1 };
        Assert.Equal(1, repeating.Sample(new EffectContext(2, 4, 0, 1)).Intensity, 6);
    }

    [Fact]
    public void LightningTogetherStrikesEveryFixtureAlike()
    {
        var lightning = new LightningEffect { Mode = LightningMode.Together };
        for (var t = 0.0; t < 10; t += 0.05)
            Assert.Equal(lightning.Sample(new EffectContext(t, 10, 0, 4)), lightning.Sample(new EffectContext(t, 10, 3, 4)));
    }

    [Fact]
    public void PresetRoundTripsTheWholeLookAndMakesIndependentClips()
    {
        var source = new LightClip
        {
            Duration = 6,
            FadeIn = 1,
            FadeEasing = Easing.EaseOut,
            Effect = new ChaseEffect { Colors = ["#00FF00", "#FF00FF"], StepTime = 0.1 },
            Automation = [new AutomationLane { Target = AutomationTarget.HueShift, Animated = true, Keys = [new Keyframe { Time = 0 }, new Keyframe { Time = 6, Value = 1 }] }],
        };
        var preset = EffectPreset.Deserialize(EffectPreset.Serialize(EffectPreset.From(source, "Neon chase")));

        var clip = preset.CreateClip(12);
        Assert.Equal("Neon chase", clip.Name);
        Assert.Equal(12, clip.Start);
        Assert.Equal(6, clip.Duration);
        Assert.Equal(1, clip.FadeIn);
        Assert.Equal(Easing.EaseOut, clip.FadeEasing);
        var chase = Assert.IsType<ChaseEffect>(clip.Effect);
        Assert.Equal(["#00FF00", "#FF00FF"], chase.Colors);
        Assert.Equal(0.5, Assert.Single(clip.Automation).Evaluate(3), 6);

        chase.Colors[0] = "#FFFFFF";
        Assert.Equal("#00FF00", ((ChaseEffect)preset.CreateClip(0).Effect).Colors[0]);
    }

    [Fact]
    public void ApplyingAPresetKeepsTheClipsPlaceAndClampsFades()
    {
        var preset = new EffectPreset { Effect = new FireEffect(), FadeIn = 5, FadeOut = 5 };
        var clip = new LightClip { Start = 3, Duration = 2, EffectOffset = 1.5 };
        preset.ApplyTo(clip);
        Assert.Equal(3, clip.Start);
        Assert.Equal(2, clip.Duration);
        Assert.Equal(2, clip.FadeIn);
        Assert.Equal(0, clip.EffectOffset);
        Assert.IsType<FireEffect>(clip.Effect);
    }

    [Fact]
    public void BuiltInPresetsAllLoad()
    {
        foreach (var preset in BuiltInPresets.All())
        {
            var copy = EffectPreset.Deserialize(EffectPreset.Serialize(preset));
            Assert.Equal(preset.Name, copy.Name);
            Assert.True(copy.CreateClip(0).Duration > 0);
        }
    }

    [Fact]
    public void GarbageIsNotAPreset() => Assert.ThrowsAny<Exception>(() => EffectPreset.Deserialize("{\"name\":\"x\",\"effect\":null}"));
}
