using System.Diagnostics;
using System.Text.Json;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;

namespace SilverAudioPlayer.Any.PlayStreamProvider.SLibIndex;

public sealed class SLibIndexAutoPlayController
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SLibIndexRecommender");

    private static readonly string SettingsJsonPath = Path.Combine(SettingsDir, "settings.json");

    private sealed class ControllerSettings
    {
        public bool AutoPlayEnabled { get; set; }
        public string ServerUrl { get; set; } = "http://192.168.1.173:32542/api";
    }

    private readonly Lock _sync = new();
    private readonly Lock _fileSync = new();
    private readonly List<SLibIndexLiteTrack> _allTracks = [];

    private IPlayStreamProviderListener? _playListener;
    private IMusicStatusInterfaceListener? _statusListener;
    private bool _statusEventsHooked;
    private volatile bool _isAutoPlayEnabled;
    private string _serverUrl = "http://192.168.1.173:32542/api";

    private SLibIndexLiteTrack? _currentPlayingTrack;
    private SLibIndexLiteTrack? _previousPlayedTrack;
    private SLibIndexLiteTrack? _preloadedNextTrack;
    private string? _preloadedNextReason;
    private double _lastReportedPosition;
    private double _currentTrackDuration;
    private volatile bool _preloadingInProgress;
    private volatile bool _loadingLibrary;

    public HttpClient HttpClient { get; } = new();
    public SLibIndexRecommender Recommender { get; } = new();

    public string LastStatusText { get; private set; } =
        "Connect to load tracks from your SLibIndex server.";
    public string LastRecommendReasonText { get; private set; } = "";

    public event Action<string>? StatusChanged;
    public event Action<string>? RecommendReasonChanged;
    public event Action? TracksReloaded;
    public event Action<SLibIndexLiteTrack>? AutoPlayTrackStarted;

    public SLibIndexAutoPlayController()
    {
        LoadSettings();
    }

    public bool IsAutoPlayEnabled
    {
        get => _isAutoPlayEnabled;
        set
        {
            if (_isAutoPlayEnabled == value) return;
            _isAutoPlayEnabled = value;
            SaveSettings();

            if (_playListener != null)
            {
                _playListener.AutoSortEnabled = !value;
            }

            if (value)
            {
                SetStatus("Auto-Play enabled.");
                _ = EnsureLibraryAndPreloadAsync();
            }
            else
            {
                SetStatus("Auto-Play disabled.");
            }
        }
    }

    public string ServerUrl
    {
        get
        {
            lock (_sync) return _serverUrl;
        }
        set
        {
            var normalized = NormalizeApiBase(value);
            lock (_sync)
            {
                if (_serverUrl == normalized) return;
                _serverUrl = normalized;
            }
            SaveSettings();
        }
    }

    public IReadOnlyList<SLibIndexLiteTrack> AllTracks
    {
        get
        {
            lock (_sync) return [.. _allTracks];
        }
    }

    public SLibIndexLiteTrack? CurrentPlayingTrack => _currentPlayingTrack;
    public SLibIndexLiteTrack? PreviousPlayedTrack => _previousPlayedTrack;
    public bool IsStarted => _statusEventsHooked;

    public void AttachStatusListener(IMusicStatusInterfaceListener listener)
    {
        lock (_sync)
        {
            if (listener is IPlayStreamProviderListener playListener)
            {
                _playListener = playListener;
                if (_isAutoPlayEnabled)
                {
                    playListener.AutoSortEnabled = false;
                }
            }

            if (ReferenceEquals(_statusListener, listener) && _statusEventsHooked)
                return;

            DetachStatusListenerLocked();
            _statusListener = listener;
            _statusListener.TrackChangedNotification += StatusListener_TrackChanged;
            _statusListener.PositionChanged += StatusListener_PositionChanged;
            _statusListener.PlayerStateChanged += StatusListener_PlayerStateChanged;
            _statusEventsHooked = true;
        }

        if (_isAutoPlayEnabled)
        {
            _ = EnsureLibraryAndPreloadAsync();
        }
    }

    public void AttachPlayListener(IPlayStreamProviderListener listener)
    {
        lock (_sync)
        {
            _playListener = listener;
            if (_isAutoPlayEnabled)
            {
                listener.AutoSortEnabled = false;
            }
        }

        if (listener is IMusicStatusInterfaceListener statusListener)
        {
            AttachStatusListener(statusListener);
        }
    }

    public void DetachStatusListener(IMusicStatusInterfaceListener listener)
    {
        lock (_sync)
        {
            if (!ReferenceEquals(_statusListener, listener)) return;
            DetachStatusListenerLocked();
        }
    }

    private void DetachStatusListenerLocked()
    {
        if (_statusListener != null && _statusEventsHooked)
        {
            _statusListener.TrackChangedNotification -= StatusListener_TrackChanged;
            _statusListener.PositionChanged -= StatusListener_PositionChanged;
            _statusListener.PlayerStateChanged -= StatusListener_PlayerStateChanged;
        }
        _statusEventsHooked = false;
        _statusListener = null;
    }

    private async Task EnsureLibraryAndPreloadAsync()
    {
        if (AllTracks.Count == 0 && !_loadingLibrary)
        {
            try
            {
                await LoadLibraryAsync(ServerUrl, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Auto-Play background library load failed: {ex.Message}");
            }
        }

        if (_isAutoPlayEnabled && _currentPlayingTrack != null)
        {
            TriggerPreloadNextTrack();
        }
    }

    public async Task<int> LoadLibraryAsync(string? serverUrlInput, CancellationToken token)
    {
        _loadingLibrary = true;
        try
        {
            var apiBase = NormalizeApiBase(serverUrlInput);
            ServerUrl = apiBase;
            SetStatus("Loading the lightweight track list…");

            var url = $"{apiBase}/tracks/lite";
            using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var tracks = await JsonSerializer.DeserializeAsync<List<SLibIndexLiteTrack>>(stream, JsonOptions, token)
                         ?? [];

            var filtered = tracks.Where(t => t.Guid != Guid.Empty).ToList();
            lock (_sync)
            {
                _allTracks.Clear();
                _allTracks.AddRange(filtered);
            }

            SetStatus($"Loaded {filtered.Count:N0} tracks. Double-click to play, or click 'Smart Next' / 'Play Radio Mix'.");
            TracksReloaded?.Invoke();
            return filtered.Count;
        }
        finally
        {
            _loadingLibrary = false;
        }
    }

    public async Task<bool> PlayTrackAsync(SLibIndexLiteTrack track, string? reason = null)
    {
        var playListener = _playListener;
        if (playListener is null) return false;

        SetStatus($"Opening {track.Title}…");
        WrappedStream? stream = null;
        try
        {
            var apiBase = ServerUrl;
            var streamUrl = $"{apiBase}/tracks/{track.Guid:D}/download";
            var streamTask = Task.Run(() => new WrappedHttpStream(streamUrl, HttpClient));
            var lyricsTask = track.HasLyrics || track.HasSyncedLyrics
                ? LoadLyricsAsync(apiBase, track)
                : Task.CompletedTask;

            stream = await streamTask;

            if (_isAutoPlayEnabled)
            {
                playListener.AutoSortEnabled = false;
            }

            Recommender.EnrichMetadata(track, AllTracks);
            playListener.LoadSong(stream, track);
            Recommender.RecordPlayed(track.Guid);
            _currentPlayingTrack = track;
            _currentTrackDuration = (track.Duration ?? 0) / 1000.0;
            _lastReportedPosition = 0;
            _preloadedNextTrack = null;
            _preloadedNextReason = null;

            stream = null;
            SetStatus($"Playing {track.Title}." + (string.IsNullOrEmpty(reason) ? "" : $" ({reason})"));
            if (!string.IsNullOrEmpty(reason))
            {
                SetRecommendReason($"Now playing: {track.Title} - {reason}");
            }

            if (lyricsTask != Task.CompletedTask)
                _ = lyricsTask;

            return true;
        }
        catch (Exception ex)
        {
            stream?.Dispose();
            SetStatus($"Could not play {track.Title}: {ex.Message}");
            return false;
        }
    }

    public async Task QueueRadioMixAsync(IReadOnlyList<SLibIndexLiteTrack> mix)
    {
        var playListener = _playListener;
        if (playListener is null || mix.Count == 0) return;

        var apiBase = ServerUrl;
        var tracksSnapshot = AllTracks;
        var songTuples = await Task.Run(() =>
        {
            var list = new List<Tuple<WrappedStream, IMetadata?>>(mix.Count);
            foreach (var track in mix)
            {
                Recommender.EnrichMetadata(track, tracksSnapshot);
                var streamUrl = $"{apiBase}/tracks/{track.Guid:D}/download";
                var stream = new WrappedHttpStream(streamUrl, HttpClient);
                list.Add(new Tuple<WrappedStream, IMetadata?>(stream, track));
            }
            return list;
        });

        playListener.AutoSortEnabled = false;
        playListener.LoadSongs(songTuples);
        SetStatus($"Queued {mix.Count} radio tracks starting with {mix[0].Title}.");
        SetRecommendReason(
            $"Smart Radio: {mix.Count} cohesive tracks across {mix.Select(m => m.Artist).Distinct().Count()} artists (Auto-Sort overridden).");
    }

    public void TriggerPreloadNextTrack()
    {
        if (!_isAutoPlayEnabled || _preloadingInProgress || _preloadedNextTrack != null) return;
        var tracksSnapshot = AllTracks;
        var currentSnapshot = _currentPlayingTrack;
        if (tracksSnapshot.Count == 0 || currentSnapshot == null || _playListener == null) return;

        _preloadingInProgress = true;
        _ = Task.Run(async () =>
        {
            try
            {
                if (!_isAutoPlayEnabled || _preloadedNextTrack != null) return;
                SetStatus("Auto-Play: selecting next smart track…");

                var apiBase = ServerUrl;
                var pick = await Recommender.PickNextAsync(tracksSnapshot, currentSnapshot);
                if (pick?.Track == null || !_isAutoPlayEnabled || _preloadedNextTrack != null) return;

                var streamUrl = $"{apiBase}/tracks/{pick.Track.Guid:D}/download";
                var stream = new WrappedHttpStream(streamUrl, HttpClient);

                if (!_isAutoPlayEnabled || _preloadedNextTrack != null ||
                    _currentPlayingTrack?.Guid != currentSnapshot.Guid)
                {
                    stream.Dispose();
                    return;
                }

                _preloadedNextTrack = pick.Track;
                _preloadedNextReason = pick.Reason;

                var playListener = _playListener;
                if (playListener != null)
                {
                    Recommender.EnrichMetadata(pick.Track, tracksSnapshot);
                    playListener.AutoSortEnabled = false;
                    playListener.LoadSong(stream, pick.Track);
                }
                else
                {
                    stream.Dispose();
                    return;
                }

                SetStatus($"Auto-Play queued next: {pick.Track.Title} — {pick.Track.Artist}");
                SetRecommendReason($"Auto-Play Next: '{pick.Track.Title}' by '{pick.Track.Artist}' ({pick.Reason})");
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

    private void StatusListener_TrackChanged(object? sender, Song? song)
    {
        if (_currentPlayingTrack != null)
        {
            var prevTrack = _currentPlayingTrack;
            var playedSec = _lastReportedPosition;
            var durSec = _currentTrackDuration;

            if (durSec >= 60.0 && playedSec > 0 && playedSec < 25.0)
            {
                Recommender.RecordNegativeCue(prevTrack.Guid, _previousPlayedTrack?.Artist, prevTrack.Artist,
                    "Skipped early (<25s)");
            }
            else if (durSec > 0 && playedSec >= durSec * 0.70)
            {
                Recommender.RecordPositiveCue(prevTrack.Guid, _previousPlayedTrack?.Artist, prevTrack.Artist,
                    "Listened through");
            }

            _previousPlayedTrack = prevTrack;
        }

        var resolvedTrack = ResolveTrack(song);
        var wasPreloaded = resolvedTrack != null && _preloadedNextTrack?.Guid == resolvedTrack.Guid;
        var preloadedReason = _preloadedNextReason;

        _lastReportedPosition = 0;
        _currentTrackDuration = 0;
        _preloadedNextTrack = null;
        _preloadedNextReason = null;

        if (resolvedTrack != null)
        {
            _currentPlayingTrack = resolvedTrack;
            Recommender.RecordPlayed(resolvedTrack.Guid);
            _currentTrackDuration = (resolvedTrack.Duration ?? 0) / 1000.0;

            if (wasPreloaded)
            {
                SetStatus($"Auto-Play started: {resolvedTrack.Title} — {resolvedTrack.Artist}");
                if (!string.IsNullOrEmpty(preloadedReason))
                {
                    SetRecommendReason(
                        $"Auto-Play playing: '{resolvedTrack.Title}' by '{resolvedTrack.Artist}' ({preloadedReason})");
                }
                AutoPlayTrackStarted?.Invoke(resolvedTrack);
            }

            if (_isAutoPlayEnabled)
            {
                TriggerPreloadNextTrack();
            }
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
        if (AllTracks.Count == 0 || _currentPlayingTrack == null) return;

        if (pos.TotalSeconds >= 2.0)
        {
            TriggerPreloadNextTrack();
        }
    }

    private void StatusListener_PlayerStateChanged(object? sender, PlaybackState state)
    {
        if (state != PlaybackState.Stopped || !_isAutoPlayEnabled) return;

        _ = Task.Run(async () =>
        {
            var tracksSnapshot = AllTracks;
            if (!_isAutoPlayEnabled || tracksSnapshot.Count == 0) return;

            if (_preloadedNextTrack != null)
            {
                var queued = _preloadedNextTrack;
                var reason = _preloadedNextReason ?? "Auto-Play next";
                _preloadedNextTrack = null;
                _preloadedNextReason = null;
                SetStatus($"Auto-Play: advancing to {queued.Title}…");
                AutoPlayTrackStarted?.Invoke(queued);
                await PlayTrackAsync(queued, $"Auto-Play: {reason}");
                return;
            }

            SetStatus("Auto-Play: picking next smart track…");
            var pick = await Recommender.PickNextAsync(tracksSnapshot, _currentPlayingTrack);
            if (pick?.Track != null)
            {
                AutoPlayTrackStarted?.Invoke(pick.Track);
                await PlayTrackAsync(pick.Track, $"Auto-Play: {pick.Reason}");
            }
        });
    }

    private SLibIndexLiteTrack? ResolveTrack(Song? song)
    {
        if (song?.Metadata is SLibIndexLiteTrack lite)
            return lite;

        var title = song?.Metadata?.Title?.Trim();
        if (string.IsNullOrEmpty(title)) return null;
        var artist = song?.Metadata?.Artist?.Trim();

        lock (_sync)
        {
            return _allTracks.FirstOrDefault(t =>
                string.Equals(t.Title?.Trim(), title, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(artist) ||
                 string.Equals(t.Artist?.Trim(), artist, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private async Task LoadLyricsAsync(string apiBase, SLibIndexLiteTrack track)
    {
        try
        {
            using var response = await HttpClient.GetAsync($"{apiBase}/tracks/{track.Guid:D}/lyrics");
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
            // Lyrics are optional.
        }
    }

    private static void ReadLyrics(JsonElement root, SLibIndexLiteTrack track)
    {
        if (root.TryGetProperty("lyrics", out var lyricsProp) && lyricsProp.ValueKind == JsonValueKind.String)
            track.Lyrics = lyricsProp.GetString();

        if (!root.TryGetProperty("syncedLyrics", out var syncedProp) || syncedProp.ValueKind != JsonValueKind.Array)
            return;

        var phrases = new List<LyricPhrase>();
        foreach (var item in syncedProp.EnumerateArray())
        {
            if (item.TryGetProperty("timeStampInMilliSeconds", out var tsProp) &&
                item.TryGetProperty("content", out var contentProp) &&
                tsProp.TryGetInt32(out var ms))
            {
                phrases.Add(new LyricPhrase(ms, contentProp.GetString() ?? ""));
            }
        }
        track.SyncedLyrics = phrases;
    }

    public static string NormalizeApiBase(string? input)
    {
        var value = (input ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(value))
            value = "http://192.168.1.173:32542/api";
        if (!value.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            value += "/api";
        return value;
    }

    private void SetStatus(string text)
    {
        LastStatusText = text;
        StatusChanged?.Invoke(text);
    }

    private void SetRecommendReason(string text)
    {
        LastRecommendReasonText = text;
        RecommendReasonChanged?.Invoke(text);
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsJsonPath)) return;
            var json = File.ReadAllText(SettingsJsonPath);
            var settings = JsonSerializer.Deserialize<ControllerSettings>(json, JsonOptions);
            if (settings == null) return;

            _isAutoPlayEnabled = settings.AutoPlayEnabled;
            if (!string.IsNullOrWhiteSpace(settings.ServerUrl))
                _serverUrl = NormalizeApiBase(settings.ServerUrl);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load SLibIndex settings: {ex.Message}");
        }
    }

    private void SaveSettings()
    {
        try
        {
            ControllerSettings settings;
            lock (_sync)
            {
                settings = new ControllerSettings
                {
                    AutoPlayEnabled = _isAutoPlayEnabled,
                    ServerUrl = _serverUrl
                };
            }

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            lock (_fileSync)
            {
                Directory.CreateDirectory(SettingsDir);
                var tmpPath = $"{SettingsJsonPath}.{Environment.ProcessId}.tmp";
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, SettingsJsonPath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save SLibIndex settings: {ex.Message}");
        }
    }
}
