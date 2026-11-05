using Lampion.Core.Model;

namespace Lampion.Core.Export;

/// <summary>
/// Writes a QLC+ 4 workspace (.qxw). Each lane — a fixture group, or a lone fixture — becomes a Sequence
/// bound to a hidden Scene, played by one track of a Show alongside the audio and video. Reads the project
/// without modifying it; safe to run on a background thread while nobody edits the project.
/// </summary>
/// <remarks>
/// QLC+ facts this relies on (4.14 source): <c>&lt;!DOCTYPE Workspace&gt;</c> is mandatory; the Engine
/// lists InputOutputMap, then fixtures, then functions; a step occupies FadeIn + Hold and its text is
/// <c>fixtureId:channel,value,…:fixtureId:…</c> with 0-based channels; a fixture that partly overlaps
/// another in the same universe is dropped on load, so it is left out here with a warning instead.
/// </remarks>
public sealed class QlcExporter
{
    /// <summary>The QLC+ workspace XML namespace.</summary>
    public const string Namespace = "http://www.qlcplus.org/Workspace";

    private readonly Project _project;
    private readonly QlcExportOptions _options;

    /// <summary>An exporter for <paramref name="project"/>; null options use the defaults.</summary>
    public QlcExporter(Project project, QlcExportOptions? options = null)
    {
        _project = project;
        _options = options ?? new QlcExportOptions();
    }

    /// <summary>Builds the workspace. Each call is independent.</summary>
    /// <exception cref="InvalidDataException">The base workspace is not valid XML or not a QLC+ project.</exception>
    /// <exception cref="IOException">The base workspace can't be read.</exception>
    public QlcExportResult Export()
    {
        var warnings = new List<string>();
        var workspace = _options.BaseWorkspacePath is { } basePath ? QlcWorkspace.Load(basePath) : QlcWorkspace.Create();

        var patch = QlcPatchPlanner.Plan(_project, workspace, warnings);
        workspace.EnsureUniverses(patch.Values.Select(p => p.Fixture.Universe).DefaultIfEmpty(1).Max());
        var added = patch.Values.Where(p => p.IsNew).ToList();
        workspace.AddFixtures(added.Select(p => QlcFixtureWriter.Element(workspace.Ns, p)).ToList());

        var showName = workspace.UniqueShowName(_project.Name, warnings);
        var functions = new QlcShowWriter(_project, _options, warnings).Build(workspace.Ns, showName, workspace.NextFunctionId(), patch, out var steps);
        workspace.AddFunctions(functions);

        QlcMonitorWriter.Write(workspace, _project, added);
        return new QlcExportResult(workspace.Serialize(), warnings, patch.Count, steps);
    }
}
