using Lampion.Core.Model;
using Lampion.Core.Model.Effects;
using Lampion.Core.Model.Live;
using Lampion.Core.Model.Rig;
using Lampion.Core.Persistence;

namespace Lampion.Core.Tests;

public class LiveConsoleFileTests
{
    private static readonly string Here = Path.Combine(Path.GetTempPath(), "lampion-console-tests");

    private static Project Source()
    {
        var project = new Project();
        project.Fixtures.Add(new Fixture { Id = "a", Name = "Wash 1", Universe = 1, Address = 1 });
        project.Fixtures.Add(new Fixture { Id = "b", Name = "Reflector", Universe = 2, Address = 40 });
        project.Fixtures.Add(new Fixture { Id = "c", Name = "Gone", Universe = 3, Address = 100 });
        var group = new FixtureGroup { Id = "g1", Name = "Front Wash" };
        group.FixtureIds.Add("a");
        project.Groups.Add(group);

        var fader = new LiveFader { Name = "Reflectors", Midi = new MidiBinding(MidiBindingKind.Control, 1, 7) };
        fader.FixtureIds.AddRange(["b", "c"]);
        fader.GroupIds.Add("g1");
        var pad = new LivePad { Name = "Rainbow", Effect = new RainbowEffect { Speed = 3 } };
        pad.FixtureIds.Add("a");
        project.Live.Faders.Add(fader);
        project.Live.Pads.Add(pad);
        project.Live.Shows.Add(new LiveShow { Name = "Opener", Path = "songs/opener.lampion" });
        project.Live.Shows.Add(new LiveShow { Name = "This timeline", Path = "" });
        project.Live.Widgets.Add(new LiveWidget { Kind = LiveWidgetKind.Photo, ImagePath = "gumi.png" });
        project.Live.LightDelayMs = 40;
        return project;
    }

    // The same rig re-patched into another project: new ids everywhere, a renamed-case group, one fixture missing.
    private static Project Target()
    {
        var project = new Project();
        project.Fixtures.Add(new Fixture { Id = "x", Universe = 1, Address = 1 });
        project.Fixtures.Add(new Fixture { Id = "y", Universe = 2, Address = 40 });
        project.Groups.Add(new FixtureGroup { Id = "g9", Name = "front wash" });
        return project;
    }

    private static LiveConsoleImport RoundTrip(Project source, string? sourcePath, Project target, string? targetPath)
    {
        var json = LiveConsoleSerializer.Serialize(LiveConsoleSerializer.Export(source, sourcePath));
        return LiveConsoleSerializer.Import(LiveConsoleSerializer.Deserialize(json), target, targetPath);
    }

    [Fact]
    public void FixturesAreMatchedByAddressAndGroupsByName()
    {
        var import = RoundTrip(Source(), null, Target(), null);

        var fader = import.Console.Faders[0];
        Assert.Equal(["y"], fader.FixtureIds);
        Assert.Equal(["g9"], fader.GroupIds);
        Assert.Equal(["x"], import.Console.Pads[0].FixtureIds);
        Assert.Equal(["Gone"], import.UnmatchedFixtures);
        Assert.Empty(import.UnmatchedGroups);
    }

    [Fact]
    public void EverythingElseComesAcrossUnchanged()
    {
        var import = RoundTrip(Source(), null, Target(), null);

        Assert.Equal("Reflectors", import.Console.Faders[0].Name);
        Assert.Equal(new MidiBinding(MidiBindingKind.Control, 1, 7), import.Console.Faders[0].Midi);
        Assert.Equal(3, Assert.IsType<RainbowEffect>(import.Console.Pads[0].Effect).Speed);
        Assert.Equal(["Opener", "This timeline"], import.Console.Shows.Select(s => s.Name));
        Assert.Equal(40, import.Console.LightDelayMs);
    }

    [Fact]
    public void LoadingIntoTheSameProjectKeepsEveryTarget()
    {
        var source = Source();
        var import = RoundTrip(source, null, source, null);

        Assert.Equal(["b", "c"], import.Console.Faders[0].FixtureIds);
        Assert.Empty(import.UnmatchedFixtures);
    }

    [Fact]
    public void PathsFollowTheProjectTheyAreLoadedInto()
    {
        var sourcePath = Path.Combine(Here, "tour", "venue-a.lampion");
        var targetPath = Path.Combine(Here, "tour", "venue-b.lampion");

        var file = LiveConsoleSerializer.Export(Source(), sourcePath);
        var import = LiveConsoleSerializer.Import(file, Target(), targetPath);

        Assert.Equal(Path.Combine(Here, "tour", "songs", "opener.lampion"), file.Console.Shows[0].Path);
        Assert.Equal("songs/opener.lampion", import.Console.Shows[0].Path);
        Assert.Equal("gumi.png", import.Console.Widgets[0].ImagePath);
        Assert.Equal("", import.Console.Shows[1].Path);
    }

    [Fact]
    public void ExportingDoesNotTouchTheProject()
    {
        var source = Source();
        var file = LiveConsoleSerializer.Export(source, Path.Combine(Here, "show.lampion"));
        file.Console.Faders[0].Name = "changed";

        Assert.Equal("songs/opener.lampion", source.Live.Shows[0].Path);
        Assert.Equal("Reflectors", source.Live.Faders[0].Name);
    }

    [Fact]
    public void EmptyPathMeansThisTimelineAndStaysEmpty()
    {
        // Path.GetRelativePath throws on "", which once crashed + This timeline on a saved project.
        Assert.Equal("", ShowPaths.Relative(Path.Combine(Here, "show.lampion"), ""));
        Assert.Equal("", ShowPaths.Resolve(Path.Combine(Here, "show.lampion"), ""));
    }
}
