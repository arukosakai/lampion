using Lampion.App.Editor;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Live;

namespace Lampion.App.Live;

/// <summary>Set-list and console edits, each one undo step through <see cref="EditorState.Commit"/>.</summary>
internal static class LiveEdits
{
    /// <summary>Adds a show to the end of the set list; <paramref name="path"/> is made relative to the project when it's next to it.</summary>
    public static void AddShow(EditorState state, string name, string path)
    {
        state.Project.Live.Shows.Add(new LiveShow { Name = name, Path = Relative(state.FilePath, path) });
        state.Commit();
    }

    /// <summary>Removes a show from the set list.</summary>
    public static void RemoveShow(EditorState state, LiveShow show)
    {
        if (state.Project.Live.Shows.Remove(show))
            state.Commit();
    }

    /// <summary>Moves a show one place up (-1) or down (+1).</summary>
    public static void MoveShow(EditorState state, LiveShow show, int step)
    {
        var shows = state.Project.Live.Shows;
        var from = shows.IndexOf(show);
        var to = from + step;
        if (from < 0 || to < 0 || to >= shows.Count)
            return;
        shows.RemoveAt(from);
        shows.Insert(to, show);
        state.Commit();
    }

    /// <summary>A new intensity fader over the selected fixtures (or none), added at the end.</summary>
    public static LiveFader AddFader(EditorState state)
    {
        var fader = new LiveFader { Name = $"Fader {state.Project.Live.Faders.Count + 1}" };
        fader.FixtureIds.AddRange(state.SelectedFixtureIds);
        state.Project.Live.Faders.Add(fader);
        state.Commit();
        return fader;
    }

    /// <summary>A new rainbow pad over the selected fixtures (or none), added at the end.</summary>
    public static LivePad AddPad(EditorState state)
    {
        var pad = new LivePad { Name = "Rainbow", Effect = new RainbowEffect() };
        pad.FixtureIds.AddRange(state.SelectedFixtureIds);
        state.Project.Live.Pads.Add(pad);
        state.Commit();
        return pad;
    }

    /// <summary>Removes a fader.</summary>
    public static void RemoveFader(EditorState state, LiveFader fader)
    {
        if (state.Project.Live.Faders.Remove(fader))
            state.Commit();
    }

    /// <summary>Removes a pad, and points faders that steered it at nothing.</summary>
    public static void RemovePad(EditorState state, LivePad pad)
    {
        if (!state.Project.Live.Pads.Remove(pad))
            return;
        foreach (var fader in state.Project.Live.Faders)
            if (fader.PadId == pad.Id)
                fader.PadId = null;
        state.Commit();
    }

    private static string Relative(string? projectPath, string path)
    {
        if (projectPath is null)
            return path;
        var relative = Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(projectPath))!, path);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? path : relative.Replace('\\', '/');
    }
}
