using Lampion.Core.Model.Timeline;

namespace Lampion.App.Controls.Timeline;

internal sealed partial class TrackSummaryCache
{
    /// <summary>One track's summary and the inputs it was built from.</summary>
    private sealed class Entry
    {
        private string[] _names = [];
        private int _nameCount;

        /// <summary>The summary, or null before the first <see cref="Update"/>.</summary>
        public string? Text { get; private set; }

        /// <summary>The explicit fixture count <see cref="Text"/> was built from.</summary>
        public int FixtureCount { get; private set; }

        /// <summary>The blend mode <see cref="Text"/> was built from.</summary>
        public BlendMode Blend { get; private set; }

        /// <summary>Whether <paramref name="names"/> are the very group-name strings <see cref="Text"/> was built from.</summary>
        public bool SameNames(List<string> names)
        {
            if (names.Count != _nameCount)
                return false;
            for (var i = 0; i < names.Count; i++)
                if (!ReferenceEquals(names[i], _names[i]))
                    return false;
            return true;
        }

        /// <summary>Stores a rebuilt summary and the inputs it came from.</summary>
        public void Update(string text, Track track, List<string> names)
        {
            Text = text;
            FixtureCount = track.FixtureIds.Count;
            Blend = track.Blend;
            if (_names.Length < names.Count)
                _names = new string[names.Count];
            names.CopyTo(_names);
            _nameCount = names.Count;
        }
    }
}
