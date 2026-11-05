using Avalonia.Threading;
using Lampion.App.Editor;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views.ProfileEditor;

/// <summary>
/// One open profile editor's state: the working copy being edited, its test values and which fixtures use it.
/// Sections edit through it and listen to its two events instead of calling each other.
/// </summary>
internal sealed class ProfileEditSession
{
    /// <param name="state">Supplies the patch, to find the fixtures that use <paramref name="original"/>.</param>
    /// <param name="original">The profile as it is in the project (or a new one); it is never modified.</param>
    public ProfileEditSession(EditorState state, FixtureProfile original)
    {
        Original = original;
        Working = original.Clone(newId: false);
        foreach (var channel in Working.Channels)
            Test.Add(FullOn(channel));
        Patched = state.Project.Fixtures.Where(f => f.ProfileId == original.Id).ToList();
    }

    /// <summary>The profile as opened, for comparing the footprint against.</summary>
    public FixtureProfile Original { get; }

    /// <summary>The copy every section edits; it replaces <see cref="Original"/> in the project on Save.</summary>
    public FixtureProfile Working { get; }

    /// <summary>Test values, always one per channel of <see cref="Working"/>.</summary>
    public ProfileTestValues Test { get; } = new();

    /// <summary>Fixtures in the project patched with this profile, in patch order.</summary>
    public IReadOnlyList<Fixture> Patched { get; }

    /// <summary>Raised after channels are added, removed, moved or change role; followed by <see cref="ValuesChanged"/>.</summary>
    public event Action? ChannelsChanged;

    /// <summary>Raised after any test value changes, including from <see cref="ChannelsChanged"/> edits.</summary>
    public event Action? ValuesChanged;

    /// <summary>Raised with the index of a channel whose name was edited.</summary>
    public event Action<int>? ChannelRenamed;

    /// <summary>A test value that makes the preview light up: intensity and colour at full, movement centred.</summary>
    public static byte FullOn(FixtureChannel channel) =>
        channel.Role switch
        {
            ChannelRole.Dimmer or ChannelRole.Red or ChannelRole.Green or ChannelRole.Blue or ChannelRole.White => 255,
            ChannelRole.Pan or ChannelRole.Tilt => 128,
            ChannelRole.Amber or ChannelRole.UV or ChannelRole.Strobe or ChannelRole.PanFine or ChannelRole.TiltFine => 0,
            _ => channel.Default,
        };

    /// <summary>Appends a channel of <paramref name="role"/>, named after it (or "Channel N" for generic ones).</summary>
    public void AddChannel(ChannelRole role)
    {
        var name = role == ChannelRole.Generic ? $"Channel {Working.Channels.Count + 1}" : ChannelRoleStyle.DefaultName(role);
        var channel = new FixtureChannel { Name = name, Role = role };
        Working.Channels.Add(channel);
        Test.Add(FullOn(channel));
        NotifyChannelsChanged();
    }

    /// <summary>Removes channel <paramref name="index"/> and its test value.</summary>
    public void RemoveChannel(int index)
    {
        Working.Channels.RemoveAt(index);
        Test.RemoveAt(index);
        NotifyChannelsChanged();
    }

    /// <summary>Exchanges two channels, test values included.</summary>
    public void SwapChannels(int a, int b)
    {
        var channels = Working.Channels;
        (channels[a], channels[b]) = (channels[b], channels[a]);
        Test.Swap(a, b);
        NotifyChannelsChanged();
    }

    /// <summary>
    /// Gives channel <paramref name="index"/> a new role and its full-on test value. Names nobody typed follow
    /// the role, so "Channel 3" becomes "Red" when you pick Red.
    /// </summary>
    public void ChangeRole(int index, ChannelRole role)
    {
        var channel = Working.Channels[index];
        if (IsPlaceholderName(channel.Name, channel.Role))
            channel.Name = ChannelRoleStyle.DefaultName(role);
        channel.Role = role;
        Test[index] = FullOn(channel);
        // Called from a role combo's own event; rebuilding the rows right away would tear it down mid-close.
        Dispatcher.UIThread.Post(NotifyChannelsChanged);
    }

    /// <summary>Renames channel <paramref name="index"/>.</summary>
    public void RenameChannel(int index, string name)
    {
        Working.Channels[index].Name = name;
        ChannelRenamed?.Invoke(index);
    }

    /// <summary>Sets one test value, DMX 0–255.</summary>
    public void SetTestValue(int index, byte value)
    {
        Test[index] = value;
        NotifyValuesChanged();
    }

    /// <summary>Sets every test value from its channel, e.g. to <see cref="FullOn"/> or the defaults.</summary>
    public void SetAllTestValues(Func<FixtureChannel, byte> value)
    {
        for (var i = 0; i < Test.Count; i++)
            Test[i] = value(Working.Channels[i]);
        NotifyValuesChanged();
    }

    /// <summary>Tells every section the test values changed, after something wrote into <see cref="Test"/> directly.</summary>
    public void NotifyValuesChanged() => ValuesChanged?.Invoke();

    /// <summary>Tells every section to rebuild from the working profile, then to show the test values.</summary>
    public void NotifyChannelsChanged()
    {
        ChannelsChanged?.Invoke();
        NotifyValuesChanged();
    }

    private static bool IsPlaceholderName(string name, ChannelRole role) =>
        string.IsNullOrWhiteSpace(name) || name == ChannelRoleStyle.DefaultName(role) || name.StartsWith("Channel ", StringComparison.Ordinal) || name.StartsWith("Ch ", StringComparison.Ordinal);
}
