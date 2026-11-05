namespace Lampion.App.Views.ProjectBin;

/// <summary>The model object behind a bin row, kept in the row's <c>Tag</c>.</summary>
/// <param name="Kind">What the row stands for.</param>
/// <param name="Id">The object's id, or the folder key for <see cref="BinItemKind.Folder"/>.</param>
internal readonly record struct BinItem(BinItemKind Kind, string Id);
