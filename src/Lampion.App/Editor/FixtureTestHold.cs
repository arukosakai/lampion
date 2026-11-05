using Lampion.Core.Engine;
using Lampion.Core.Model;
using Lampion.Core.Model.Rig;

namespace Lampion.App.Editor;

/// <summary>
/// Lays a <see cref="FixtureTest"/> over the rendered show into a frame it owns and reuses, so holding test
/// values costs no allocations per tick. The target fixtures are resolved once per test or project edit.
/// </summary>
internal sealed class FixtureTestHold
{
    private readonly DmxFrame _held = new();
    private readonly List<Fixture> _targets = [];
    private FixtureTest? _resolvedTest;
    private Project? _resolvedProject;
    private int _resolvedVersion = -1;
    private FixtureTest? _heldTest;

    /// <summary>The values to hold, or null to let the show through untouched.</summary>
    public FixtureTest? Test { get; set; }

    /// <summary>
    /// <paramref name="rendered"/> with <see cref="Test"/> written over its target fixtures, or
    /// <paramref name="rendered"/> itself when there is no test. The returned frame is owned by this hold
    /// and overwritten by a later call.
    /// </summary>
    /// <param name="rendered">The show's output at the playhead.</param>
    /// <param name="renderedChanged">True when <paramref name="rendered"/> was re-rendered since the last call.</param>
    /// <param name="project">The project the fixture ids refer to.</param>
    /// <param name="version">The project's edit version; a change re-resolves the target fixtures.</param>
    public DmxFrame Apply(DmxFrame rendered, bool renderedChanged, Project project, int version)
    {
        if (Test is not { } test)
            return rendered;
        var retargeted = ResolveTargets(test, project, version);
        if (renderedChanged || retargeted || _heldTest != test)
        {
            rendered.CopyTo(_held);
            foreach (var fixture in _targets)
                _held.Write(fixture, test.Values);
            _heldTest = test;
        }
        return _held;
    }

    private bool ResolveTargets(FixtureTest test, Project project, int version)
    {
        if (test == _resolvedTest && ReferenceEquals(project, _resolvedProject) && version == _resolvedVersion)
            return false;
        _targets.Clear();
        foreach (var fixture in project.Fixtures)
            if (test.FixtureIds.Contains(fixture.Id))
                _targets.Add(fixture);
        _resolvedTest = test;
        _resolvedProject = project;
        _resolvedVersion = version;
        return true;
    }
}
