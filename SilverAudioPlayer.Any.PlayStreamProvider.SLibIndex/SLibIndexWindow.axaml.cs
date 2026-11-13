using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
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
    private readonly SLibIndexRecommender _recommender = new();
    private volatile bool _isAutoPlayEnabled;

    // Track playback state for implicit feedback & pre-queuing
    private SLibIndexLiteTrack? _currentPlayingTrack;
    private SLibIndexLiteTrack? _previousPlayedTrack;
    private SLibIndexLiteTrack? _preloadedNextTrack;
    private double _lastReportedPosition;
    private double _currentTrackDuration;
    private volatile bool _preloadingInProgress;

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
        SmartNextButton.Click += SmartNextButton_Click;
        RadioMixButton.Click += RadioMixButton_Click;
        LikeButton.Click += LikeButton_Click;
        NotAFitButton.Click += NotAFitButton_Click;

        AutoPlayCheckBox.IsCheckedChanged += (_, _) =>
        {
            _isAutoPlayEnabled = AutoPlayCheckBox.IsChecked == true;
            if (_listener != null)
            {
                // Auto Next overrides and disables auto-sort so queue retains the recommended sequence
                _listener.AutoSortEnabled = !_isAutoPlayEnabled;
            }
        };

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

        _isAutoPlayEnabled = AutoPlayCheckBox.IsChecked == true;
        if (_listener != null && _isAutoPlayEnabled)
        {
            _listener.AutoSortEnabled = false;
        }

        SetupStatusListener();
    }

    private void CancelAndDisposeArtCts()
    {
        try
        {
            var cts = Interlocked.Exchange(ref _artCts, null);
            cts?.Cancel();
            cts?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error disposing art CTS: {ex.Message}");
        }
    }

    private void CancelAndDisposeLoadCts()
    {
        try
        {
            var cts = Interlocked.Exchange(ref _loadCts, null);
            cts?.Cancel();
            cts?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error disposing load CTS: {ex.Message}");
        }
    }

    private void SetupStatusListener()
    {
        if (_listener is IMusicStatusInterfaceListener statusListener)
        {
            statusListener.PlayerStateChanged += StatusListener_PlayerStateChanged;
            statusListener.TrackChangedNotification += StatusListener_TrackChanged;
            statusListener.PositionChanged += StatusListener_PositionChanged;
        }
    }

    private void StatusListener_TrackChanged(object? sender, Song? song)
    {
        // 1. Implicit feedback check on the song that just ended or was skipped
        if (_currentPlayingTrack != null)
        {
            var prevTrack = _currentPlayingTrack;
            var playedSec = _lastReportedPosition;
            var durSec = _currentTrackDuration;

            if (durSec >= 60.0 && playedSec > 0 && playedSec < 25.0)
            {
                // Skipped early (< 25s) -> strong negative cue (contextual penalty + steering)
                _recommender.RecordNegativeCue(prevTrack.Guid, _previousPlayedTrack?.Artist, prevTrack.Artist,
                    "Skipped early (<25s)");
            }
            else if (durSec > 0 && playedSec >= durSec * 0.70)
            {
                // Listened to >= 70% -> positive cue
                _recommender.RecordPositiveCue(prevTrack.Guid, _previousPlayedTrack?.Artist, prevTrack.Artist,
                    "Listened through");
            }

            _previousPlayedTrack = prevTrack;
        }

        _lastReportedPosition = 0;
        _currentTrackDuration = 0;
        _preloadedNextTrack = null;

        // 2. Set new currently playing track
        if (song?.Metadata is SLibIndexLiteTrack liteTrack)
        {
            _currentPlayingTrack = liteTrack;
            _recommender.RecordPlayed(liteTrack.Guid);
            _currentTrackDuration = (liteTrack.Duration ?? 0) / 1000.0;
        }
        else
        {
            _currentPlayingTrack = null;
        }
    }

    private void StatusListener_PositionChanged(object? sender, TimeSpan pos)
    {
        _lastReportedPosition = pos.TotalSeconds;

        if (!_isAutoPlayEnabled || _preloadingInProgress || _preloadedNextTrack != null) return;
        if (_allTracks.Count == 0 || _currentPlayingTrack == null) return;

        var dur = _currentTrackDuration;
        if (dur <= 0 && _listener is IMusicStatusInterfaceListener msi)
        {
            dur = msi.GetDuration();
            _currentTrackDuration = dur;
        }

        // Run it earlier: pre-queue the next smart track 15 seconds before the current track finishes
        if (dur > 25.0 && (dur - pos.TotalSeconds) <= 15.0)
        {
            _preloadingInProgress = true;
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    if (!_isAutoPlayEnabled || _preloadedNextTrack != null) return;
                    var pick = await _recommender.PickNextAsync(_allTracks, _currentPlayingTrack);
                    if (pick?.Track != null)
                    {
                        _preloadedNextTrack = pick.Track;
                        var apiBase = NormalizeApiBase(ServerUrlBox.Text);
                        var streamUrl = $"{apiBase}/tracks/{pick.Track.Guid:D}/download";
                        var stream = new WrappedHttpStream(streamUrl, _httpClient);

                        // Ensure AutoSort remains disabled during auto next
                        if (_listener != null) _listener.AutoSortEnabled = false;

                        _listener?.LoadSong(stream, pick.Track);
                        RecommendReasonText.Text =
                            $"Pre-queued up next: '{pick.Track.Title}' by '{pick.Track.Artist}' ({pick.Reason})";
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error preloading next smart track: {ex.Message}");
                }
                finally
                {
                    _preloadingInProgress = false;
                }
            });
        }
    }

    private void StatusListener_PlayerStateChanged(object? sender, PlaybackState state)
    {
        if (state != PlaybackState.Stopped) return;
        if (!_isAutoPlayEnabled) return;

        // Fallback when queue finishes or stopped unexpectedly
        Dispatcher.UIThread.Post(async () =>
        {
            if (!_isAutoPlayEnabled || _allTracks.Count == 0) return;
            var pick = await _recommender.PickNextAsync(_allTracks, _currentPlayingTrack);
            if (pick?.Track != null)
            {
                TrackList.SelectedItem = pick.Track;
                TrackList.ScrollIntoView(pick.Track);
                await PlayTrackAsync(pick.Track, pick.Reason);
            }
        });
    }

    private void LikeButton_Click(object? sender, RoutedEventArgs e)
    {
        var target = _currentPlayingTrack ?? (TrackList.SelectedItem as SLibIndexLiteTrack);
        if (target != null)
        {
            _recommender.RecordPositiveCue(target.Guid, _previousPlayedTrack?.Artist, target.Artist,
                "User liked track");
            RecommendReasonText.Text = $"Liked '{target.Title}' by '{target.Artist}' 👍 (preference tuned)";
        }
    }

    private async void NotAFitButton_Click(object? sender, RoutedEventArgs e)
    {
        var target = _currentPlayingTrack ?? (TrackList.SelectedItem as SLibIndexLiteTrack);
        if (target != null)
        {
            _recommender.RecordNegativeCue(target.Guid, _previousPlayedTrack?.Artist, target.Artist,
                "User flagged not a fit");
            RecommendReasonText.Text =
                $"Not a fit for this session: steering away from '{target.Title}' by '{target.Artist}'...";
        }

        await PlaySmartNextAsync();
    }

    private async void ConnectButton_Click(object? sender, RoutedEventArgs e)
    {
        CancelAndDisposeLoadCts();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var token = cts.Token;
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
                $"Loaded {_allTracks.Count:N0} tracks. Double-click to play, or click 'Smart Next' / 'Play Radio Mix'.";
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
        CancelAndDisposeArtCts();

        if (TrackList.SelectedItem is not SLibIndexLiteTrack track)
        {
            SelectedTrackText.Text = "Select a track to see its artwork.";
            CoverImage.Source = null;
            return;
        }

        SelectedTrackText.Text = $"{track.Title}\n{track.Artist} — {track.Album}";
        if (string.IsNullOrWhiteSpace(track.ArtHash))
        {
            CoverImage.Source = null;
            return;
        }

        var cts = new CancellationTokenSource();
        _artCts = cts;
        var token = cts.Token;

        try
        {
            var apiBase = NormalizeApiBase(ServerUrlBox.Text);
            var url = $"{apiBase}/art/{Uri.EscapeDataString(track.ArtHash)}";
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, token);
            memoryStream.Position = 0;

            _cover?.Dispose();
            _cover = new Bitmap(memoryStream);
            CoverImage.Source = _cover;
        }
        catch (OperationCanceledException)
        {
            // Expected on fast scrolling.
        }
        catch
        {
            CoverImage.Source = null;
        }
    }

    private async void TrackList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        await PlaySelectedTrackAsync();
    }

    private async void PlayButton_Click(object? sender, RoutedEventArgs e)
    {
        await PlaySelectedTrackAsync();
    }

    private async void SmartNextButton_Click(object? sender, RoutedEventArgs e)
    {
        await PlaySmartNextAsync();
    }

    private async Task PlaySmartNextAsync()
    {
        if (_allTracks.Count == 0)
        {
            StatusText.Text = "Please load the library first.";
            return;
        }

        var current = TrackList.SelectedItem as SLibIndexLiteTrack ?? _currentPlayingTrack;
        StatusText.Text = "Selecting smart next song based on session & feedback…";

        var pick = await _recommender.PickNextAsync(_allTracks, current);
        if (pick?.Track != null)
        {
            TrackList.SelectedItem = pick.Track;
            TrackList.ScrollIntoView(pick.Track);
            RecommendReasonText.Text = $"AI Pick: {pick.Track.Title} by {pick.Track.Artist} ({pick.Reason})";
            await PlayTrackAsync(pick.Track, pick.Reason);
        }
        else
        {
            StatusText.Text = "Could not find a next track recommendation.";
        }
    }

    private async void RadioMixButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_allTracks.Count == 0)
        {
            StatusText.Text = "Please load the library first.";
            return;
        }

        var seed = TrackList.SelectedItem as SLibIndexLiteTrack ??
                   _currentPlayingTrack ?? _allTracks[Random.Shared.Next(_allTracks.Count)];
        RadioMixButton.IsEnabled = false;
        StatusText.Text = $"Generating smart radio mix starting from {seed.Title}…";

        try
        {
            // Disable AutoSort so the radio mix queue preserves the smart ordered sequence
            if (_listener != null)
            {
                _listener.AutoSortEnabled = false;
            }

            var mix = await _recommender.GenerateRadioMixAsync(_allTracks, seed, count: 10);
            if (mix.Count == 0)
            {
                StatusText.Text = "Could not generate radio mix.";
                return;
            }

            var apiBase = NormalizeApiBase(ServerUrlBox.Text);
            var songTuples = new List<Tuple<WrappedStream, IMetadata?>>();
            foreach (var track in mix)
            {
                var streamUrl = $"{apiBase}/tracks/{track.Guid:D}/download";
                var stream = new WrappedHttpStream(streamUrl, _httpClient);
                songTuples.Add(new Tuple<WrappedStream, IMetadata?>(stream, track));
            }

            _listener?.LoadSongs(songTuples);
            StatusText.Text = $"Queued {mix.Count} radio tracks starting with {seed.Title}.";
            RecommendReasonText.Text =
                $"Smart Radio: {mix.Count} cohesive tracks across {mix.Select(m => m.Artist).Distinct().Count()} artists (Auto-Sort overridden).";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error generating radio mix: {ex.Message}";
        }
        finally
        {
            RadioMixButton.IsEnabled = true;
        }
    }

    private Task PlaySelectedTrackAsync()
    {
        if (TrackList.SelectedItem is SLibIndexLiteTrack track)
        {
            return PlayTrackAsync(track);
        }

        return Task.CompletedTask;
    }

    private async Task PlayTrackAsync(SLibIndexLiteTrack track, string? reason = null)
    {
        if (_listener is null)
            return;

        PlayButton.IsEnabled = false;
        if (SmartNextButton != null) SmartNextButton.IsEnabled = false;
        if (RadioMixButton != null) RadioMixButton.IsEnabled = false;

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

            if (_isAutoPlayEnabled && _listener != null)
            {
                _listener.AutoSortEnabled = false;
            }

            _listener?.LoadSong(stream, track);
            _recommender.RecordPlayed(track.Guid);
            _currentPlayingTrack = track;
            _currentTrackDuration = (track.Duration ?? 0) / 1000.0;
            _lastReportedPosition = 0;
            _preloadedNextTrack = null;

            stream = null;
            StatusText.Text = $"Playing {track.Title}." + (string.IsNullOrEmpty(reason) ? "" : $" ({reason})");
            if (!string.IsNullOrEmpty(reason))
            {
                RecommendReasonText.Text = $"Now playing: {track.Title} - {reason}";
            }

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
            if (SmartNextButton != null) SmartNextButton.IsEnabled = true;
            if (RadioMixButton != null) RadioMixButton.IsEnabled = true;
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

    private static string NormalizeApiBase(string? raw)
    {
        var trimmed = (raw ?? string.Empty).Trim().TrimEnd('/');
        return string.IsNullOrEmpty(trimmed) ? "http://127.0.0.1:32542/api" : trimmed;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        CancelAndDisposeLoadCts();
        CancelAndDisposeArtCts();
        _cover?.Dispose();

        if (_listener is IMusicStatusInterfaceListener statusListener)
        {
            statusListener.PlayerStateChanged -= StatusListener_PlayerStateChanged;
            statusListener.TrackChangedNotification -= StatusListener_TrackChanged;
            statusListener.PositionChanged -= StatusListener_PositionChanged;
        }
    }
}