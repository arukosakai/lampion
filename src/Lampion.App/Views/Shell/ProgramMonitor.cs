using Avalonia.Controls;

namespace Lampion.App.Views.Shell;

/// <summary>The program monitor, After Effects' Composition viewer: Stage 3D, Video and DMX output tabs.</summary>
internal static class ProgramMonitor
{
    /// <summary>The tabbed monitor over the three views; Stage 3D is shown first.</summary>
    public static TabControl Build(Control stage, Control video, Control dmx) =>
        new()
        {
            Items =
            {
                new TabItem { Header = "Stage 3D", Content = stage },
                new TabItem { Header = "Video", Content = video },
                new TabItem { Header = "DMX output", Content = dmx },
            },
        };
}
