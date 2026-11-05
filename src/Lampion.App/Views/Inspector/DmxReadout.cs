using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Views.Inspector;

/// <summary>
/// A fixture's live DMX values as a monospace table (address, channel name, value). Line labels are built
/// once; on refresh only the value runs that changed are swapped, using cached strings, so it doesn't allocate.
/// </summary>
internal sealed class DmxReadout : IInspectorRefreshable
{
    private static readonly FontFamily Monospace = new("Consolas, Menlo, monospace");
    private static readonly string[] ValueTexts = BuildValueTexts();

    private readonly InspectorContext _context;
    private readonly Fixture _fixture;
    private readonly int _channels;
    private readonly Run[] _values;
    private readonly int[] _shown;

    /// <summary>Builds the table, shows the current frame and tracks itself for playhead refreshes.</summary>
    /// <param name="context">The inspector the readout belongs to.</param>
    /// <param name="fixture">The fixture whose footprint is shown.</param>
    /// <param name="profile">The fixture's profile, for channel names and count.</param>
    public DmxReadout(InspectorContext context, Fixture fixture, FixtureProfile profile)
    {
        _context = context;
        _fixture = fixture;
        _channels = profile.Channels.Count;
        _values = new Run[_channels];
        _shown = new int[_channels];
        Array.Fill(_shown, -1);

        var view = new TextBlock { FontFamily = Monospace, FontSize = 11, Foreground = Brushes.Gainsboro, Margin = new Thickness(20, 0, 0, 0) };
        var inlines = view.Inlines ??= [];
        for (var i = 0; i < _channels; i++)
        {
            if (i > 0)
                inlines.Add(new LineBreak());
            inlines.Add(new Run($"{fixture.Address + i,3}  {profile.Channels[i].Name,-12} "));
            _values[i] = new Run();
            inlines.Add(_values[i]);
        }
        View = view;
        Refresh();
        context.Track(this);
    }

    /// <summary>The control to add to a section.</summary>
    public Control View { get; }

    /// <inheritdoc />
    public void Refresh()
    {
        var slice = _context.State.CurrentFrame.Slice(_fixture, _channels);
        for (var i = 0; i < _channels; i++)
        {
            int value = i < slice.Length ? slice[i] : 0;
            if (_shown[i] == value)
                continue;
            _shown[i] = value;
            _values[i].Text = ValueTexts[value];
        }
    }

    private static string[] BuildValueTexts()
    {
        var texts = new string[256];
        for (var i = 0; i < texts.Length; i++)
            texts[i] = $"{i,3}";
        return texts;
    }
}
