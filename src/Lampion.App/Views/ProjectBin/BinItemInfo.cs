namespace Lampion.App.Views.ProjectBin;

/// <summary>The text of the bin's info header for one selection.</summary>
/// <param name="Title">Bold first line.</param>
/// <param name="Line1">Grey second line.</param>
/// <param name="Line2">Grey third line; replaced by the patch warning when there is one.</param>
/// <param name="Icon">Glyph shown in the thumbnail.</param>
internal readonly record struct BinItemInfo(string Title, string Line1, string Line2, string Icon);
