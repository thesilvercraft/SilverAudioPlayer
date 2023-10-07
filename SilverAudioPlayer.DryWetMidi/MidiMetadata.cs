using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.MetadataSource.DryWetMidi;

public class MidiMetadata : Metadata
{
    public MidiMetadata(MidiFile theTrack)
    {
        OGInfo = theTrack;
        Title = null;
        Artist = null;
        Album = null;
        Genre = null;
        Year = null;
        TrackNumber = null;
        Duration = theTrack.GetDuration<MetricTimeSpan>().TotalMilliseconds;
        Bitrate = null;
        SampleRate = null;
        Channels = (uint?)theTrack.GetChannels().Count();
        Pictures = null;
        Lyrics = string.Concat(theTrack.GetTimedEvents().Select(x =>
        {
            if (x.Event is LyricEvent y) return y.Text;
            return null;
        }).Where(x => x != null).ToArray());
        SyncedLyrics = theTrack.GetTimedEvents().Where(x => x.Event is LyricEvent).Select(x =>
        {
            if (x.Event is LyricEvent y)
                return new LyricPhrase(x.TimeAs<MetricTimeSpan>(theTrack.GetTempoMap()).TotalMilliseconds, y.Text);
            return null;
        }).Where(x => x is not null).ToList();
        DiscNumber = 0;
    }

    public MidiFile OGInfo { get; set; }
    public override void Dispose()
    {
        OGInfo = null;
    }
}