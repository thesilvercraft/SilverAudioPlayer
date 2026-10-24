using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace SilverAudioPlayer.Any.MusicStatusInterface.ListenBrainz;

public class ListenBrainzClient(HttpClient httpClient, string userToken)
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly string _userToken = userToken ?? throw new ArgumentNullException(nameof(userToken));
    private const string EndpointUrl = "https://api.listenbrainz.org/1/submit-listens";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Submits a "Playing Now" notification with optional additional metadata (MBIDs, tags, client info).
    /// </summary>
    public async Task SetNowPlayingAsync(
        string artist, 
        string title, 
        string album, 
        IReadOnlyDictionary<string, object>? additionalInfo = null, 
        CancellationToken ct = default) =>
        await PostPayloadAsync(BuildPayload("playing_now", artist, title, album, listenedAt: null, additionalInfo), ct);

    /// <summary>
    /// Submits a single scrobble listen.
    /// </summary>
    public async Task SubmitListenAsync(
        string artist, 
        string title, 
        string album, 
        DateTimeOffset listenedAt, 
        IReadOnlyDictionary<string, object>? additionalInfo = null, 
        CancellationToken ct = default) =>
        await PostPayloadAsync(BuildPayload("single", artist, title, album, listenedAt, additionalInfo), ct);

    private static object BuildPayload(
        string listenType, 
        string artist, 
        string title, 
        string album, 
        DateTimeOffset? listenedAt, 
        IReadOnlyDictionary<string, object>? additionalInfo)
    {
        var trackMetadata = new Dictionary<string, object?>
        {
            ["artist_name"] = artist,
            ["track_name"] = title,
            ["release_name"] = album
        };

        if (additionalInfo is { Count: > 0 })
        {
            trackMetadata["additional_info"] = additionalInfo;
        }

        var item = new Dictionary<string, object?>();
        if (listenedAt.HasValue)
        {
            item["listened_at"] = listenedAt.Value.ToUnixTimeSeconds();
        }
        item["track_metadata"] = trackMetadata;

        return new
        {
            listen_type = listenType,
            payload = new[] { item }
        };
    }

    private async Task PostPayloadAsync(object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, EndpointUrl);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        request.Headers.TryAddWithoutValidation("Authorization", $"Token {_userToken}");

        using var response = await _httpClient.SendAsync(request, ct);

        if (response.IsSuccessStatusCode) return;
        var errorBody = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException($"ListenBrainz HTTP {(int)response.StatusCode}: {errorBody}");
    }
}