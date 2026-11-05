using System.Globalization;
using Lampion.Core.Export;
using Lampion.Core.Model;

namespace Lampion.Core.Tests.Golden;

/// <summary>The exported QLC+ workspace (and its warnings) for the demo and the everything-project, pinned.</summary>
public class QlcGoldenTests
{
    [Fact]
    public void DemoExportsTheGoldenWorkspace() => GoldenFile.AssertMatches("demo.qxw.txt", Export(DemoProject.Create()));

    [Fact]
    public void EverythingProjectExportsTheGoldenWorkspace() => GoldenFile.AssertMatches("everything.qxw.txt", Export(EverythingProject.Create()));

    private static string Export(Project project)
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var result = new QlcExporter(project, new QlcExportOptions { SampleRate = 20, Tolerance = 2 }).Export();
            var header = $"fixtures {result.FixtureCount}\nsteps {result.StepCount}\n" + string.Concat(result.Warnings.Select(w => $"warning {w}\n"));
            return header + result.Xml;
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
