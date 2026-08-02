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

    private string MagicString = ":32542/tracks";
    public async Task<IMetadata?> GetMetadata(WrappedStream stream)
    {
        if (stream is not WrappedHttpStream { Url: not null } s || !s.Url.Contains(MagicString)) return null; //TODO: BETTER CHECK
        //http://fridge.lan:32542/tracks/eff39dcd-85fc-dd45-ed36-bc7677293370/download/B1%20-%20%20Clean%2C%20Clean!.wav
        //                                ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
        //to
        //http://fridge.lan:32542/tracks/a3d0c9ea-8058-1b6a-bc26-fe4617fac22b/lyrics
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