using System.IO.Ports;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lampion.App.Output;

namespace Lampion.App.Views.Output;

/// <summary>
/// Output → Output Setup: picks Art-Net or a USB DMX interface, its address or serial port, and whether to send now.
/// The status line follows the live output, so a cable that won't open shows up while the window is still open.
/// </summary>
internal sealed class OutputSetupWindow : Window
{
    private static readonly (DmxOutputKind Kind, string Name, string Hint)[] Kinds =
    [
        (DmxOutputKind.ArtNet, "Art-Net (network)", "Sends every universe over the network to Art-Net nodes, or to QLC+ on this or another computer."),
        (DmxOutputKind.EnttecPro, "Enttec DMX USB Pro or compatible", "Enttec DMX USB Pro / Pro Mk2, DMXking ultraDMX and other interfaces that speak the Pro protocol. Sends one universe."),
        (DmxOutputKind.OpenDmx, "Open DMX USB (FTDI)", "Enttec Open DMX USB and other plain FTDI cables. Sends one universe; the computer drives the line, so expect about 35 refreshes a second."),
    ];

    private readonly DmxOutput _output;
    private readonly DispatcherTimer _timer;
    private readonly TextBox _host;
    private readonly ContentControl _portSlot = new();
    private readonly CheckBox _send;
    private readonly TextBlock _hint = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray, FontSize = 11 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.IndianRed, IsVisible = false };
    private readonly Control _artNetFields;
    private readonly Control _usbFields;
    private OutputSettings _draft;

    private OutputSetupWindow(DmxOutput output)
    {
        _output = output;
        _draft = output.Settings;
        Title = "Output Setup";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var kind = Ui.Combo(Kinds.Select(k => k.Kind), _draft.Kind, k => { _draft = _draft with { Kind = k }; ShowKind(); }, k => Array.Find(Kinds, x => x.Kind == k).Name);
        _host = new TextBox { Text = _draft.ArtNetHost, PlaceholderText = OutputSettings.Broadcast };
        ToolTip.SetTip(_host, $"{OutputSettings.Broadcast} reaches every node on the network; type a node's IP to send to it alone");
        _artNetFields = Ui.Field("Send to", _host, 110);

        var universe = Ui.Number(_draft.Universe, v => _draft = _draft with { Universe = (int)v }, 1, 32767, 1, "0");
        ToolTip.SetTip(universe, "The Lampion universe this interface sends; fixtures patched elsewhere stay silent on it");
        var refresh = Ui.Button("Refresh", FillPorts, "Look for interfaces again, e.g. after plugging one in");
        var portRow = new DockPanel();
        DockPanel.SetDock(refresh, Dock.Right);
        refresh.Margin = new Thickness(6, 0, 0, 0);
        portRow.Children.Add(refresh);
        portRow.Children.Add(_portSlot);
        _usbFields = Ui.Column(4, Ui.Field("Serial port", portRow, 110), Ui.Field("Universe", universe, 110));
        FillPorts();

        _send = new CheckBox { Content = "Send output", IsChecked = output.IsSending };
        var ok = new Button { Content = "OK", MinWidth = 90, IsDefault = true };
        var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true };
        ok.Click += (_, _) => Confirm();
        cancel.Click += (_, _) => Close();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } };

        Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 8,
            Children = { Ui.Section("Device"), Ui.Field("Output", kind, 110), _hint, _artNetFields, _usbFields, _send, _status, _error, buttons },
        };
        ShowKind();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => ShowStatus());
        _timer.Start();
        ShowStatus();
        Closed += (_, _) => _timer.Stop();
    }

    /// <summary>Shows the window over <paramref name="owner"/>; OK saves the settings and applies them to <paramref name="output"/>.</summary>
    public static Task Show(Window owner, DmxOutput output) => new OutputSetupWindow(output).ShowDialog(owner);

    private void ShowKind()
    {
        var usb = _draft.Kind != DmxOutputKind.ArtNet;
        _artNetFields.IsVisible = !usb;
        _usbFields.IsVisible = usb;
        _hint.Text = Array.Find(Kinds, k => k.Kind == _draft.Kind).Hint;
        _error.IsVisible = false;
    }

    private void FillPorts()
    {
        var ports = SerialPortNames();
        // Keep the saved port listed even while it's unplugged, so OK doesn't silently forget it.
        if (_draft.SerialPort is { } saved && !ports.Contains(saved))
            ports.Insert(0, saved);
        var combo = Ui.Combo(ports, _draft.SerialPort ?? "", p => _draft = _draft with { SerialPort = p });
        if (ports.Count == 0)
            combo.PlaceholderText = "No serial ports found; plug the interface in and press Refresh";
        else if (_draft.SerialPort is null)
        {
            combo.SelectedIndex = 0;
            _draft = _draft with { SerialPort = ports[0] };
        }
        _portSlot.Content = combo;
    }

    private static List<string> SerialPortNames() =>
        SerialPort.GetPortNames()
            .Where(Openable)
            .Distinct()
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

    // macOS lists every device twice and the tty.* twin waits for a modem carrier on open, so only cu.* is usable;
    // on Windows .NET refuses to open anything not named COMx, virtual pairs like com0com's CNCA0 included.
    private static bool Openable(string port) =>
        OperatingSystem.IsMacOS() ? port.StartsWith("/dev/cu.", StringComparison.Ordinal)
        : !OperatingSystem.IsWindows() || port.StartsWith("COM", StringComparison.OrdinalIgnoreCase);

    private void ShowStatus()
    {
        if (!_output.IsSending)
        {
            _status.Text = "Output is off.";
            _status.Foreground = Brushes.Gray;
        }
        else if (_output.Problem is { } problem)
        {
            _status.Text = $"{_output.Settings.Describe()}: {problem}";
            _status.Foreground = Brushes.IndianRed;
        }
        else
        {
            _status.Text = $"Sending: {_output.Settings.Describe()}";
            _status.Foreground = Brushes.MediumSeaGreen;
        }
    }

    private void Confirm()
    {
        var settings = _draft with { ArtNetHost = string.IsNullOrWhiteSpace(_host.Text) ? OutputSettings.Broadcast : _host.Text.Trim() };
        if (settings.Problem() is { } problem)
        {
            _error.Text = problem;
            _error.IsVisible = true;
            return;
        }
        _output.Configure(settings, _send.IsChecked == true);
        Close();
    }
}
