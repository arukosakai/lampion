namespace Lampion.Core.Model.Live;

/// <summary>
/// The live mode of a project: a set list of other Lampion shows played on this project's rig, plus a
/// console of faders and effect pads for the fixtures the playing show leaves free. Saved with the project.
/// </summary>
public sealed class LiveSet
{
    /// <summary>Shows to play, in set-list order.</summary>
    public List<LiveShow> Shows { get; } = [];

    /// <summary>Console faders, left to right.</summary>
    public List<LiveFader> Faders { get; } = [];

    /// <summary>Effect pads, row by row.</summary>
    public List<LivePad> Pads { get; } = [];
}
