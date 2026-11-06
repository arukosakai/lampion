using System.Net;

namespace Lampion.App.Output;

/// <summary>How this machine sends DMX. Part of the user settings; property names are the file format.</summary>
internal sealed record OutputSettings
{
    /// <summary>The broadcast address: every Art-Net node on the local network listens to it.</summary>
    public const string Broadcast = "255.255.255.255";

    /// <summary>Which kind of output to use.</summary>
    public DmxOutputKind Kind { get; init; } = DmxOutputKind.ArtNet;

    /// <summary>IPv4 address Art-Net is sent to; <see cref="Broadcast"/> reaches every node.</summary>
    public string ArtNetHost { get; init; } = Broadcast;

    /// <summary>Serial port of the USB interface (COM3, /dev/cu.usbserial-…), or null before one is picked.</summary>
    public string? SerialPort { get; init; }

    /// <summary>The Lampion universe (1-based) a USB interface sends; it carries just the one.</summary>
    public int Universe { get; init; } = 1;

    /// <summary>What is wrong with these settings in plain words, or null when they can be used.</summary>
    public string? Problem() =>
        Kind switch
        {
            DmxOutputKind.ArtNet when !IPAddress.TryParse(ArtNetHost, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork =>
                $"\"{ArtNetHost}\" isn't an IPv4 address; use {Broadcast} to reach every node.",
            DmxOutputKind.EnttecPro or DmxOutputKind.OpenDmx when string.IsNullOrWhiteSpace(SerialPort) => "Pick the serial port the USB interface is on.",
            DmxOutputKind.EnttecPro or DmxOutputKind.OpenDmx when Universe < 1 => "Universes start at 1.",
            _ => null,
        };

    /// <summary>Short description for the status bar and menus, e.g. "Art-Net → broadcast" or "Enttec USB Pro on COM3".</summary>
    public string Describe() =>
        Kind switch
        {
            DmxOutputKind.ArtNet => ArtNetHost == Broadcast ? "Art-Net → broadcast" : $"Art-Net → {ArtNetHost}",
            DmxOutputKind.EnttecPro => $"Enttec USB Pro on {SerialPort} (universe {Universe})",
            _ => $"Open DMX on {SerialPort} (universe {Universe})",
        };
}
