using System.Xml.Linq;
using Lampion.Core.Engine;
using Lampion.Core.Export;
using Lampion.Core.Model;
using Lampion.Core.Output;
using Lampion.Core.Persistence;

namespace Lampion.Core.Tests;

public class QlcExporterTests
{
    private static readonly XNamespace Ns = QlcExporter.Namespace;

    [Fact]
    public void DemoProjectExportsAWellFormedWorkspace()
    {
        var project = Project.CreateDemo();
        var result = new QlcExporter(project).Export();

        Assert.Contains("<!DOCTYPE Workspace>", result.Xml);
        var doc = XDocument.Parse(result.Xml);
        var engine = doc.Root!.Element(Ns + "Engine")!;
        Assert.Equal(project.Fixtures.Count, engine.Elements(Ns + "Fixture").Count());
        Assert.Equal("InputOutputMap", engine.Elements().First().Name.LocalName);

        var functions = engine.Elements(Ns + "Function").ToList();
        var show = Assert.Single(functions, f => (string?)f.Attribute("Type") == "Show");
        Assert.Equal(project.Groups.Count, show.Elements(Ns + "Track").Count());

        // Every sequence plays exactly as long as the show.
        var showMs = (int)Math.Round(project.ContentEnd * 1000);
        foreach (var sequence in functions.Where(f => (string?)f.Attribute("Type") == "Sequence"))
        {
            var total = sequence.Elements(Ns + "Step").Sum(s => (int)s.Attribute("FadeIn")! + (int)s.Attribute("Hold")!);
            Assert.Equal(showMs, total);
        }
    }

    [Fact]
    public void RgbwFixturesBecomeQlcRgbPanels()
    {
        var profiles = BuiltInProfiles.All();
        Assert.Equal("RGBW", QlcExporter.RgbPanelMode(profiles.First(p => p.Id == BuiltInProfiles.RgbwParId)));
        Assert.Equal("RGB", QlcExporter.RgbPanelMode(profiles.First(p => p.Id == BuiltInProfiles.RgbParId)));
        Assert.Null(QlcExporter.RgbPanelMode(profiles.First(p => p.Id == BuiltInProfiles.MovingHeadId)));
    }

    [Fact]
    public void SimplifyCollapsesARampIntoOneFade()
    {
        var times = Enumerable.Range(0, 11).Select(i => i / 10.0).ToList();
        var ramp = times.Select(t => new[] { (byte)(t * 200) }).ToList();
        Assert.Equal([0, 10], QlcExporter.Simplify(ramp, times, 1));
    }

    [Fact]
    public void SimplifyKeepsACut()
    {
        var times = Enumerable.Range(0, 6).Select(i => (double)i).ToList();
        var cut = new[] { 0, 0, 0, 255, 255, 255 }.Select(v => new[] { (byte)v }).ToList();
        Assert.Equal([0, 2, 3, 5], QlcExporter.Simplify(cut, times, 1));
    }

    [Fact]
    public void ReplayedStepsMatchTheRenderedShow()
    {
        var project = Project.CreateDemo();
        var options = new QlcExportOptions { SampleRate = 25, Tolerance = 1 };
        var doc = XDocument.Parse(new QlcExporter(project, options).Export().Xml);
        var engine = doc.Root!.Element(Ns + "Engine")!;
        var qlcIds = engine.Elements(Ns + "Fixture").ToDictionary(f => (string)f.Element(Ns + "Name")!, f => (int)f.Element(Ns + "ID")!);
        var sequence = engine.Elements(Ns + "Function").First(f => (string?)f.Attribute("Type") == "Sequence");

        // Rebuild the timeline of step values and compare a fixture against the renderer between samples.
        var steps = sequence.Elements(Ns + "Step").Select(s => (FadeIn: (int)s.Attribute("FadeIn")!, Text: s.Value)).ToList();
        var fixture = project.Fixtures.First(f => project.Groups[0].FixtureIds.Contains(f.Id));
        var qlcId = qlcIds[fixture.Name];
        var renderer = new ShowRenderer(project);
        var at = 0;
        byte[]? previous = null;
        foreach (var (fadeIn, text) in steps)
        {
            var values = ParseStep(text, qlcId);
            if (previous is not null && fadeIn > 40)
            {
                var midMs = at + fadeIn / 2;
                var expected = renderer.RenderFixtures(midMs / 1000.0)[fixture.Id];
                for (var c = 0; c < values.Length; c++)
                    Assert.InRange((previous[c] + values[c]) / 2.0, expected[c] - 2.5, expected[c] + 2.5);
            }
            at += fadeIn;
            previous = values;
        }
    }

    private static byte[] ParseStep(string text, int qlcId)
    {
        var parts = text.Split(':');
        for (var i = 0; i + 1 < parts.Length; i += 2)
            if (int.Parse(parts[i]) == qlcId)
            {
                var pairs = parts[i + 1].Split(',').Select(int.Parse).ToArray();
                return Enumerable.Range(0, pairs.Length / 2).Select(k => (byte)pairs[k * 2 + 1]).ToArray();
            }
        throw new InvalidOperationException($"Fixture {qlcId} not in step.");
    }

    [Fact]
    public void MergingReusesBaseFixturesAtTheSameAddress()
    {
        var project = Project.CreateDemo();
        var first = new QlcExporter(project).Export().Xml;
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, first);
            var result = new QlcExporter(project, new QlcExportOptions { BaseWorkspacePath = path }).Export();
            var engine = XDocument.Parse(result.Xml).Root!.Element(Ns + "Engine")!;
            Assert.Equal(project.Fixtures.Count, engine.Elements(Ns + "Fixture").Count());
            var shows = engine.Elements(Ns + "Function").Where(f => (string?)f.Attribute("Type") == "Show").Select(f => (string?)f.Attribute("Name")).ToList();
            Assert.Equal([project.Name, $"{project.Name} (2)"], shows);
            var ids = engine.Elements(Ns + "Function").Select(f => (int)f.Attribute("ID")!).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ProjectSurvivesAJsonRoundTrip()
    {
        var project = Project.CreateDemo();
        var copy = ProjectSerializer.Deserialize(ProjectSerializer.Serialize(project));
        Assert.Equal(ProjectSerializer.Serialize(project), ProjectSerializer.Serialize(copy));
        Assert.IsType<RainbowEffect>(copy.Tracks[1].Clips.OfType<LightClip>().Last().Effect);
    }

    [Fact]
    public void ArtNetPacketHasTheArtDmxHeader()
    {
        var packet = ArtNetSender.BuildPacket(0x123, new byte[512], 7);
        Assert.Equal("Art-Net\0"u8.ToArray(), packet[..8]);
        Assert.Equal([0x00, 0x50, 0, 14, 7, 0, 0x23, 0x01, 0x02, 0x00], packet[8..18]);
    }
}
