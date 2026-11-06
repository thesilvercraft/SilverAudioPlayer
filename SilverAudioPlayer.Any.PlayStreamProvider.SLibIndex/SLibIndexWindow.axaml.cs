using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverCraft.AvaloniaUtils;

namespace SilverAudioPlayer.Any.PlayStreamProvider.SLibIndex;

public partial class SLibIndexWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private HttpClient _httpClient = new();
    private IPlayStreamProviderListener? _listener;
    private Action<string>? _saveServerUrl;
    private readonly List<SLibIndexLiteTrack> _allTracks = [];
    private readonly ObservableCollection<SLibIndexLiteTrack> _visibleTracks = [];
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _artCts;
    private Bitmap? _cover;

    public SLibIndexWindow()
    {
        InitializeComponent();
        ServerUrlBox.Text = "http://192.168.1.173:32542/api";
        TrackList.ItemsSource = _visibleTracks;
        ConnectButton.Click += ConnectButton_Click;
        SearchBox.TextChanged += SearchBox_TextChanged;
        TrackList.SelectionChanged += TrackList_SelectionChanged;
        TrackList.DoubleTapped += TrackList_DoubleTapped;
        PlayButton.Click += PlayButton_Click;
        Closed += Window_Closed;
        this.DoAfterInitTasks(true);
    }

    public SLibIndexWindow(HttpClient httpClient, IPlayStreamProviderListener listener, string serverUrl,
        Action<string> saveServerUrl) : this()
    {
        _httpClient = httpClient;
        _listener = listener;
        _saveServerUrl = saveServerUrl;
        ServerUrlBox.Text = serverUrl;
    }

    private async void ConnectButton_Click(object? sender, RoutedEventArgs e)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;
        ConnectButton.IsEnabled = false;
        StatusText.Text = "Loading the lightweight track list… Large libraries may take a while on a slow connection.";

        try
        {
            var apiBase = NormalizeApiBase(ServerUrlBox.Text);
            _saveServerUrl?.Invoke(apiBase);
            var url = $"{apiBase}/tracks/lite";
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var tracks = await JsonSerializer.DeserializeAsync<List<SLibIndexLiteTrack>>(stream, JsonOptions, token)
                         ?? [];

            _allTracks.Clear();
            _allTracks.AddRange(tracks.Where(track => track.Guid != Guid.Empty));
            FilterTracks();
            StatusText.Text =
                $"Loaded {_allTracks.Count:N0} tracks. Search this list; double-click a track to play it.";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Loading cancelled.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not load the library: {exception.Message}";
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) => FilterTracks();

    private void FilterTracks()
    {
        var query = SearchBox.Text?.Trim();
        IEnumerable<SLibIndexLiteTrack> matches = _allTracks;
        if (!string.IsNullOrEmpty(query))
        {
            matches = matches.Where(track =>
                Contains(track.Title, query) || Contains(track.Artist, query) ||
                Contains(track.Album, query) || Contains(track.FileName, query));
        }

        TrackList.SelectedItem = null;
        _visibleTracks.Clear();
        foreach (var track in matches)
            _visibleTracks.Add(track);
    }

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

    private async void TrackList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _artCts?.Cancel();
        _artCts?.Dispose();
        _artCts = new CancellationTokenSource();
        var token = _artCts.Token;
        SetCover(null);

        if (TrackList.SelectedItem is not SLibIndexLiteTrack track)
        {
            SelectedTrackText.Text = "Select a track to see its artwork.";
            return;
        }

        SelectedTrackText.Text = $"{track.Title}\n{track.Artist}\n{track.Album}";
        if (!track.HasArt)
            return;

        try
        {
            var apiBase = NormalizeApiBase(ServerUrlBox.Text);
            var artPath = string.IsNullOrWhiteSpace(track.ArtHash)
                ? $"tracks/{track.Guid:D}/art"
                : $"art/{Uri.EscapeDataString(track.ArtHash)}";
            var bytes = await _httpClient.GetByteArrayAsync($"{apiBase}/{artPath}", token);
            if (token.IsCancellationRequested)
                return;

            using var memory = new MemoryStream(bytes);
            SetCover(Bitmap.DecodeToHeight(memory, 300));
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Artwork is optional; keep the track usable when its image is unavailable.
        }
    }

    private void TrackList_DoubleTapped(object? sender, RoutedEventArgs e) => _ = PlaySelectedTrackAsync();

    private void PlayButton_Click(object? sender, RoutedEventArgs e) => _ = PlaySelectedTrackAsync();

    private async Task PlaySelectedTrackAsync()
    {
        if (_listener is null || TrackList.SelectedItem is not SLibIndexLiteTrack track)
            return;

        PlayButton.IsEnabled = false;
        StatusText.Text = $"Opening {track.Title}…";
        WrappedStream? stream = null;
        try
        {
            var apiBase = NormalizeApiBase(ServerUrlBox.Text);
            var streamUrl = $"{apiBase}/tracks/{track.Guid:D}/download";
            var streamTask = Task.Run(() => new WrappedHttpStream(streamUrl, _httpClient));
            var lyricsTask = track.HasLyrics || track.HasSyncedLyrics
                ? LoadLyricsAsync(apiBase, track)
                : Task.CompletedTask;

            stream = await streamTask;
            _listener?.LoadSong(stream, track);
            stream = null;
            StatusText.Text = $"Playing {track.Title}.";

            // Lyrics are optional and can be slow to retrieve, so let playback begin first.
            if (lyricsTask != Task.CompletedTask)
                _ = lyricsTask;
        }
        catch (Exception exception)
        {
            stream?.Dispose();
            StatusText.Text = $"Could not play {track.Title}: {exception.Message}";
        }
        finally
        {
            PlayButton.IsEnabled = true;
        }
    }

    private async Task LoadLyricsAsync(string apiBase, SLibIndexLiteTrack track)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"{apiBase}/tracks/{track.Guid:D}/lyrics");
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            try
            {
                using var document = JsonDocument.Parse(body);
                ReadLyrics(document.RootElement, track);
            }
            catch (JsonException)
            {
                track.Lyrics = body;
            }
        }
        catch
        {
            // Lyrics are optional and should not prevent playback.
        }
    }

    private static void ReadLyrics(JsonElement root, SLibIndexLiteTrack track)
    {
        if (root.ValueKind == JsonValueKind.String)
        {
            track.Lyrics = root.GetString();
            return;
        }

        if (root.ValueKind != JsonValueKind.Object)
            return;

        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.Equals("lyrics", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Equals("plainLyrics", StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                    track.Lyrics = property.Value.GetString();
            }
            else if (property.Name.Equals("syncedLyrics", StringComparison.OrdinalIgnoreCase) &&
                     property.Value.ValueKind == JsonValueKind.Array)
            {
                var phrases = new List<LyricPhrase>();
                foreach (var phrase in property.Value.EnumerateArray())
                {
                    if (phrase.ValueKind != JsonValueKind.Object)
                        continue;
                    var timestamp = ReadNumber(phrase, "timeStampInMilliSeconds", "timestamp", "timeStamp", "time");
                    var content = ReadString(phrase, "content", "text", "line");
                    if (timestamp.HasValue && content != null)
                        phrases.Add(new LyricPhrase(timestamp.Value, content));
                }

                if (phrases.Count > 0)
                    track.SyncedLyrics = phrases;
            }
        }
    }

    private static double? ReadNumber(JsonElement element, params string[] names)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                continue;
            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out var number))
                return number;
            if (property.Value.ValueKind == JsonValueKind.String &&
                double.TryParse(property.Value.GetString(), out number))
                return number;
        }

        return null;
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (names.Contains(property.Name, StringComparer.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
                return property.Value.GetString();
        }

        return null;
    }

    private static string NormalizeApiBase(string? serverUrl)
    {
        var value = serverUrl?.Trim().TrimEnd('/') ?? "";
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Enter the SLibIndex server URL.");

        if (value.EndsWith("/api/tracks", StringComparison.OrdinalIgnoreCase))
            value = value[..^"/tracks".Length];
        else if (value.EndsWith("/tracks", StringComparison.OrdinalIgnoreCase))
            value = value[..^"/tracks".Length];

        return value.EndsWith("/api", StringComparison.OrdinalIgnoreCase) ? value : $"{value}/api";
    }

    private void SetCover(Bitmap? bitmap)
    {
        var old = _cover;
        _cover = bitmap;
        CoverImage.Source = bitmap;
        old?.Dispose();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _artCts?.Cancel();
        _artCts?.Dispose();
        SetCover(null);
    }
}