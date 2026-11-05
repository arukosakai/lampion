using Lampion.Core.Engine;
using Lampion.Core.Model;
using Lampion.Core.Model.Stage;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Editor;

/// <summary>
/// The open show: the project instance, its renderer, where it lives on disk, whether it has unsaved edits
/// and a version counter that caches compare against. Raises no events; <see cref="EditorState"/> does.
/// </summary>
internal sealed class ProjectDocument
{
    /// <summary>Opens <paramref name="project"/>, saved at <paramref name="filePath"/> (null while unsaved).</summary>
    public ProjectDocument(Project project, string? filePath)
    {
        Project = project;
        Renderer = new ShowRenderer(project);
        FilePath = filePath;
    }

    /// <summary>The project being edited. Replaced wholesale by undo, redo and open.</summary>
    public Project Project { get; private set; }

    /// <summary>The renderer for <see cref="Project"/>; recreated with it. Not thread-safe: UI thread only.</summary>
    public ShowRenderer Renderer { get; private set; }

    /// <summary>The file the project was opened from or last saved to; null while unsaved.</summary>
    public string? FilePath { get; set; }

    /// <summary>True when the project has edits that are not on disk.</summary>
    public bool Dirty { get; set; }

    /// <summary>Bumped on every model change and project replacement.</summary>
    public int Version { get; private set; }

    /// <summary>Records an edit: bumps <see cref="Version"/> and marks the document dirty.</summary>
    public void Touch()
    {
        Version++;
        Dirty = true;
    }

    /// <summary>Swaps in a new project instance with a fresh renderer and bumps <see cref="Version"/>.</summary>
    public void Replace(Project project)
    {
        Project = project;
        Renderer = new ShowRenderer(project);
        Version++;
    }

    /// <summary>The track with this id, or null (also for a null id).</summary>
    public Track? TrackById(string? id)
    {
        if (id is null)
            return null;
        foreach (var track in Project.Tracks)
            if (track.Id == id)
                return track;
        return null;
    }

    /// <summary>The clip with this id and the track holding it, or null.</summary>
    public (Track Track, Clip Clip)? FindClip(string id)
    {
        foreach (var track in Project.Tracks)
            foreach (var clip in track.Clips)
                if (clip.Id == id)
                    return (track, clip);
        return null;
    }

    /// <summary>The stage set piece with this id, or null (also for a null id).</summary>
    public StageObject? StageObjectById(string? id)
    {
        if (id is null)
            return null;
        foreach (var item in Project.Stage.Objects)
            if (item.Id == id)
                return item;
        return null;
    }
}
