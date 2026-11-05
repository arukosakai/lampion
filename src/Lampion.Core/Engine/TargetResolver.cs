using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Engine;

/// <summary>
/// Works out which fixtures a track drives, in the order its effects walk them, as fixture indices.
/// Reuses its scratch buffers, so it is allocation-free once warmed up. Not thread-safe.
/// </summary>
internal sealed class TargetResolver
{
    private int[] _seenAt = [];
    private int _stamp;
    private double[] _keys = [];

    /// <summary>
    /// Fills <paramref name="targets"/> with the track's fixtures: explicit ones first, then group members,
    /// deduplicated, unknown ids skipped, then ordered by <see cref="Track.Order"/> (a stable sort).
    /// </summary>
    public void Resolve(ProjectIndex index, Track track, List<int> targets)
    {
        targets.Clear();
        BeginPass(index.FixtureCount);
        foreach (var id in track.FixtureIds)
            Add(index, id, targets);
        foreach (var groupId in track.GroupIds)
            if (index.GroupById(groupId) is { } group)
                foreach (var id in group.FixtureIds)
                    Add(index, id, targets);
        Order(index.Project.Fixtures, targets, track.Order);
    }

    private void BeginPass(int fixtureCount)
    {
        if (_seenAt.Length < fixtureCount)
            _seenAt = new int[Math.Max(fixtureCount, _seenAt.Length * 2)];
        if (++_stamp == int.MaxValue)
        {
            Array.Clear(_seenAt);
            _stamp = 1;
        }
    }

    private void Add(ProjectIndex index, string id, List<int> targets)
    {
        var fixture = index.FixtureIndexOf(id);
        if (fixture < 0 || _seenAt[fixture] == _stamp)
            return;
        _seenAt[fixture] = _stamp;
        targets.Add(fixture);
    }

    private void Order(List<Fixture> fixtures, List<int> targets, FixtureOrder order)
    {
        var count = targets.Count;
        if (count < 2)
            return;
        // Summed in list order and divided once, like Enumerable.Average, so ties sort the same way.
        var sum = 0.0;
        for (var i = 0; i < count; i++)
            sum += fixtures[targets[i]].Position.X;
        var center = sum / count;

        bool descending;
        switch (order)
        {
            case FixtureOrder.LeftToRight or FixtureOrder.RightToLeft:
                descending = order == FixtureOrder.RightToLeft;
                FillKeys(fixtures, targets, center, fromCenter: false);
                break;
            case FixtureOrder.CenterOut or FixtureOrder.OutsideIn:
                descending = order == FixtureOrder.OutsideIn;
                FillKeys(fixtures, targets, center, fromCenter: true);
                break;
            default:
                return;
        }
        StableSort(targets, descending);
    }

    private void FillKeys(List<Fixture> fixtures, List<int> targets, double center, bool fromCenter)
    {
        if (_keys.Length < targets.Count)
            _keys = new double[Math.Max(targets.Count, _keys.Length * 2)];
        for (var i = 0; i < targets.Count; i++)
        {
            var x = fixtures[targets[i]].Position.X;
            _keys[i] = fromCenter ? Math.Abs(x - center) : x;
        }
    }

    /// <summary>Insertion sort by <see cref="_keys"/> using <see cref="double.CompareTo(double)"/>, keeping equal keys in list order (as LINQ's OrderBy does).</summary>
    private void StableSort(List<int> targets, bool descending)
    {
        for (var i = 1; i < targets.Count; i++)
        {
            var key = _keys[i];
            var target = targets[i];
            var j = i;
            while (j > 0 && (descending ? _keys[j - 1].CompareTo(key) < 0 : _keys[j - 1].CompareTo(key) > 0))
            {
                _keys[j] = _keys[j - 1];
                targets[j] = targets[j - 1];
                j--;
            }
            _keys[j] = key;
            targets[j] = target;
        }
    }
}
