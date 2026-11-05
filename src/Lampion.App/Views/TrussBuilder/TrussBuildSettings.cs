using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;

namespace Lampion.App.Views.TrussBuilder;

/// <summary>What the truss builder dialog has been told so far; edited in place by its form.</summary>
internal sealed class TrussBuildSettings
{
    /// <summary>Arch or cube.</summary>
    public TrussShape Shape { get; set; } = TrussShape.Arch;

    /// <summary>Prefix for the pieces', lights' and group's names.</summary>
    public string Name { get; set; } = "Arch";

    /// <summary>Floor point under the middle, metres.</summary>
    public double CentreX { get; set; }

    /// <summary>Floor point under the middle, metres; + is toward the audience.</summary>
    public double CentreZ { get; set; }

    /// <summary>Outer width, metres.</summary>
    public double Width { get; set; } = 6;

    /// <summary>Outer depth of a cube, metres.</summary>
    public double Depth { get; set; } = 3;

    /// <summary>Height to the top of the spans, metres.</summary>
    public double Height { get; set; } = 4;

    /// <summary>Degrees the whole structure is turned.</summary>
    public double Yaw { get; set; }

    /// <summary>The lights' profile, or null for a bare structure.</summary>
    public FixtureProfile? Profile { get; set; }

    /// <summary>How many lights hang on each lit span; 0 for none.</summary>
    public int LightsPerSpan { get; set; } = 3;

    /// <summary>Which spans of a cube get lights.</summary>
    public LightSpans Spans { get; set; } = LightSpans.Front;

    /// <summary>Whether any lights will be hung.</summary>
    public bool HasLights => Profile is not null && LightsPerSpan > 0;

    /// <summary>The structure these settings describe, centred where asked.</summary>
    public TrussStructure Build() => TrussStructures.Build(Shape, Name, new Vec3(CentreX, 0, CentreZ), Width, Depth, Height, Yaw);

    /// <summary>The spans of <paramref name="structure"/> that get lights, front first.</summary>
    public IEnumerable<StageObject> LitSpans(TrussStructure structure)
    {
        if (!HasLights)
            return [];
        var count = Shape == TrussShape.Arch ? 1 : Spans switch
        {
            LightSpans.Front => 1,
            LightSpans.FrontAndBack => 2,
            _ => 4,
        };
        return structure.Spans.Take(count);
    }
}
