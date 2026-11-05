namespace Lampion.Core.Model.Timeline;

/// <summary>A timeline layer. Light tracks drive their target fixtures; the topmost track in the project's list is drawn on top.</summary>
public sealed class Track
{
    /// <summary>Stable id the selection refers to.</summary>
    public string Id { get; set; } = Ids.New();

    /// <summary>Name shown in the track header.</summary>
    public string Name { get; set; } = "Track";

    /// <summary>What the track holds.</summary>
    public TrackKind Kind { get; set; } = TrackKind.Light;

    /// <summary>Muted tracks are skipped by the evaluator (and exported muted).</summary>
    public bool Muted { get; set; }

    /// <summary>When any light track is soloed, only soloed light tracks are evaluated.</summary>
    public bool Solo { get; set; }

    /// <summary>Locked tracks can't be edited on the timeline.</summary>
    public bool Locked { get; set; }

    /// <summary>Whether the timeline shows the automation lanes (the twirl-down).</summary>
    public bool Expanded { get; set; }

    /// <summary>How the track combines with the tracks below it.</summary>
    public BlendMode Blend { get; set; } = BlendMode.Normal;

    /// <summary>The order effects walk the target fixtures in.</summary>
    public FixtureOrder Order { get; set; } = FixtureOrder.AsListed;

    /// <summary>Fixtures this light track drives; groups are expanded at evaluation time.</summary>
    public List<string> FixtureIds { get; } = [];

    /// <summary>Groups this light track drives, after <see cref="FixtureIds"/>; duplicates are dropped when resolving.</summary>
    public List<string> GroupIds { get; } = [];

    /// <summary>Clips sorted by <see cref="Clip.Start"/>; call <see cref="SortClips"/> after moving one.</summary>
    public List<Clip> Clips { get; } = [];

    /// <summary>Restores start-time order after clips were added or moved.</summary>
    public void SortClips() => Clips.Sort((a, b) => a.Start.CompareTo(b.Start));
}
