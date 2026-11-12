using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Lampion.App.Editor;
using Lampion.App.Live;
using Lampion.App.Output;

namespace Lampion.App.Views.Live;

/// <summary>
/// Output → Live console: the set list on the left, faders and pads on the right, engine and MIDI status below.
/// Opening it starts live mode (the workspace's stage and DMX monitor follow the live output); closing it stops it,
/// after asking while a show is playing. Wires the panels to one <see cref="LiveSession"/>; it does no work itself.
/// </summary>
internal sealed class LiveWindow : Window
{
    private readonly LiveSession _session;
    private readonly DispatcherTimer _timer;
    private bool _confirmedClose;

    private LiveWindow(EditorState state, DmxOutput output)
    {
        Title = "Lampion Live";
        Width = 1180;
        Height = 720;
        _session = new LiveSession(state, output);
        var setList = new SetListPanel(this, state, _session);
        var console = new ConsolePanel(this, state, _session);
        var status = new LiveStatusLine(_session, output);

        var body = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(status, Dock.Bottom);
        body.Children.Add(status);
        DockPanel.SetDock(setList, Dock.Left);
        body.Children.Add(setList);
        body.Children.Add(console);
        Content = body;

        // UI refresh only: DMX timing lives on the engine thread and doesn't depend on this timer.
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Render, (_, _) =>
        {
            _session.Tick();
            setList.Refresh();
            console.Refresh();
            status.Refresh();
        });
        Opened += (_, _) =>
        {
            _session.Start();
            _timer.Start();
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            _session.Dispose();
        };
    }

    /// <summary>Shows the live console next to <paramref name="owner"/>, or brings the open one forward.</summary>
    public static void ShowFor(Window owner, EditorState state, DmxOutput output, ref LiveWindow? open)
    {
        if (open is { IsVisible: true })
        {
            open.Activate();
            return;
        }
        var window = new LiveWindow(state, output);
        open = window;
        window.Show(owner);
    }

    /// <inheritdoc />
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_confirmedClose || _session.Playing is null || _session.Engine.ShowEnded)
            return;
        e.Cancel = true;
        var answer = await Dialogs.Ask(this, "Stop live?", $"{_session.Playing.Name} is playing. Closing the live console stops it and hands the lights back to the timeline.", "Stop and close", "Keep playing");
        if (answer != "Stop and close")
            return;
        _confirmedClose = true;
        Close();
    }
}
