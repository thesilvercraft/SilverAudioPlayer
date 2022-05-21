using SilverAudioPlayer.Shared;
using ATL;

namespace SilverAudioPlayer.MetadataSource.Z440AtlCore
{
    public class AtlCoreMetadata : Metadata
    {
        public AtlCoreMetadata(Track theTrack)
        {
            OGInfo = theTrack;
            Title = theTrack.Title;
            Artist = theTrack.Artist;
            Album = theTrack.Album;
            Genre = theTrack.Genre;
            Year = theTrack.Year;
            TrackNumber = theTrack.TrackNumber;
            Duration = theTrack.DurationMs;
            Bitrate = (ulong?)theTrack.Bitrate;
            SampleRate = (ulong?)theTrack.SampleRate;
            Channels = (uint?)theTrack.ChannelsArrangement.NbChannels;
            Pictures = theTrack.EmbeddedPictures.Select(x => new ATLCOREPicture(x)).ToList();
            Lyrics = theTrack.Lyrics.UnsynchronizedLyrics;
            DiscNumber = theTrack.DiscNumber;
        }

        public Track OGInfo { get; init; }
    }
}