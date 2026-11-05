namespace Lampion.App.Controls.Timeline;

/// <summary>
/// Running "closest candidate within tolerance" search used by <see cref="TimelineSnapper"/>. A mutable
/// accumulator: keep it in a local and call <see cref="Consider"/> on that local.
/// </summary>
internal struct Nearest
{
    private readonly double _target;
    private double _bestDistance;

    /// <summary>Starts a search around <paramref name="target"/>; candidates must be strictly closer than <paramref name="tolerance"/>.</summary>
    public Nearest(double target, double tolerance)
    {
        _target = target;
        _bestDistance = tolerance;
        Best = target;
    }

    /// <summary>The closest candidate so far, or the target when none was close enough.</summary>
    public double Best { get; private set; }

    /// <summary>Takes <paramref name="candidate"/> if it is closer than everything before it.</summary>
    public void Consider(double candidate)
    {
        var distance = Math.Abs(candidate - _target);
        if (distance < _bestDistance)
        {
            _bestDistance = distance;
            Best = candidate;
        }
    }
}
