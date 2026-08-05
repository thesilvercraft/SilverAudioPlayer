using System.Composition;
using System.Text.Json;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Any.MetadataSource.SLIbIndex;

[Export(typeof(IAdditionalMetadataGatherer))]
public class SLibIndexMetadataGatherer : IMetadataProvider
{
    HttpClient _client ;

    public SLibIndexMetadataGatherer(HttpClient client)
    {
        _client = client;
    }

    public string Name =>"SLibIndex";
    public string Description => "Gathers metadata from SLibIndex";
    public WrappedStream? Icon => null;
    public Version? Version => null;
    public string Licenses =>@"SilverAudioPlayer.Any.MetadataSource.SLIbIndex
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.";
    public List<Tuple<Uri, URLType>>? Links { get; }
    private JsonSerializerOptions options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private string MagicString = "/api/tracks/";
    public async Task<IMetadata?> GetMetadata(WrappedStream stream)
    {
        if (stream is not WrappedHttpStream { Url: not null } s || !s.Url.Contains(MagicString)) return null; //TODO: BETTER CHECK
        //  http://localhost:5244/api/tracks/1019dcae-1c83-3be6-c9af-1f400ab94688/download/17.%20Deep%20Breath%20Deep%20Breath%20-Reload-.flac                             
        //to
        //http://localhost:5244/api/tracks/1019dcae-1c83-3be6-c9af-1f400ab94688/lyrics
        var indexOfDownload = s.Url.IndexOf("download", StringComparison.Ordinal);
        if (indexOfDownload < 0) return null;
        var lyricUrl =  s.Url[0..indexOfDownload]+"lyrics";
        var resp = await _client.GetAsync(lyricUrl);
        if (!resp.IsSuccessStatusCode) return null;
        var lyricsInfo = await JsonSerializer.DeserializeAsync<LyricsInfo> (await resp.Content.ReadAsStreamAsync(),options);
        if (lyricsInfo == null) return null;
        SLibIndexMetadata metadata = new();
        metadata.SyncedLyrics = lyricsInfo.syncedLyrics;
        metadata.Lyrics = lyricsInfo.lyrics;
        return lyricsInfo.syncedLyrics is { Count: > 0 } ||lyricsInfo.lyrics != null  ? metadata : null;
    }

    public bool CanGetMetadata(WrappedStream stream)
    {
        return stream is WrappedHttpStream s && s.Url.Contains(MagicString); //TODO: BETTER CHECK
    }
}