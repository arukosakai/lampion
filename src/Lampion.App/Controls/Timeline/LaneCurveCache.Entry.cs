using Avalonia;
using Avalonia.Media;
using Lampion.Core.Model.Animation;

namespace Lampion.App.Controls.Timeline;

internal sealed partial class LaneCurveCache
{
    /// <summary>One lane's sampled curve and everything it was sampled from.</summary>
    private sealed class Entry
    {
        // Sampling past the visible edges by at least this much lets short scrolls reuse the curve.
        private const double MinMargin = 200;

        private double[] _times = [];
        private double[] _values = [];
        private Easing[] _easings = [];
        private int _keyCount = -1;
        private bool _animated;
        private AutomationTarget _target;
        private double _pixelsPerSecond;
        private double _width;
        private double _height;
        private double _min;
        private double _max;
        private double _coverFrom;
        private double _coverTo;

        /// <summary>The sampled curve in clip-local pixels (origin at the span's top-left), or null before the first build.</summary>
        public StreamGeometry? Geometry { get; private set; }

        /// <summary>Whether <see cref="Geometry"/> was built from these inputs and spans the visible stretch.</summary>
        public bool Covers(AutomationLane lane, in LaneValueScale scale, double visibleFrom, double visibleTo, double pixelsPerSecond) =>
            Geometry is not null
            && _pixelsPerSecond == pixelsPerSecond
            && _width == scale.Bounds.Width
            && _height == scale.Bounds.Height
            && _min == scale.Min
            && _max == scale.Max
            && _animated == lane.Animated
            && _target == lane.Target
            && visibleFrom >= _coverFrom
            && visibleTo <= _coverTo
            && SameKeys(lane.Keys);

        /// <summary>Resamples the curve over the visible stretch plus a margin either side.</summary>
        public void Rebuild(AutomationLane lane, in LaneValueScale scale, double visibleFrom, double visibleTo, double pixelsPerSecond)
        {
            Snapshot(lane);
            _pixelsPerSecond = pixelsPerSecond;
            _width = scale.Bounds.Width;
            _height = scale.Bounds.Height;
            _min = scale.Min;
            _max = scale.Max;

            var margin = Math.Max(MinMargin, visibleTo - visibleFrom);
            var first = (long)Math.Max(0, Math.Floor((visibleFrom - margin) / SampleStep));
            var last = (long)Math.Floor(Math.Min(_width, visibleTo + margin) / SampleStep);
            _coverFrom = first == 0 ? double.NegativeInfinity : first * SampleStep;
            _coverTo = (last + 1) * SampleStep > _width ? double.PositiveInfinity : last * SampleStep;

            var top = scale.Bounds.Top;
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                for (var k = first; k <= last; k++)
                {
                    var x = k * SampleStep;
                    var p = new Point(x, scale.ValueToY(lane.Evaluate(x / pixelsPerSecond)) - top);
                    if (k == first)
                        g.BeginFigure(p, false);
                    else
                        g.LineTo(p);
                }
                if (last >= first)
                    g.EndFigure(false);
            }
            Geometry = geometry;
        }

        private bool SameKeys(List<Keyframe> keys)
        {
            if (keys.Count != _keyCount)
                return false;
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (key.Time != _times[i] || key.Value != _values[i] || key.Easing != _easings[i])
                    return false;
            }
            return true;
        }

        private void Snapshot(AutomationLane lane)
        {
            var keys = lane.Keys;
            if (_times.Length < keys.Count)
            {
                _times = new double[keys.Count];
                _values = new double[keys.Count];
                _easings = new Easing[keys.Count];
            }
            for (var i = 0; i < keys.Count; i++)
            {
                _times[i] = keys[i].Time;
                _values[i] = keys[i].Value;
                _easings[i] = keys[i].Easing;
            }
            _keyCount = keys.Count;
            _animated = lane.Animated;
            _target = lane.Target;
        }
    }
}
