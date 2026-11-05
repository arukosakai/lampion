namespace Lampion.App.Views.ProjectBin;

/// <summary>What a row in the Project bin stands for.</summary>
internal enum BinItemKind
{
    /// <summary>A top-level folder (groups, fixtures, profiles, media); its id is the folder key.</summary>
    Folder,

    /// <summary>A <see cref="Core.Model.Rig.FixtureGroup"/>.</summary>
    Group,

    /// <summary>A patched <see cref="Core.Model.Rig.Fixture"/>.</summary>
    Fixture,

    /// <summary>A <see cref="Core.Model.Rig.FixtureProfile"/>.</summary>
    Profile,

    /// <summary>An audio or video <see cref="Core.Model.Timeline.MediaClip"/>.</summary>
    Media,
}
