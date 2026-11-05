using System.Text.Json.Serialization;
using Lampion.Core.Model.Rig;
using Lampion.Core.Model.Stage;
using Lampion.Core.Model.Timeline;

namespace Lampion.Core.Model;

/// <summary>
/// A show: the rig, the stage and the timeline. Plain data, edited in place by the App; nothing here raises
/// change notifications, so anything derived from it must be recomputed rather than cached. Not thread-safe.
/// Saved as a <c>.lampion</c> file by <see cref="Persistence.ProjectSerializer"/>.
/// </summary>
public sealed class Project
{
    /// <summary>File format version; bump it and migrate on load when the saved shape changes.</summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>Show name, also used for the QLC+ show function.</summary>
    public string Name { get; set; } = "Untitled show";

    /// <summary>Seconds. The show ends here even if clips run longer.</summary>
    public double Duration { get; set; } = 60;

    /// <summary>Tempo of the beat grid, in beats per minute.</summary>
    public double Bpm { get; set; } = 120;

    /// <summary>Seconds to the first downbeat, so the beat grid lines up with the music.</summary>
    public double BeatOffset { get; set; }

    /// <summary>The set and room; replaced wholesale when a stage layout is applied.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
    public StageSettings Stage { get; set; } = StageSettings.CreateDefault();

    /// <summary>Fixture types; ids are unique, and lookups take the first match if they are not.</summary>
    public List<FixtureProfile> Profiles { get; } = [];

    /// <summary>The patch. Engine buffers are indexed by position in this list.</summary>
    public List<Fixture> Fixtures { get; } = [];

    /// <summary>Fixture groups tracks can target.</summary>
    public List<FixtureGroup> Groups { get; } = [];

    /// <summary>Timeline tracks, top first; the first light track is drawn on top.</summary>
    public List<Track> Tracks { get; } = [];

    /// <summary>Timeline markers, in no particular order.</summary>
    public List<Marker> Markers { get; } = [];

    /// <summary>Every clip on every track, in track order. Lazily enumerated; also written to project files (read-only there).</summary>
    public IEnumerable<Clip> AllClips => Tracks.SelectMany(t => t.Clips);

    /// <summary>Seconds until everything has finished: <see cref="Duration"/> or the last clip's end, whichever is later. Allocation-free; also written to project files.</summary>
    public double ContentEnd
    {
        get
        {
            // Same NaN handling as Enumerable.Max: a NaN end only wins when every clip's end is NaN.
            var any = false;
            var last = 0.0;
            foreach (var track in Tracks)
                foreach (var clip in track.Clips)
                {
                    var end = clip.End;
                    if (!any || end > last || double.IsNaN(last))
                        last = end;
                    any = true;
                }
            return Math.Max(Duration, last);
        }
    }

    /// <summary>The fixture's profile, or null when its <see cref="Fixture.ProfileId"/> matches none. Linear in the profile count; allocation-free.</summary>
    public FixtureProfile? ProfileOf(Fixture fixture)
    {
        var id = fixture.ProfileId;
        foreach (var profile in Profiles)
            if (profile.Id == id)
                return profile;
        return null;
    }

    /// <summary>The first fixture with this id, or null. Linear in the fixture count; allocation-free.</summary>
    public Fixture? FixtureById(string id)
    {
        foreach (var fixture in Fixtures)
            if (fixture.Id == id)
                return fixture;
        return null;
    }
}
