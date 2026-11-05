using Lampion.Core.Model;

namespace Lampion.App.Views;

/// <summary>What the user picked on the start screen.</summary>
/// <param name="Project">The show to edit, already loaded or freshly created.</param>
/// <param name="Path">The file it was opened from; null for a new, unsaved show.</param>
internal sealed record StartChoice(Project Project, string? Path);
