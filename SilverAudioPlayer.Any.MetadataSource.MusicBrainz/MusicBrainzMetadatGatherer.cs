using System.Composition;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using MetaBrainz.MusicBrainz;
using SilverAudioPlayer.Shared.Metadata;
[Export(typeof(IAdditionalMetadataGatherer))]
public class MusicBrainzMetadatGatherer : IAdditionalMetadataGatherer
{
    
    public IMetadata? GatherAdditionalMetadata(IMetadataCombo original)
    {
        try
        {
            var q = new Query("SilverAudioPlayer", "6.0", "https://gitlab.com/silvercraft/SilverAudioPlayer");
            var recordings =
                q.FindRecordings(
                    $"\"{original.Title}\" AND release:\"{original.Album}\" AND qdur:{(int)(original.Duration / 2000)}", 10);
            if (recordings.Results.Count <= 0) return null;
            foreach (var result in recordings.Results)
            {
                if (result.Score > 80)
                {
                    Debug.WriteLine("Almost certain this is the right recording");
                }
                JsonSerializerOptions options = new()
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    WriteIndented = true
                };
                Debug.WriteLine(JsonSerializer.Serialize(result,options ));
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
        return null;
    }
}