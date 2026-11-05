using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lampion.App.Editor;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.App.Views.ProjectBin;

/// <summary>
/// The preview area at the top of the Project panel: what the selection is at a glance, or a project summary
/// with any patch problems when nothing is selected.
/// </summary>
internal sealed class ProjectInfoHeader : ContentControl
{
    private readonly EditorState _state;
    private readonly TextBlock _icon = new() { FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _title = new() { FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _line1 = new() { Foreground = Brushes.Gray, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _line2 = new() { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly StackPanel _text;
    private IReadOnlyList<string> _problems = [];

    /// <param name="state">Supplies the project the selection belongs to.</param>
    public ProjectInfoHeader(EditorState state)
    {
        _state = state;
        Margin = new Thickness(8, 6);
        MinHeight = 54;
        _text = new StackPanel { Children = { _title, _line1, _line2 } };
        var thumb = new Border
        {
            Width = 54,
            Height = 54,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)),
            Child = _icon,
            Margin = new Thickness(0, 0, 10, 0),
        };
        var row = new DockPanel();
        DockPanel.SetDock(thumb, Dock.Left);
        row.Children.Add(thumb);
        row.Children.Add(_text);
        Content = row;
    }

    /// <summary>
    /// The patch problems the project summary warns about, from <see cref="PatchPlanner.Problems"/>. Set it when the
    /// patch changes rather than per selection: working them out is quadratic in the fixture count.
    /// </summary>
    public void SetProblems(IReadOnlyList<string> problems) => _problems = problems;

    /// <summary>Describes <paramref name="items"/>: the summary for none, a count for several, details for one.</summary>
    public void Show(IReadOnlyList<BinItem> items)
    {
        var info = Describe(items);
        var problems = items.Count == 0 ? _problems : [];
        _icon.Text = info.Icon;
        _title.Text = info.Title;
        _line1.Text = info.Line1;
        if (problems.Count > 0)
        {
            _line2.Text = $"⚠ {problems.Count} patch problem{(problems.Count == 1 ? "" : "s")}";
            _line2.Foreground = Brushes.Orange;
            ToolTip.SetTip(_text, string.Join("\n", problems));
        }
        else
        {
            _line2.Text = info.Line2;
            _line2.Foreground = Brushes.Gray;
            ToolTip.SetTip(_text, null);
        }
    }

    private BinItemInfo Describe(IReadOnlyList<BinItem> items)
    {
        if (items.Count == 0)
            return ProjectSummary();
        if (items.Count > 1)
            return new BinItemInfo($"{items.Count} items selected", "Drag them onto the timeline to light them", "", "▦");
        var project = _state.Project;
        return items[0] switch
        {
            { Kind: BinItemKind.Fixture } f when project.FixtureById(f.Id) is { } fixture => DescribeFixture(fixture),
            { Kind: BinItemKind.Group } g when project.Groups.Find(x => x.Id == g.Id) is { } group => new BinItemInfo(group.Name, $"{group.FixtureIds.Count} fixtures", "Drag onto the timeline for a light track", "▣"),
            { Kind: BinItemKind.Profile } p when project.Profiles.Find(x => x.Id == p.Id) is { } profile => new BinItemInfo(
                profile.Model,
                $"{profile.Manufacturer} · {profile.Channels.Count} channels · {profile.Beam}",
                "Double-click to edit the channel layout",
                "⚙"
            ),
            { Kind: BinItemKind.Media } m when _state.FindClip(m.Id)?.Clip is MediaClip clip => new BinItemInfo(clip.Name, Path.GetFileName(clip.Path), $"{clip.Duration:0.00} s at {clip.Start:0.00} s", "♪"),
            _ => ProjectSummary(),
        };
    }

    private BinItemInfo DescribeFixture(Fixture fixture)
    {
        var profile = _state.Project.ProfileOf(fixture);
        var last = fixture.Address + (profile?.Channels.Count ?? 1) - 1;
        var position = fixture.Position;
        return new BinItemInfo(
            fixture.Name,
            profile?.DisplayName ?? "no profile",
            $"DMX {fixture.Universe}.{fixture.Address:000}–{last:000} · {position.X:0.##}, {position.Y:0.##}, {position.Z:0.##} m",
            "💡"
        );
    }

    private BinItemInfo ProjectSummary()
    {
        var project = _state.Project;
        var universes = project.Fixtures.Select(f => f.Universe).Distinct().Count();
        return new BinItemInfo(
            project.Name,
            $"{project.Fixtures.Count} fixtures in {universes} universe{(universes == 1 ? "" : "s")}",
            $"{project.Tracks.Count} tracks · {project.AllClips.Count()} clips",
            "🏮"
        );
    }
}
