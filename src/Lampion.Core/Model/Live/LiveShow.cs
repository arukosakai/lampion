namespace Lampion.Core.Model.Live;

/// <summary>
/// One entry in the set list: another <c>.lampion</c> show. Its fixtures are matched to the live rig by id,
/// then by universe and address, so shows made from a copy of the rig line up without any setup.
/// </summary>
public sealed class LiveShow
{
    /// <summary>Stable id the set list refers to.</summary>
    public string Id { get; set; } = Ids.New();

    /// <summary>Name shown in the set list.</summary>
    public string Name { get; set; } = "Show";

    /// <summary>The show file, relative to the project file when it sits next to it, absolute otherwise.</summary>
    public string Path { get; set; } = "";

    /// <summary>Start over at the end instead of stopping.</summary>
    public bool Loop { get; set; }
}
