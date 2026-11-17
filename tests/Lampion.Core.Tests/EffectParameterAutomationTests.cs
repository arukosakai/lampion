using Lampion.Core.Model.Animation;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Timeline;
using Lampion.Core.Persistence;

namespace Lampion.Core.Tests;

public class EffectParameterAutomationTests
{
    private static Keyframe K(double time, double value, Easing easing = Easing.Linear) => new() { Time = time, Value = value, Easing = easing };

    private static AutomationLane Lane(string parameter, params Keyframe[] keys)
    {
        var lane = new AutomationLane { Target = AutomationTarget.EffectParameter, Parameter = parameter };
        lane.Keys.AddRange(keys);
        return lane;
    }

    private static LightClip Clip(Effect effect, params AutomationLane[] lanes)
    {
        var clip = new LightClip { Duration = 10, Effect = effect };
        clip.Automation.AddRange(lanes);
        return clip;
    }

    // The effect's speed times its clock: the phase it has travelled.
    private static double Phase(LightClip clip, double local)
    {
        var ctx = clip.EffectContextAt(local, 0, 1);
        return ctx.Time * ctx.Parameter(0, double.NaN);
    }

    private static double Riemann(Func<double, double> speed, double to, int steps = 200_000)
    {
        var h = to / steps;
        var sum = 0.0;
        for (var i = 0; i < steps; i++)
            sum += speed((i + 0.5) * h) * h;
        return sum;
    }

    [Fact]
    public void EveryAnimatableParameterHeldConstantPlaysLikeSettingTheProperty()
    {
        foreach (var entry in EffectCatalog.Entries)
        {
            var parameters = EffectCatalog.Create(entry.Kind).AnimatableParameters;
            Assert.InRange(parameters.Count, 0, EffectParameterValues.Capacity);
            Assert.Equal(parameters.Count, parameters.Select(p => p.Key).Distinct().Count());
            for (var slot = 0; slot < parameters.Count; slot++)
            {
                var parameter = parameters[slot];
                var effect = EffectCatalog.Create(entry.Kind);
                var value = Math.Max(parameter.Minimum, parameter.Get(effect) * 1.7 + 0.13);
                var clip = Clip(effect, Lane(parameter.Key, K(0, value), K(10, value)));
                clip.EffectOffset = 1.25;
                var expected = EffectCatalog.Create(entry.Kind);
                parameter.Set(expected, value);

                for (var t = 0.0; t < 10; t += 0.0371)
                    for (var i = 0; i < 5; i++)
                    {
                        var actual = effect.Sample(clip.EffectContextAt(t, i, 5));
                        var wanted = expected.Sample(new EffectContext(t + 1.25, 10, i, 5));
                        var where = $"{entry.Kind}.{parameter.Key} at {t:0.###} s, fixture {i}";
                        Assert.True(Math.Abs(actual.Intensity - wanted.Intensity) < 1e-6, where);
                        Assert.True(Math.Abs(actual.Color.R - wanted.Color.R) + Math.Abs(actual.Color.G - wanted.Color.G) + Math.Abs(actual.Color.B - wanted.Color.B) < 1e-6, where);
                    }
            }
        }
    }

    [Theory]
    [InlineData(Easing.Linear)]
    [InlineData(Easing.EaseIn)]
    [InlineData(Easing.EaseOut)]
    [InlineData(Easing.EaseInOut)]
    [InlineData(Easing.Hold)]
    public void KeyframedSpeedIsIntegratedSoThePhaseNeverJumps(Easing easing)
    {
        var lane = Lane("speed", K(1, 0.5, easing), K(5, 3));
        var clip = Clip(new RainbowEffect(), lane);
        foreach (var t in new[] { 0.0, 0.5, 1, 2.2, 4.9, 5, 7.5 })
            Assert.Equal(Riemann(lane.Evaluate, t), Phase(clip, t), 1e-4);

        // Small steps in time are small steps in phase, even where the speed changes fastest.
        for (var t = 0.0; t < 8; t += 0.01)
            Assert.InRange(Phase(clip, t + 0.01) - Phase(clip, t), 0, 3 * 0.01 + 1e-9);
    }

    [Fact]
    public void KeyframedStepTimeIntegratesStepsPerSecond()
    {
        var lane = Lane("stepTime", K(0, 0.5), K(4, 0.1, Easing.EaseIn));
        var clip = Clip(new ChaseEffect(), lane);
        foreach (var t in new[] { 1.0, 3.3, 6 })
        {
            var ctx = clip.EffectContextAt(t, 0, 1);
            var steps = ctx.Time / ctx.Parameter(0, double.NaN);
            // Simpson on 1/x is close rather than exact; what matters is that it never jumps.
            Assert.Equal(Riemann(s => 1 / lane.Evaluate(s), t), steps, 0.005);
        }
    }

    [Fact]
    public void SpeedsBelowTheEffectsOwnClampAreIntegratedAtTheClamp()
    {
        var lane = Lane("rate", K(0, 0), K(4, 0));
        var clip = Clip(new SparkleEffect(), lane);
        Assert.Equal(0.05 * 3, Phase(clip, 3), 9);
    }

    [Fact]
    public void ASpeedCrossingZeroKeepsAFiniteClock()
    {
        var clip = Clip(new RainbowEffect(), Lane("speed", K(0, -1), K(2, 1)));
        Assert.Equal(-0.5, Phase(clip, 1), 6);
        Assert.True(double.IsFinite(clip.EffectContextAt(1, 0, 1).Time));
        Assert.Equal(0, Phase(clip, 2), 6);
    }

    [Fact]
    public void TheEffectOffsetIsAtTheStartingSpeedSoASplitPicksUpWhereTheLeftHalfStopped()
    {
        var whole = Clip(new SparkleEffect(), Lane("rate", K(0, 2), K(10, 12, Easing.EaseOut)));
        const double cut = 3.7;
        var right = Clip(new SparkleEffect(), Lane("rate", K(0, whole.Automation[0].Evaluate(cut)), K(10 - cut, 12)));
        right.EffectOffset = whole.EffectContextAt(cut, 0, 1).Time;

        Assert.Equal(Phase(whole, cut), Phase(right, 0), 9);
        for (var i = 0; i < 8; i++)
            Assert.Equal(whole.Effect.Sample(whole.EffectContextAt(cut, i, 8)).Intensity, right.Effect.Sample(right.EffectContextAt(0, i, 8)).Intensity, 9);
    }

    [Fact]
    public void PlainParametersReplaceTheValueAtEachInstant()
    {
        var clip = Clip(new StrobeEffect { Rate = 1 }, Lane("duty", K(0, 0), K(10, 1)));
        // At 2.25 s the strobe is 25 % into its cycle: off while duty is 0.2, on once duty passes 0.25.
        Assert.Equal(0, clip.Effect.Sample(clip.EffectContextAt(2.25, 0, 1)).Intensity);
        Assert.Equal(1, clip.Effect.Sample(clip.EffectContextAt(5.25, 0, 1)).Intensity);
    }

    [Fact]
    public void DisabledEmptyAndUnknownLanesDoNothing()
    {
        var disabled = Lane("rate", K(0, 9));
        disabled.Enabled = false;
        var clip = Clip(new SparkleEffect(), disabled, Lane("rate"), Lane("tail", K(0, 3)), Lane("nope", K(0, 3)));
        var plain = new SparkleEffect();
        for (var t = 0.0; t < 5; t += 0.1)
            Assert.Equal(plain.Sample(new EffectContext(t, 10, 2, 4)), clip.Effect.Sample(clip.EffectContextAt(t, 2, 4)));
    }

    [Fact]
    public void ParameterLanesSurviveJsonAndCloning()
    {
        var clip = Clip(new ChaseEffect(), Lane("stepTime", K(0, 0.5), K(4, 0.1, Easing.EaseInOut)));
        var loaded = EffectPreset.Deserialize(EffectPreset.Serialize(EffectPreset.From(clip, "x"))).CreateClip(0);
        foreach (var copy in new[] { loaded, (LightClip)clip.Clone() })
        {
            var lane = Assert.Single(copy.Automation);
            Assert.Equal(AutomationTarget.EffectParameter, lane.Target);
            Assert.Equal("stepTime", lane.Parameter);
            Assert.Equal("Step time", lane.Label);
            Assert.Equal(clip.EffectContextAt(2.5, 0, 1), copy.EffectContextAt(2.5, 0, 1));
        }
    }

    [Fact]
    public void LanesOnOtherTargetsDontWriteAParameterKey()
    {
        var clip = Clip(new SolidEffect(), new AutomationLane { Target = AutomationTarget.Intensity, Keys = { K(0, 1) } });
        Assert.DoesNotContain("\"parameter\"", EffectPreset.Serialize(EffectPreset.From(clip, "x")));
    }
}
