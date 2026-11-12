using Lampion.App.Controls;
using Lampion.App.Editor;

namespace Lampion.App.Views.Inspector;

/// <summary>Whether timing properties are shown in their own unit or in BPM: one choice for every tempo row, remembered per machine. UI thread only.</summary>
internal static class TempoUnits
{
    private static bool? _inBpm;

    /// <summary>True when tempo rows show BPM.</summary>
    public static bool InBpm => _inBpm ??= UserData.LoadSettings().RatesInBpm;

    /// <summary>The range <paramref name="scale"/> is shown with right now.</summary>
    public static ScrubRange Current(TempoScale scale) => InBpm ? scale.Bpm : scale.Native;

    /// <summary>Switches every tempo row between its own unit and BPM, and saves the choice.</summary>
    public static void Toggle()
    {
        _inBpm = !InBpm;
        UserData.SaveSettings(UserData.LoadSettings() with { RatesInBpm = _inBpm.Value });
    }
}
