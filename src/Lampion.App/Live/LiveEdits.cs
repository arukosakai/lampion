using Lampion.App.Editor;
using Lampion.Core.Live;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Live;
using Lampion.Core.Persistence;

namespace Lampion.App.Live;

/// <summary>Set-list and console edits, each one undo step through <see cref="EditorState.Commit"/>.</summary>
internal static class LiveEdits
{
    /// <summary>Adds a show to the end of the set list; <paramref name="path"/> is made relative to the project when it's next to it.</summary>
    public static void AddShow(EditorState state, string name, string path)
    {
        state.Project.Live.Shows.Add(new LiveShow { Name = name, Path = ShowPaths.Relative(state.FilePath, path) });
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

    /// <summary>Adds a widget at the bottom of the column; photos get <paramref name="imagePath"/>, made relative to the project when next to it.</summary>
    public static void AddWidget(EditorState state, LiveWidgetKind kind, string imagePath = "")
    {
        state.Project.Live.Widgets.Add(new LiveWidget { Kind = kind, ImagePath = imagePath.Length == 0 ? "" : ShowPaths.Relative(state.FilePath, imagePath) });
        state.Commit();
    }

    /// <summary>Points a photo widget at another picture.</summary>
    public static void SetPhoto(EditorState state, LiveWidget widget, string imagePath)
    {
        widget.ImagePath = ShowPaths.Relative(state.FilePath, imagePath);
        state.Commit();
    }

    /// <summary>Removes a widget.</summary>
    public static void RemoveWidget(EditorState state, LiveWidget widget)
    {
        if (state.Project.Live.Widgets.Remove(widget))
            state.Commit();
    }

    /// <summary>Binds the console to an APC mini mk2's controls (see <see cref="ApcLayout"/>), as one undo step.</summary>
    public static void MapToApcMiniMk2(EditorState state)
    {
        ApcLayout.Apply(state.Project.Live);
        state.Commit();
    }

    /// <summary>Replaces the whole console (set list, faders, pads, widgets, light delay, set-list MIDI) with <paramref name="console"/>, as one undo step.</summary>
    public static void ReplaceConsole(EditorState state, LiveSet console)
    {
        var live = state.Project.Live;
        live.Shows.Clear();
        live.Shows.AddRange(console.Shows);
        live.Faders.Clear();
        live.Faders.AddRange(console.Faders);
        live.Pads.Clear();
        live.Pads.AddRange(console.Pads);
        live.Widgets.Clear();
        live.Widgets.AddRange(console.Widgets);
        live.LightDelayMs = console.LightDelayMs;
        live.GoMidi = console.GoMidi;
        live.StopMidi = console.StopMidi;
        state.Commit();
    }
}
