using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.PlayStreamProvider.SLibIndex;

public class RecommendationResult
{
    public SLibIndexLiteTrack? Track { get; set; }
    public double Score { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class SLibIndexRecommender
{
    private const double MinTrackDurationSec = 60.0;
    private const double MaxTrackDurationSec = 600.0;
    private const double FallbackMinDurationSec = 75.0;
    private const double FallbackMaxDurationSec = 420.0;

    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SLibIndexRecommender");

    private static readonly string[] EmbeddingsNpzCandidates =
    [
        Path.Combine(CacheDir, "embeddings.npz"),
        "/var/home/silver/slib-audio-analysis/output/embeddings.npz",
        "/home/silver/slib-audio-analysis/output/embeddings.npz"
    ];

    private static readonly string[] AudioLabelsCandidates =
    [
        Path.Combine(CacheDir, "audio_labels.json"),
        "/var/home/silver/slib-audio-analysis/output/audio_labels.json",
        "/home/silver/slib-audio-analysis/output/audio_labels.json"
    ];

    private static readonly string EmbeddingsBinPath = Path.Combine(CacheDir, "embeddings.bin");
    private static readonly string MetadataJsonPath = Path.Combine(CacheDir, "track_metadata.json");
    private static readonly string ArtistKnowledgeJsonPath = Path.Combine(CacheDir, "artist_knowledge.json");
    private static readonly string UserProfileJsonPath = Path.Combine(CacheDir, "user_profile.json");
    private static readonly string UserFeedbackJsonPath = Path.Combine(CacheDir, "user_feedback.json");
    private static readonly string PlayHistoryJsonPath = Path.Combine(CacheDir, "play_history.json");

    private static readonly string[] ArtistSeparators =
        [", ", " & ", " feat. ", " feat ", " ft. ", " ft ", " / ", "; ", " vs. ", " vs "];

    private static readonly char[] GenreSeparators = ['/', ',', ';', '-', '&', '|', ' '];

    private readonly HttpClient _httpClient;
    private readonly string _serviceUrl;

    // In-memory cache for native C# engine (< 20 MB RAM, SIMD accelerated)
    private float[]? _embeddingsFlat; // TotalCount * _dimension (512 for CLAP, 384 for legacy)
    private float[]? _dspFlat;        // TotalCount * _dspDimension (71 for z-scored DSP)
    private float[]? _trackBpm;
    private float[]? _trackLoudnessDb;
    private float[]? _dspRaw;
    private int _dspRawCols;
    private string[]? _dspFeatureNames;
    private readonly Dictionary<Guid, AudioLabelEntry> _audioLabels = new();

    private sealed class AudioLabelEntry
    {
        public string? Key { get; set; }
        public string? Mode { get; set; }
        public string? Top_mood { get; set; }
        public string? Top_genre { get; set; }
        public bool Is_instrumental { get; set; }
    }
    private int _trackCount;
    private int _dimension = 512;
    private int _dspDimension;
    private readonly Dictionary<Guid, int> _guidToIndex = new();
    private readonly Dictionary<string, int> _artistCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ArtistKnowledge> _artistKnowledge = new(StringComparer.OrdinalIgnoreCase);

    // User profile from ListenBrainz export (transitions, loved items)
    private UserProfile? _userProfile;

    // Interactive session & persistent feedback
    // Note: Dislike / skip is contextual ("not a fit right now"), NOT a permanent global block!
    private readonly HashSet<Guid> _likedGuids = [];
    private readonly Dictionary<(string FromArtist, string ToArtist), int> _transitionMismatches = [];
    private readonly Dictionary<(string FromArtist, string ToArtist), float> _transitionAffinities = [];
    private readonly Dictionary<Guid, TrackPlayStats> _playStats = [];
    private float[] _sessionSteeringVector = new float[512];

    public class TrackPlayStats
    {
        public int PlayCount { get; set; }
        public int SkipCount { get; set; }
        public DateTime LastPlayedUtc { get; set; }
    }

    private volatile bool _internalEngineLoaded;
    private readonly Lock _loadSync = new();
    private readonly Lock _fileWriteSync = new();
    private CancellationTokenSource? _playHistorySaveCts;

    public class ArtistKnowledge
    {
        public List<string>? Tags { get; set; }
        public List<string>? Similar { get; set; }
        public string? Source { get; set; }
    }

    public class UserProfile
    {
        public List<string>? LovedMbids { get; set; }
        public List<string>? DislikedMbids { get; set; }
        public List<string>? LovedTitles { get; set; }
        public Dictionary<string, int>? LovedArtists { get; set; }
        public List<string>? DislikedTitles { get; set; }
        public Dictionary<string, int>? DislikedArtists { get; set; }
        public Dictionary<string, int>? TopArtists { get; set; }
        public Dictionary<string, Dictionary<string, int>>? ArtistTransitions { get; set; }
        public Dictionary<string, Dictionary<string, int>>? SessionCooccurrences { get; set; }
    }

    public class TransitionFeedbackEntry
    {
        public string FromArtist { get; set; } = string.Empty;
        public string ToArtist { get; set; } = string.Empty;
        public int Count { get; set; }
        public float Affinity { get; set; }
    }

    public class UserFeedbackData
    {
        public List<string> LikedGuids { get; set; } = [];
        public List<string> ContextualMismatches { get; set; } = [];
        public List<TransitionFeedbackEntry> TransitionMismatches { get; set; } = [];
        public List<TransitionFeedbackEntry> TransitionAffinities { get; set; } = [];
    }

    private readonly Lock _sync = new();
    public List<Guid> History { get; } = [];
    public HashSet<Guid> PlayedInSession { get; } = [];

    public SLibIndexRecommender(HttpClient? httpClient = null, string serviceUrl = "http://127.0.0.1:32543")
    {
        _httpClient = httpClient ?? new HttpClient();
        _serviceUrl = serviceUrl.TrimEnd('/');
    }

    public void RecordPlayed(Guid trackGuid)
    {
        if (trackGuid == Guid.Empty) return;
        lock (_sync)
        {
            if (History.Count == 0 || History[^1] != trackGuid)
            {
                History.Add(trackGuid);
                if (History.Count > 25)
                {
                    History.RemoveAt(0);
                }

                if (!_playStats.TryGetValue(trackGuid, out var stats))
                {
                    stats = new TrackPlayStats();
                    _playStats[trackGuid] = stats;
                }
                stats.PlayCount++;
                stats.LastPlayedUtc = DateTime.UtcNow;
            }
            PlayedInSession.Add(trackGuid);
        }

        SavePlayHistory();
    }

    /// <summary>
    /// Contextual negative feedback: Indicates a track was skipped or "not a fit" for the current session/transition.
    /// Does NOT permanently block or delete the track! It steers the session latent space away from this subspace
    /// and penalizes the specific transition link.
    /// </summary>
    public void RecordNegativeCue(Guid trackGuid, string? fromArtist = null, string? candidateArtist = null, string reason = "")
    {
        if (trackGuid == Guid.Empty) return;
        lock (_sync)
        {
            PlayedInSession.Add(trackGuid);

            if (_guidToIndex.TryGetValue(trackGuid, out var idx) && idx < _trackCount && _embeddingsFlat != null)
            {
                if (_sessionSteeringVector.Length == _dimension)
                {
                    var offset = idx * _dimension;
                    for (var d = 0; d < _dimension; d++)
                    {
                        _sessionSteeringVector[d] -= 0.40f * _embeddingsFlat[offset + d];
                    }
                    ClampVectorNorm(_sessionSteeringVector, 1.0f);
                }
                else
                {
                    Debug.WriteLine($"Skipping negative steering: steering dim {_sessionSteeringVector.Length} != embedding dim {_dimension}");
                }
            }

            if (!string.IsNullOrWhiteSpace(fromArtist) && !string.IsNullOrWhiteSpace(candidateArtist) &&
                !string.Equals(fromArtist, candidateArtist, StringComparison.OrdinalIgnoreCase))
            {
                var key = (fromArtist.Trim().ToLowerInvariant(), candidateArtist.Trim().ToLowerInvariant());
                _transitionMismatches[key] = _transitionMismatches.GetValueOrDefault(key) + 1;
            }

            if (!_playStats.TryGetValue(trackGuid, out var stats))
            {
                stats = new TrackPlayStats();
                _playStats[trackGuid] = stats;
            }
            stats.SkipCount++;
            stats.LastPlayedUtc = DateTime.UtcNow;
        }

        SaveFeedback();
        SavePlayHistory();
    }

    /// <summary>
    /// Contextual positive feedback: Indicates a track was liked or listened to completion.
    /// Gently steers the session latent space toward this style and reinforces the transition link.
    /// </summary>
    public void RecordPositiveCue(Guid trackGuid, string? fromArtist = null, string? candidateArtist = null, string reason = "")
    {
        if (trackGuid == Guid.Empty) return;
        lock (_sync)
        {
            _likedGuids.Add(trackGuid);

            if (_guidToIndex.TryGetValue(trackGuid, out var idx) && idx < _trackCount && _embeddingsFlat != null)
            {
                if (_sessionSteeringVector.Length == _dimension)
                {
                    var offset = idx * _dimension;
                    for (var d = 0; d < _dimension; d++)
                    {
                        _sessionSteeringVector[d] += 0.20f * _embeddingsFlat[offset + d];
                    }
                    ClampVectorNorm(_sessionSteeringVector, 1.0f);
                }
                else
                {
                    Debug.WriteLine($"Skipping positive steering: steering dim {_sessionSteeringVector.Length} != embedding dim {_dimension}");
                }
            }

            if (!string.IsNullOrWhiteSpace(fromArtist) && !string.IsNullOrWhiteSpace(candidateArtist) &&
                !string.Equals(fromArtist, candidateArtist, StringComparison.OrdinalIgnoreCase))
            {
                var key = (fromArtist.Trim().ToLowerInvariant(), candidateArtist.Trim().ToLowerInvariant());
                _transitionAffinities[key] = Math.Min(1.0f, _transitionAffinities.GetValueOrDefault(key) + 0.25f);
            }
        }

        SaveFeedback();
    }

    private void SaveFeedback()
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            UserFeedbackData data;
            lock (_sync)
            {
                data = new UserFeedbackData
                {
                    LikedGuids = _likedGuids.Select(g => g.ToString("D")).ToList(),
                    ContextualMismatches = _transitionMismatches.Where(kv => kv.Value > 0)
                        .Select(kv => $"{kv.Key.FromArtist} -> {kv.Key.ToArtist} ({kv.Value})").ToList(),
                    TransitionMismatches = _transitionMismatches.Where(kv => kv.Value > 0)
                        .Select(kv => new TransitionFeedbackEntry
                        {
                            FromArtist = kv.Key.FromArtist,
                            ToArtist = kv.Key.ToArtist,
                            Count = kv.Value
                        }).ToList(),
                    TransitionAffinities = _transitionAffinities.Where(kv => kv.Value > 0)
                        .Select(kv => new TransitionFeedbackEntry
                        {
                            FromArtist = kv.Key.FromArtist,
                            ToArtist = kv.Key.ToArtist,
                            Affinity = kv.Value
                        }).ToList()
                };
            }

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            WriteFileAtomic(UserFeedbackJsonPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save user feedback: {ex.Message}");
        }
    }

    private void SavePlayHistory()
    {
        CancellationToken token;
        lock (_sync)
        {
            _playHistorySaveCts?.Cancel();
            _playHistorySaveCts?.Dispose();
            _playHistorySaveCts = new CancellationTokenSource();
            token = _playHistorySaveCts.Token;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(2000, token);
                Dictionary<string, TrackPlayStats> snapshot;
                lock (_sync)
                {
                    snapshot = _playStats.ToDictionary(kv => kv.Key.ToString("D"), kv => kv.Value);
                }
                var json = JsonSerializer.Serialize(snapshot);
                WriteFileAtomic(PlayHistoryJsonPath, json);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save play history: {ex.Message}");
            }
        }, token);
    }

    private void WriteFileAtomic(string path, string content)
    {
        lock (_fileWriteSync)
        {
            Directory.CreateDirectory(CacheDir);
            var tmpPath = $"{path}.{Environment.ProcessId}.tmp";
            File.WriteAllText(tmpPath, content);
            File.Move(tmpPath, path, overwrite: true);
        }
    }

    public Task<RecommendationResult?> PickNextAsync(
        IReadOnlyList<SLibIndexLiteTrack> libraryTracks,
        SLibIndexLiteTrack? currentTrack = null,
        double temperature = 0.12,
        bool allowSameAlbum = false)
    {
        if (currentTrack != null)
        {
            RecordPlayed(currentTrack.Guid);
        }

        List<Guid> historySnapshot;
        HashSet<Guid> playedSnapshot;
        lock (_sync)
        {
            historySnapshot = [.. History];
            playedSnapshot = [.. PlayedInSession];
        }

        return Task.Run(() =>
            PickNextInternal(libraryTracks, historySnapshot, playedSnapshot, temperature, allowSameAlbum));
    }

    private void EnsureInternalEngineLoaded(IReadOnlyList<SLibIndexLiteTrack> libraryTracks)
    {
        if (_internalEngineLoaded) return;
        lock (_loadSync)
        {
            if (_internalEngineLoaded) return;

            var npzPath = EmbeddingsNpzCandidates.FirstOrDefault(File.Exists);
            if (npzPath != null)
            {
                try
                {
                    LoadNpzArchive(npzPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load npz embeddings from '{npzPath}': {ex.Message}");
                }
            }
            else if (File.Exists(EmbeddingsBinPath) && File.Exists(MetadataJsonPath))
            {
                try
                {
                    _dimension = 384;
                    _sessionSteeringVector = new float[_dimension];
                    var bytes = File.ReadAllBytes(EmbeddingsBinPath);
                    _trackCount = bytes.Length / (_dimension * sizeof(float));
                    _embeddingsFlat = new float[_trackCount * _dimension];
                    Buffer.BlockCopy(bytes, 0, _embeddingsFlat, 0, bytes.Length);

                    var json = File.ReadAllText(MetadataJsonPath);
                    using var doc = JsonDocument.Parse(json);
                    var idx = 0;
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        if (elem.TryGetProperty("guid", out var gProp) &&
                            Guid.TryParse(gProp.GetString(), out var g))
                        {
                            _guidToIndex[g] = idx;
                        }
                        idx++;
                        if (idx >= _trackCount) break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load internal embeddings: {ex.Message}");
                }
            }

            _artistCounts.Clear();
            foreach (var t in libraryTracks)
            {
                var a = t.Artist?.Trim() ?? "";
                if (!string.IsNullOrEmpty(a))
                {
                    _artistCounts[a] = _artistCounts.TryGetValue(a, out var c) ? c + 1 : 1;
                }
            }

            if (File.Exists(ArtistKnowledgeJsonPath))
            {
                try
                {
                    var kbJson = File.ReadAllText(ArtistKnowledgeJsonPath);
                    var kb = JsonSerializer.Deserialize<Dictionary<string, ArtistKnowledge>>(kbJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (kb != null)
                    {
                        _artistKnowledge.Clear();
                        foreach (var kvp in kb)
                        {
                            _artistKnowledge[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load artist knowledge: {ex.Message}");
                }
            }

            if (File.Exists(UserProfileJsonPath))
            {
                try
                {
                    var profileJson = File.ReadAllText(UserProfileJsonPath);
                    _userProfile = JsonSerializer.Deserialize<UserProfile>(profileJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load user profile: {ex.Message}");
                }
            }

            if (File.Exists(UserFeedbackJsonPath))
            {
                try
                {
                    var fbJson = File.ReadAllText(UserFeedbackJsonPath);
                    var fb = JsonSerializer.Deserialize<UserFeedbackData>(fbJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (fb != null)
                    {
                        lock (_sync)
                        {
                            foreach (var gStr in fb.LikedGuids)
                                if (Guid.TryParse(gStr, out var g)) _likedGuids.Add(g);

                            foreach (var m in fb.TransitionMismatches)
                            {
                                if (!string.IsNullOrWhiteSpace(m.FromArtist) && !string.IsNullOrWhiteSpace(m.ToArtist) && m.Count > 0)
                                {
                                    var key = (m.FromArtist.Trim().ToLowerInvariant(), m.ToArtist.Trim().ToLowerInvariant());
                                    _transitionMismatches[key] = m.Count;
                                }
                            }

                            foreach (var a in fb.TransitionAffinities)
                            {
                                if (!string.IsNullOrWhiteSpace(a.FromArtist) && !string.IsNullOrWhiteSpace(a.ToArtist) && a.Affinity > 0)
                                {
                                    var key = (a.FromArtist.Trim().ToLowerInvariant(), a.ToArtist.Trim().ToLowerInvariant());
                                    _transitionAffinities[key] = Math.Min(1.0f, a.Affinity);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load user feedback: {ex.Message}");
                }
            }

            if (File.Exists(PlayHistoryJsonPath))
            {
                try
                {
                    var histJson = File.ReadAllText(PlayHistoryJsonPath);
                    var hist = JsonSerializer.Deserialize<Dictionary<string, TrackPlayStats>>(histJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (hist != null)
                    {
                        lock (_sync)
                        {
                            foreach (var kv in hist)
                            {
                                if (Guid.TryParse(kv.Key, out var g))
                                    _playStats[g] = kv.Value;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load play history: {ex.Message}");
                }
            }

            _internalEngineLoaded = true;
        }
    }

    private List<string> GetArtistTags(string artist)
    {
        if (string.IsNullOrWhiteSpace(artist)) return [];
        if (_artistKnowledge.TryGetValue(artist, out var info) && info.Tags != null)
        {
            return info.Tags;
        }
        return [];
    }

    private HashSet<string> GetSimilarArtists(string artist)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(artist)) return set;
        if (_artistKnowledge.TryGetValue(artist, out var info) && info.Similar != null)
        {
            foreach (var s in info.Similar)
            {
                if (!string.IsNullOrWhiteSpace(s)) set.Add(s.Trim());
            }
        }
        return set;
    }

    private static List<string> SplitArtists(string? artist)
    {
        if (string.IsNullOrWhiteSpace(artist)) return [];
        var parts = artist.Split(ArtistSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 ? parts.ToList() : [artist.Trim()];
    }

    private static float ComputeTagJaccard(List<string> tags1, List<string> tags2)
    {
        if (tags1.Count == 0 || tags2.Count == 0) return 0f;
        var set1 = new HashSet<string>(tags1, StringComparer.OrdinalIgnoreCase);
        var set2 = new HashSet<string>(tags2, StringComparer.OrdinalIgnoreCase);
        var intersect = set1.Count(t => set2.Contains(t));
        var union = set1.Count + set2.Count - intersect;
        return union > 0 ? (float)intersect / union : 0f;
    }

    private static float ComputeGenreSimilarity(string? genre1, string? genre2)
    {
        var g1 = genre1?.Trim() ?? "";
        var g2 = genre2?.Trim() ?? "";
        if (string.IsNullOrEmpty(g1) || string.IsNullOrEmpty(g2)) return 0f;
        if (string.Equals(g1, g2, StringComparison.OrdinalIgnoreCase)) return 1.0f;

        var t1 = g1.Split(GenreSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.ToLowerInvariant()).ToHashSet();
        var t2 = g2.Split(GenreSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.ToLowerInvariant()).ToHashSet();
        if (t1.Count == 0 || t2.Count == 0) return 0f;

        var intersect = t1.Count(t => t2.Contains(t));
        var union = t1.Count + t2.Count - intersect;
        return union > 0 ? (float)intersect / union : 0f;
    }

    private static bool? ClassifySoundtrack(List<string> tags, string? genre)
    {
        var g = genre?.Trim().ToLowerInvariant() ?? "";
        if (!string.IsNullOrEmpty(g) && g is not ("unknown" or "other" or "general"))
        {
            if (g.Contains("soundtrack") || g.Contains("vgm") || g.Contains("score") ||
                g.Contains("game") || g.Contains("ost") || g.Contains("anime") || g.Contains("stage & screen"))
            {
                return true;
            }
            return false;
        }

        if (tags.Count > 0)
        {
            foreach (var t in tags)
            {
                var tl = t.ToLowerInvariant();
                if (tl is "soundtrack" or "video game music" or "vgm" or "score" or "ost" or "chiptune" or "anime")
                    return true;
            }
            return false;
        }

        return null;
    }

    private static bool IsAcceptableDuration(
        SLibIndexLiteTrack track,
        double minSec = MinTrackDurationSec,
        double maxSec = MaxTrackDurationSec)
    {
        // Allow unknown/missing duration (null or <= 0), while strictly rejecting known short or overly long tracks
        if (track.Duration is not > 0) return true;
        var durSec = track.Duration.Value / 1000.0;
        return durSec >= minSec && durSec <= maxSec;
    }

    private static bool IsNotShortTrack(SLibIndexLiteTrack track)
    {
        return track.Duration is not > 0 || track.Duration.Value / 1000.0 >= MinTrackDurationSec;
    }

    private float CalculateMetadataQuality(SLibIndexLiteTrack track)
    {
        var title = track.Title?.Trim() ?? "";
        var artist = track.Artist?.Trim() ?? "";
        var album = track.Album?.Trim() ?? "";
        var genre = track.Genre?.Trim() ?? "";

        if (string.IsNullOrEmpty(artist) || artist is "????" or "Unknown" or "Various Artists" or "Various")
            return 0.1f;
        if (string.Equals(artist, title, StringComparison.OrdinalIgnoreCase))
            return 0.1f;
        if (album.StartsWith("CD", StringComparison.OrdinalIgnoreCase) && album.Length <= 4)
            return 0.15f;

        var score = 0.35f;
        if (!string.IsNullOrEmpty(album) && !album.Equals("Unknown", StringComparison.OrdinalIgnoreCase) &&
            !album.Equals("Various", StringComparison.OrdinalIgnoreCase) && !album.Equals("Untitled", StringComparison.OrdinalIgnoreCase))
        {
            score += 0.25f;
        }

        var tags = GetArtistTags(artist);
        if (!string.IsNullOrEmpty(genre) && !genre.Equals("Unknown", StringComparison.OrdinalIgnoreCase) &&
            !genre.Equals("Other", StringComparison.OrdinalIgnoreCase) && !genre.Equals("General", StringComparison.OrdinalIgnoreCase))
        {
            score += 0.20f;
        }
        else if (tags.Count > 0)
        {
            score += 0.20f;
        }

        if (track.Year is > 1900)
        {
            score += 0.10f;
        }

        return Math.Min(1.0f, score);
    }

    private bool IsMetadataAcceptable(SLibIndexLiteTrack track) => CalculateMetadataQuality(track) >= 0.35f;

    private RecommendationResult? PickNextInternal(
        IReadOnlyList<SLibIndexLiteTrack> libraryTracks,
        List<Guid> historySnapshot,
        HashSet<Guid> playedSnapshot,
        double temperature,
        bool allowSameAlbum)
    {
        EnsureInternalEngineLoaded(libraryTracks);
        if (libraryTracks.Count == 0) return null;

        var trackByGuid = new Dictionary<Guid, SLibIndexLiteTrack>(libraryTracks.Count);
        foreach (var t in libraryTracks)
        {
            if (t.Guid != Guid.Empty)
                trackByGuid.TryAdd(t.Guid, t);
        }

        if (_embeddingsFlat == null || _guidToIndex.Count == 0)
        {
            return PickNextByMetadataFeatures(libraryTracks, trackByGuid, historySnapshot, playedSnapshot, temperature, allowSameAlbum);
        }

        var validHistory = new List<int>();
        foreach (var guid in historySnapshot)
        {
            if (_guidToIndex.TryGetValue(guid, out var idx) && idx < _trackCount)
            {
                validHistory.Add(idx);
            }
        }

        if (validHistory.Count == 0)
        {
            return PickFromPool(libraryTracks, playedSnapshot, "Library start");
        }

        // Context vectors with exponential decay (0.70) + session latent steering
        var context = new float[_dimension];
        var dspContext = _dspFlat != null && _dspDimension > 0 ? new float[_dspDimension] : null;
        var totalWeight = 0.0f;
        const float decay = 0.70f;

        for (var i = 0; i < validHistory.Count && i < 5; i++)
        {
            var hIdx = validHistory[validHistory.Count - 1 - i];
            var weight = MathF.Pow(decay, i);
            var offset = hIdx * _dimension;

            for (var d = 0; d < _dimension; d++)
            {
                context[d] += weight * _embeddingsFlat[offset + d];
            }

            if (dspContext != null && _dspFlat != null)
            {
                var dspOffset = hIdx * _dspDimension;
                for (var d = 0; d < _dspDimension; d++)
                {
                    dspContext[d] += weight * _dspFlat[dspOffset + d];
                }
            }

            totalWeight += weight;
        }

        float[]? steeringSnapshot = null;
        lock (_sync)
        {
            if (_sessionSteeringVector.Length == _dimension)
            {
                steeringSnapshot = (float[])_sessionSteeringVector.Clone();
                // Gently decay steering per pick so repeated cues don't permanently override history
                for (var d = 0; d < _dimension; d++)
                    _sessionSteeringVector[d] *= 0.85f;
            }
            else
            {
                Debug.WriteLine($"Skipping context steering: steering dim {_sessionSteeringVector.Length} != embedding dim {_dimension}");
            }
        }

        for (var d = 0; d < _dimension; d++)
        {
            context[d] /= Math.Max(totalWeight, 1e-6f);
            if (steeringSnapshot != null)
                context[d] += steeringSnapshot[d] * 0.35f;
        }
        NormalizeVector(context);

        if (dspContext != null)
        {
            for (var d = 0; d < _dspDimension; d++)
            {
                dspContext[d] /= Math.Max(totalWeight, 1e-6f);
            }
            NormalizeVector(dspContext);
        }

        var lastValidIdx = validHistory[^1];
        var lastBpm = _trackBpm != null && lastValidIdx < _trackBpm.Length ? _trackBpm[lastValidIdx] : 0f;
        var lastLoudness = _trackLoudnessDb != null && lastValidIdx < _trackLoudnessDb.Length ? _trackLoudnessDb[lastValidIdx] : 0f;

        var ctx = BuildScoringContext(trackByGuid, historySnapshot);
        var candidates = new List<(SLibIndexLiteTrack track, float score, float rawSim, string reason)>();

        foreach (var track in libraryTracks)
        {
            if (!IsEligibleCandidate(track, playedSnapshot, ctx)) continue;
            if (!_guidToIndex.TryGetValue(track.Guid, out var idx) || idx >= _trackCount) continue;

            var quality = CalculateMetadataQuality(track);
            if (quality < 0.35f) continue;

            var clapSim = DotProduct(context, _embeddingsFlat, idx * _dimension, _dimension);
            var dspSim = dspContext != null && _dspFlat != null
                ? DotProduct(dspContext, _dspFlat, idx * _dspDimension, _dspDimension)
                : 0f;
            var sim = dspContext != null ? 0.72f * clapSim + 0.28f * dspSim : clapSim;

            // Apply quality scaling only to non-negative similarity so negative sim is never rewarded
            var baseScore = sim >= 0f
                ? sim * (0.6f + 0.4f * quality)
                : sim + (quality - 0.5f) * 0.15f;

            // BPM & Loudness continuity (allowing half-time / double-time matches)
            var tBpm = _trackBpm != null && idx < _trackBpm.Length ? _trackBpm[idx] : 0f;
            if (lastBpm > 20f && tBpm > 20f)
            {
                var bpmDiff = MathF.Min(MathF.Abs(lastBpm - tBpm),
                    MathF.Min(MathF.Abs(lastBpm - 2f * tBpm), MathF.Abs(2f * lastBpm - tBpm)));
                if (bpmDiff <= 8f) baseScore += 0.08f;
                else if (bpmDiff <= 16f) baseScore += 0.03f;
                else if (bpmDiff > 35f) baseScore -= 0.08f;
            }

            if (_trackLoudnessDb != null && idx < _trackLoudnessDb.Length && lastLoudness < 0f && _trackLoudnessDb[idx] < 0f)
            {
                var loudDiff = MathF.Abs(lastLoudness - _trackLoudnessDb[idx]);
                if (loudDiff > 8f) baseScore -= MathF.Min(0.10f, (loudDiff - 8f) * 0.015f);
            }

            var (metaScore, reason) = ScoreCandidateMetadata(track, quality, ctx, allowSameAlbum, includeGenreYearSignals: false);
            var totalScore = baseScore + metaScore;
            if (tBpm > 20f)
            {
                reason = string.IsNullOrEmpty(reason) ? $"{tBpm:F0} BPM" : $"{tBpm:F0} BPM, {reason}";
            }
            candidates.Add((track, totalScore, sim, reason));
        }

        if (candidates.Count == 0)
        {
            return PickFallbackTrack(libraryTracks, playedSnapshot, "Library fallback");
        }

        var topDiverse = SelectDiverseTopK(candidates, k: 5);
        var chosen = SampleSoftmax(topDiverse, temperature);

        var reasonPrefix = string.IsNullOrEmpty(chosen.reason) ? "" : $"{chosen.reason}, ";
        return new RecommendationResult
        {
            Track = chosen.track,
            Score = chosen.rawSim,
            Reason = $"{reasonPrefix}{(int)(Math.Clamp(chosen.rawSim, 0f, 1f) * 100)}% match"
        };
    }

    private RecommendationResult? PickNextByMetadataFeatures(
        IReadOnlyList<SLibIndexLiteTrack> libraryTracks,
        Dictionary<Guid, SLibIndexLiteTrack> trackByGuid,
        List<Guid> historySnapshot,
        HashSet<Guid> playedSnapshot,
        double temperature,
        bool allowSameAlbum)
    {
        if (historySnapshot.Count == 0)
        {
            return PickLibraryStart(libraryTracks, playedSnapshot, "Metadata library start");
        }

        var ctx = BuildScoringContext(trackByGuid, historySnapshot);
        if (ctx.HistoryTracks.Count == 0)
        {
            return PickLibraryStart(libraryTracks, playedSnapshot, "Metadata library start");
        }

        var candidates = new List<(SLibIndexLiteTrack track, float score, float rawSim, string reason)>();

        foreach (var track in libraryTracks)
        {
            if (!IsEligibleCandidate(track, playedSnapshot, ctx)) continue;

            var quality = CalculateMetadataQuality(track);
            if (quality < 0.35f) continue;

            var (score, reason) = ScoreCandidateMetadata(track, quality, ctx, allowSameAlbum, includeGenreYearSignals: true);
            // Normalize an approximate [0, 1] confidence for display
            var displaySim = Math.Clamp(0.45f + score * 0.45f, 0.05f, 0.99f);
            candidates.Add((track, score, displaySim, reason));
        }

        if (candidates.Count == 0)
        {
            return PickFallbackTrack(libraryTracks, playedSnapshot, "Metadata fallback");
        }

        var topDiverse = SelectDiverseTopK(candidates, k: 5);
        var chosen = SampleSoftmax(topDiverse, Math.Max(temperature, 0.14));

        return new RecommendationResult
        {
            Track = chosen.track,
            Score = chosen.rawSim,
            Reason = string.IsNullOrEmpty(chosen.reason) ? "Metadata smart match" : chosen.reason
        };
    }

    private sealed class ScoringContext
    {
        public List<SLibIndexLiteTrack> HistoryTracks { get; init; } = [];
        public SLibIndexLiteTrack? LastTrack { get; init; }
        public string LastArtist { get; init; } = "";
        public string LastAlbum { get; init; } = "";
        public int LastTrackNum { get; init; }
        public List<string> LastTags { get; init; } = [];
        public bool? LastIsOst { get; init; }
        public HashSet<string> SimilarArtists { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> LastArtistTokens { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<(string Title, string Artist)> PlayedTitleArtistPairs { get; init; } = [];
        public HashSet<string> PlayedTitles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> RecentArtistCounts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public int ConsecutiveSameArtist { get; init; }
    }

    private ScoringContext BuildScoringContext(
        Dictionary<Guid, SLibIndexLiteTrack> trackByGuid,
        List<Guid> historySnapshot)
    {
        var historyTracks = new List<SLibIndexLiteTrack>();
        var playedTitleArtist = new HashSet<(string Title, string Artist)>();
        var playedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var recentArtistCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < historySnapshot.Count; i++)
        {
            if (!trackByGuid.TryGetValue(historySnapshot[i], out var t)) continue;
            historyTracks.Add(t);

            var title = t.Title?.Trim() ?? "";
            var artist = t.Artist?.Trim() ?? "";
            if (!string.IsNullOrEmpty(title))
            {
                playedTitles.Add(title);
                playedTitleArtist.Add((title.ToLowerInvariant(), artist.ToLowerInvariant()));
            }

            // Count artist frequency over the most recent 12 tracks
            if (i >= historySnapshot.Count - 12 && !string.IsNullOrEmpty(artist))
            {
                recentArtistCounts[artist] = recentArtistCounts.GetValueOrDefault(artist) + 1;
            }
        }

        var lastTrack = historyTracks.Count > 0 ? historyTracks[^1] : null;
        var lastArtist = lastTrack?.Artist?.Trim() ?? "";
        var lastTags = GetArtistTags(lastArtist);

        var consecutiveSameArtist = 0;
        for (var i = historyTracks.Count - 1; i >= 0; i--)
        {
            if (string.Equals(historyTracks[i].Artist?.Trim(), lastArtist, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(lastArtist))
            {
                consecutiveSameArtist++;
            }
            else
            {
                break;
            }
        }

        return new ScoringContext
        {
            HistoryTracks = historyTracks,
            LastTrack = lastTrack,
            LastArtist = lastArtist,
            LastAlbum = lastTrack?.Album?.Trim() ?? "",
            LastTrackNum = lastTrack?.TrackNumber ?? 0,
            LastTags = lastTags,
            LastIsOst = ClassifySoundtrack(lastTags, lastTrack?.Genre),
            SimilarArtists = GetSimilarArtists(lastArtist),
            LastArtistTokens = SplitArtists(lastArtist).ToHashSet(StringComparer.OrdinalIgnoreCase),
            PlayedTitleArtistPairs = playedTitleArtist,
            PlayedTitles = playedTitles,
            RecentArtistCounts = recentArtistCounts,
            ConsecutiveSameArtist = consecutiveSameArtist
        };
    }

    private static bool IsEligibleCandidate(
        SLibIndexLiteTrack track,
        HashSet<Guid> playedSnapshot,
        ScoringContext ctx)
    {
        if (playedSnapshot.Contains(track.Guid)) return false;

        // Strictly ignore short tracks (< 60s) and excessively long tracks (> 600s)
        if (!IsAcceptableDuration(track, MinTrackDurationSec, MaxTrackDurationSec)) return false;

        // Block exact (title, artist) repeats to avoid alternate versions/remasters of the exact same song
        var title = track.Title?.Trim() ?? "";
        var artist = track.Artist?.Trim() ?? "";
        if (!string.IsNullOrEmpty(title) &&
            ctx.PlayedTitleArtistPairs.Contains((title.ToLowerInvariant(), artist.ToLowerInvariant())))
        {
            return false;
        }

        return true;
    }

    private (float score, string reason) ScoreCandidateMetadata(
        SLibIndexLiteTrack track,
        float quality,
        ScoringContext ctx,
        bool allowSameAlbum,
        bool includeGenreYearSignals)
    {
        var score = 0.0f;
        var reasons = new List<string>();

        var durSec = (track.Duration ?? 0) / 1000.0;
        if (durSec is > 0 and < 90.0)
            score -= (float)(90.0 - durSec) * 0.010f;
        if (durSec is > 420.0)
            score -= (float)(durSec - 420.0) * 0.002f;

        // Quality bonus (bounded, additive)
        score += (quality - 0.5f) * 0.20f;

        var tTitle = track.Title?.Trim() ?? "";
        var tArtist = track.Artist?.Trim() ?? "";
        var tAlbum = track.Album?.Trim() ?? "";
        var tTags = GetArtistTags(tArtist);
        var tIsOst = ClassifySoundtrack(tTags, track.Genre);

        // Mild penalty if same title by a different artist (avoids hearing "Battle" back-to-back, without hard-blocking)
        if (!string.IsNullOrEmpty(tTitle) && ctx.PlayedTitles.Contains(tTitle))
        {
            score -= 0.25f;
        }

        // 1. Soundtrack / VGM separation (only when both tracks have a known classification)
        if (ctx.LastIsOst.HasValue && tIsOst.HasValue && ctx.LastIsOst.Value != tIsOst.Value)
        {
            score -= 0.60f;
        }

        // 2. Dynamic Tag Jaccard
        if (ctx.LastTags.Count > 0 && tTags.Count > 0 && ctx.LastIsOst == tIsOst)
        {
            var jaccard = ComputeTagJaccard(ctx.LastTags, tTags);
            if (jaccard > 0)
            {
                score += jaccard * 0.45f;
                var shared = ctx.LastTags.Intersect(tTags, StringComparer.OrdinalIgnoreCase).Take(2).ToList();
                if (shared.Count > 0) reasons.Add($"tags: {string.Join(", ", shared)}");
            }
        }

        // 3. Multi-track context: Genre, Year era, and Vocal/Instrumental continuity
        if (includeGenreYearSignals && ctx.HistoryTracks.Count > 0)
        {
            var genreScore = 0.0f;
            var yearScore = 0.0f;
            var vocalScore = 0.0f;
            var totalDecayWeight = 0.0f;
            const float decay = 0.70f;

            var maxLookback = Math.Min(ctx.HistoryTracks.Count, 5);
            for (var i = 0; i < maxLookback; i++)
            {
                var hTrack = ctx.HistoryTracks[ctx.HistoryTracks.Count - 1 - i];
                var w = MathF.Pow(decay, i);
                totalDecayWeight += w;

                var gSim = ComputeGenreSimilarity(hTrack.Genre, track.Genre);
                genreScore += w * gSim;

                if (hTrack.Year is > 1900 && track.Year is > 1900)
                {
                    var diff = Math.Abs(hTrack.Year.Value - track.Year.Value);
                    var era = diff switch
                    {
                        0 => 0.14f,
                        <= 3 => 0.10f,
                        <= 7 => 0.05f,
                        <= 12 => 0.0f,
                        _ => -0.08f
                    };
                    yearScore += w * era;
                }

                var hHasLyrics = hTrack.HasLyrics || hTrack.HasSyncedLyrics;
                var tHasLyrics = track.HasLyrics || track.HasSyncedLyrics;
                vocalScore += w * (hHasLyrics == tHasLyrics ? 0.05f : -0.02f);
            }

            if (totalDecayWeight > 0)
            {
                var avgGenre = genreScore / totalDecayWeight;
                var avgYear = yearScore / totalDecayWeight;
                var avgVocal = vocalScore / totalDecayWeight;

                score += avgGenre * 0.42f + avgYear + avgVocal;

                if (avgGenre >= 0.5f && !string.IsNullOrWhiteSpace(track.Genre))
                    reasons.Add($"{track.Genre} genre");
                if (avgYear >= 0.08f && track.Year is > 1900)
                    reasons.Add($"{track.Year} era");
            }
        }
        else if (!string.IsNullOrEmpty(track.Genre) && !string.IsNullOrEmpty(ctx.LastTrack?.Genre))
        {
            var gSim = ComputeGenreSimilarity(ctx.LastTrack.Genre, track.Genre);
            score += gSim * 0.15f;
            if (reasons.Count == 0 && gSim >= 0.5f)
                reasons.Add($"{track.Genre} genre");
        }

        // 4. Collaborative filtering dataset similar artist affinity
        var tArtistTokens = SplitArtists(tArtist);
        var isSimilarArtist = !string.IsNullOrEmpty(tArtist) &&
                              (ctx.SimilarArtists.Contains(tArtist) ||
                               tArtistTokens.Any(tok => ctx.SimilarArtists.Contains(tok)));
        if (isSimilarArtist)
        {
            score += 0.35f;
            reasons.Add("similar artist");
        }
        else if (ctx.LastArtistTokens.Count > 0 && tArtistTokens.Count > 0 &&
                 !string.Equals(tArtist, ctx.LastArtist, StringComparison.OrdinalIgnoreCase) &&
                 tArtistTokens.Any(tok => ctx.LastArtistTokens.Contains(tok)))
        {
            // Shared collaborator / featuring artist
            score += 0.20f;
            reasons.Add("shared artist");
        }

        // 5. User's personal ListenBrainz transitions & co-occurrences
        if (_userProfile?.ArtistTransitions != null &&
            _userProfile.ArtistTransitions.TryGetValue(ctx.LastArtist.ToLowerInvariant(), out var transMap) &&
            transMap.ContainsKey(tArtist.ToLowerInvariant()))
        {
            score += 0.35f;
            reasons.Add("user history transition");
        }
        else if (_userProfile?.SessionCooccurrences != null &&
                 _userProfile.SessionCooccurrences.TryGetValue(ctx.LastArtist.ToLowerInvariant(), out var coMap) &&
                 coMap.ContainsKey(tArtist.ToLowerInvariant()))
        {
            score += 0.20f;
        }

        // 6. Contextual transition fit (learned from "Not a fit" / positive feedback)
        if (!string.IsNullOrEmpty(ctx.LastArtist) && !string.IsNullOrEmpty(tArtist))
        {
            var transKey = (ctx.LastArtist.ToLowerInvariant(), tArtist.ToLowerInvariant());
            if (_transitionMismatches.TryGetValue(transKey, out var mismatchCount))
            {
                score -= Math.Min(0.60f, mismatchCount * 0.25f);
            }
            if (_transitionAffinities.TryGetValue(transKey, out var transAffinity))
            {
                score += transAffinity;
            }
        }

        // 7. Liked tracks, cross-session recency/skips & user's loved artist / track bonus
        bool isLiked;
        TrackPlayStats? playStat;
        lock (_sync)
        {
            isLiked = _likedGuids.Contains(track.Guid);
            _playStats.TryGetValue(track.Guid, out playStat);
        }
        if (isLiked)
        {
            score += 0.25f;
            reasons.Add("liked track");
        }

        if (playStat != null)
        {
            var hoursSince = (DateTime.UtcNow - playStat.LastPlayedUtc).TotalHours;
            if (hoursSince is >= 0 and < 2.0)
                score -= 0.45f;
            else if (hoursSince is >= 2.0 and < 24.0)
                score -= 0.20f;

            if (playStat.SkipCount > playStat.PlayCount)
                score -= Math.Min(0.45f, (playStat.SkipCount - playStat.PlayCount) * 0.15f);
        }
        else
        {
            // Gentle discovery bonus for unplayed tracks
            score += 0.04f;
        }

        if (_userProfile != null)
        {
            var artistLower = tArtist.ToLowerInvariant();
            if (_userProfile.LovedArtists != null && _userProfile.LovedArtists.ContainsKey(artistLower))
            {
                score += 0.20f;
            }
            if (!string.IsNullOrEmpty(tTitle) && _userProfile.LovedTitles != null && _userProfile.LovedTitles.Contains(tTitle.ToLowerInvariant()))
            {
                score += 0.25f;
            }
            if (_userProfile.DislikedArtists != null && _userProfile.DislikedArtists.ContainsKey(artistLower))
            {
                score -= 0.12f;
            }
            if (!string.IsNullOrEmpty(tTitle) && _userProfile.DislikedTitles != null && _userProfile.DislikedTitles.Contains(tTitle.ToLowerInvariant()))
            {
                score -= 0.30f;
            }
            if (_userProfile.TopArtists != null && _userProfile.TopArtists.TryGetValue(artistLower, out var artistPlays) && artistPlays > 0)
            {
                score += MathF.Min(0.10f, MathF.Log10(1f + artistPlays) * 0.04f);
            }
        }

        // 8. Artist pacing & session fatigue (avoid clustering on a single 200-track composer)
        if (!string.IsNullOrEmpty(tArtist))
        {
            if (string.Equals(tArtist, ctx.LastArtist, StringComparison.OrdinalIgnoreCase))
            {
                score -= ctx.ConsecutiveSameArtist >= 2 ? 0.50f : 0.12f;
            }

            if (ctx.RecentArtistCounts.TryGetValue(tArtist, out var recentCount) && recentCount > 0)
            {
                score -= Math.Min(0.35f, recentCount * 0.08f);
            }
        }

        // 9. Album monotony breaker
        if (!allowSameAlbum && !string.IsNullOrEmpty(tAlbum) &&
            string.Equals(tAlbum, ctx.LastAlbum, StringComparison.OrdinalIgnoreCase))
        {
            score -= 0.35f;
            if (track.TrackNumber == ctx.LastTrackNum + 1)
            {
                score -= 0.30f;
            }
        }

        if (!string.IsNullOrEmpty(tArtist) && !string.Equals(tArtist, ctx.LastArtist, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add($"transition to {tArtist}");
        }
        else if (!string.IsNullOrEmpty(tArtist))
        {
            reasons.Add($"by {tArtist}");
        }

        return (score, string.Join(", ", reasons.Distinct()));
    }

    private static List<(SLibIndexLiteTrack track, float score, float rawSim, string reason)> SelectDiverseTopK(
        List<(SLibIndexLiteTrack track, float score, float rawSim, string reason)> candidates,
        int k)
    {
        var sorted = candidates.OrderByDescending(c => c.score).ToList();
        var selected = new List<(SLibIndexLiteTrack track, float score, float rawSim, string reason)>(k);
        var artistUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var albumUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // First pass: max 1 track per artist and per album
        foreach (var c in sorted)
        {
            var artist = c.track.Artist?.Trim() ?? "";
            var album = c.track.Album?.Trim() ?? "";

            if (!string.IsNullOrEmpty(artist) && artistUsed.Contains(artist)) continue;
            if (!string.IsNullOrEmpty(album) && albumUsed.Contains(album)) continue;

            selected.Add(c);
            if (!string.IsNullOrEmpty(artist)) artistUsed.Add(artist);
            if (!string.IsNullOrEmpty(album)) albumUsed.Add(album);
            if (selected.Count >= k) return selected;
        }

        // Second pass: fill remaining slots if library has fewer than k unique artists/albums
        foreach (var c in sorted)
        {
            if (selected.Contains(c)) continue;
            selected.Add(c);
            if (selected.Count >= k) break;
        }

        return selected;
    }

    private static (SLibIndexLiteTrack track, float score, float rawSim, string reason) SampleSoftmax(
        List<(SLibIndexLiteTrack track, float score, float rawSim, string reason)> topK,
        double temperature)
    {
        var maxScore = topK[0].score;
        var temp = (float)Math.Max(temperature, 0.08);
        var exps = topK.Select(c => MathF.Exp((c.score - maxScore) / temp)).ToArray();
        var sumExp = exps.Sum();
        var roll = Random.Shared.NextSingle() * sumExp;
        var running = 0.0f;

        for (var i = 0; i < topK.Count; i++)
        {
            running += exps[i];
            if (roll <= running)
            {
                return topK[i];
            }
        }

        return topK[0];
    }

    private RecommendationResult? PickLibraryStart(
        IReadOnlyList<SLibIndexLiteTrack> libraryTracks,
        HashSet<Guid> playedSnapshot,
        string reason) => PickFromPool(libraryTracks, playedSnapshot, reason);

    private RecommendationResult? PickFallbackTrack(
        IReadOnlyList<SLibIndexLiteTrack> libraryTracks,
        HashSet<Guid> playedSnapshot,
        string reason) => PickFromPool(libraryTracks, playedSnapshot, reason);

    private RecommendationResult? PickFromPool(
        IReadOnlyList<SLibIndexLiteTrack> libraryTracks,
        HashSet<Guid> playedSnapshot,
        string reason)
    {
        var pool = libraryTracks
            .Where(t => !playedSnapshot.Contains(t.Guid) && IsMetadataAcceptable(t) &&
                        IsAcceptableDuration(t, FallbackMinDurationSec, FallbackMaxDurationSec))
            .ToList();

        if (pool.Count == 0)
        {
            pool = libraryTracks
                .Where(t => !playedSnapshot.Contains(t.Guid) && IsAcceptableDuration(t))
                .ToList();
        }

        if (pool.Count == 0)
        {
            pool = libraryTracks.Where(t => IsAcceptableDuration(t)).ToList();
        }

        if (pool.Count == 0)
        {
            pool = libraryTracks.Where(IsNotShortTrack).ToList();
        }

        if (pool.Count == 0) return null;

        var pick = pool[Random.Shared.Next(pool.Count)];
        return new RecommendationResult
        {
            Track = pick,
            Score = 0.5,
            Reason = reason
        };
    }

    private void LoadNpzArchive(string npzPath)
    {
        using var fs = File.OpenRead(npzPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Read);

        var guids = NpyReader.ReadStringArray(archive, "guids.npy");
        var clap = NpyReader.ReadFloatArray(archive, "clap_embeddings.npy", out var clapRows, out var clapCols);
        // Mean-center + L2-normalize to eliminate CLAP's 0.899 anisotropy cone collapse
        CenterAndNormalizeRows(clap, clapRows, clapCols);

        _guidToIndex.Clear();
        _trackCount = Math.Min(guids.Length, clapRows);
        for (var i = 0; i < _trackCount; i++)
        {
            if (Guid.TryParse(guids[i], out var g))
                _guidToIndex[g] = i;
        }

        _dimension = clapCols;
        _embeddingsFlat = clap;
        _sessionSteeringVector = new float[_dimension];

        if (archive.GetEntry("dsp_features_z.npy") != null)
        {
            var dsp = NpyReader.ReadFloatArray(archive, "dsp_features_z.npy", out var dspRows, out var dspCols);
            NormalizeRows(dsp, dspRows, dspCols);
            _dspDimension = dspCols;
            _dspFlat = dsp;
        }

        if (archive.GetEntry("dsp_features_raw.npy") != null && archive.GetEntry("dsp_feature_names.npy") != null)
        {
            var raw = NpyReader.ReadFloatArray(archive, "dsp_features_raw.npy", out var rawRows, out var rawCols);
            var names = NpyReader.ReadStringArray(archive, "dsp_feature_names.npy");
            _dspRaw = raw;
            _dspRawCols = rawCols;
            _dspFeatureNames = names;

            var bpmCol = Array.IndexOf(names, "bpm");
            var loudCol = Array.IndexOf(names, "loudness_db");

            if (bpmCol >= 0)
            {
                _trackBpm = new float[rawRows];
                for (var r = 0; r < rawRows; r++) _trackBpm[r] = raw[r * rawCols + bpmCol];
            }
            if (loudCol >= 0)
            {
                _trackLoudnessDb = new float[rawRows];
                for (var r = 0; r < rawRows; r++) _trackLoudnessDb[r] = raw[r * rawCols + loudCol];
            }
        }

        var labelsPath = AudioLabelsCandidates.FirstOrDefault(File.Exists);
        if (labelsPath != null)
        {
            try
            {
                var labelsJson = File.ReadAllText(labelsPath);
                var parsed = JsonSerializer.Deserialize<Dictionary<string, AudioLabelEntry>>(labelsJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (parsed != null)
                {
                    _audioLabels.Clear();
                    foreach (var kv in parsed)
                    {
                        if (Guid.TryParse(kv.Key, out var g))
                            _audioLabels[g] = kv.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load audio labels from '{labelsPath}': {ex.Message}");
            }
        }
    }

    public void EnrichMetadata(SLibIndexLiteTrack track, IReadOnlyList<SLibIndexLiteTrack>? libraryTracks = null)
    {
        if (track.Guid == Guid.Empty) return;
        EnsureInternalEngineLoaded(libraryTracks ?? [track]);

        var hasIndex = _guidToIndex.TryGetValue(track.Guid, out var idx) && idx < _trackCount;
        var hasLabels = _audioLabels.TryGetValue(track.Guid, out var labels);
        if (!hasIndex && !hasLabels) return;

        var entries = track.Entries ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (hasLabels && labels != null)
        {
            if (!string.IsNullOrEmpty(labels.Key)) entries["SLIB_KEY"] = labels.Key;
            if (!string.IsNullOrEmpty(labels.Mode)) entries["SLIB_MODE"] = labels.Mode;
            if (!string.IsNullOrEmpty(labels.Top_mood)) entries["SLIB_TOP_MOOD"] = labels.Top_mood;
            if (!string.IsNullOrEmpty(labels.Top_genre)) entries["SLIB_TOP_GENRE"] = labels.Top_genre;
            entries["SLIB_IS_INSTRUMENTAL"] = labels.Is_instrumental ? "true" : "false";
        }

        if (hasIndex && _dspRaw != null && _dspFeatureNames != null &&
            (idx + 1) * _dspRawCols <= _dspRaw.Length)
        {
            var rowOffset = idx * _dspRawCols;
            for (var c = 0; c < _dspFeatureNames.Length && c < _dspRawCols; c++)
            {
                var name = _dspFeatureNames[c];
                if (name.StartsWith("mfcc_", StringComparison.Ordinal) ||
                    name.StartsWith("contrast_", StringComparison.Ordinal) ||
                    name.StartsWith("chroma_", StringComparison.Ordinal))
                {
                    continue;
                }
                entries[$"SLIB_{name.ToUpperInvariant()}"] = _dspRaw[rowOffset + c].ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        track.Entries = entries;
    }

    private static void CenterAndNormalizeRows(float[] flat, int rows, int cols)
    {
        if (rows <= 1 || cols <= 0)
        {
            NormalizeRows(flat, rows, cols);
            return;
        }

        var mean = new float[cols];
        for (var r = 0; r < rows; r++)
        {
            var offset = r * cols;
            for (var c = 0; c < cols; c++)
            {
                mean[c] += flat[offset + c];
            }
        }
        for (var c = 0; c < cols; c++) mean[c] /= rows;

        for (var r = 0; r < rows; r++)
        {
            var offset = r * cols;
            for (var c = 0; c < cols; c++)
            {
                flat[offset + c] -= mean[c];
            }
        }

        NormalizeRows(flat, rows, cols);
    }

    private static void NormalizeRows(float[] flat, int rows, int cols)
    {
        for (var r = 0; r < rows; r++)
        {
            var offset = r * cols;
            var normSq = 0.0f;
            for (var c = 0; c < cols; c++)
            {
                var v = flat[offset + c];
                normSq += v * v;
            }
            var norm = MathF.Sqrt(normSq);
            if (norm > 1e-7f)
            {
                for (var c = 0; c < cols; c++) flat[offset + c] /= norm;
            }
        }
    }

    private static void ClampVectorNorm(float[] vec, float maxNorm)
    {
        var normSq = 0.0f;
        for (var i = 0; i < vec.Length; i++) normSq += vec[i] * vec[i];
        var norm = MathF.Sqrt(normSq);
        if (norm > maxNorm && norm > 1e-7f)
        {
            var scale = maxNorm / norm;
            for (var i = 0; i < vec.Length; i++) vec[i] *= scale;
        }
    }

    private static void NormalizeVector(float[] vec)
    {
        var normSq = 0.0f;
        for (var i = 0; i < vec.Length; i++) normSq += vec[i] * vec[i];
        var norm = MathF.Sqrt(normSq);
        if (norm > 1e-7f)
        {
            for (var i = 0; i < vec.Length; i++) vec[i] /= norm;
        }
    }

    private static float DotProduct(float[] a, float[] b, int bOffset, int dim)
    {
        var sum = 0.0f;
        var vectorSize = Vector<float>.Count;
        var i = 0;

        for (; i <= dim - vectorSize; i += vectorSize)
        {
            var va = new Vector<float>(a, i);
            var vb = new Vector<float>(b, bOffset + i);
            sum += Vector.Dot(va, vb);
        }

        for (; i < dim; i++)
        {
            sum += a[i] * b[bOffset + i];
        }

        return sum;
    }

    public Task<List<SLibIndexLiteTrack>> GenerateRadioMixAsync(
        IReadOnlyList<SLibIndexLiteTrack> libraryTracks,
        SLibIndexLiteTrack seedTrack,
        int count = 10)
    {
        return Task.Run(() =>
        {
            EnsureInternalEngineLoaded(libraryTracks);

            List<Guid> localHistory;
            HashSet<Guid> localPlayed;
            lock (_sync)
            {
                localHistory = [.. History];
                localPlayed = [.. PlayedInSession];
            }

            if (seedTrack.Guid != Guid.Empty)
            {
                if (localHistory.Count == 0 || localHistory[^1] != seedTrack.Guid)
                {
                    localHistory.Add(seedTrack.Guid);
                    if (localHistory.Count > 25) localHistory.RemoveAt(0);
                }
                localPlayed.Add(seedTrack.Guid);
            }

            var results = new List<SLibIndexLiteTrack>();
            for (var i = 0; i < count; i++)
            {
                var pick = PickNextInternal(libraryTracks, localHistory, localPlayed, temperature: 0.14, allowSameAlbum: false);
                if (pick?.Track == null) break;

                results.Add(pick.Track);
                localHistory.Add(pick.Track.Guid);
                if (localHistory.Count > 25) localHistory.RemoveAt(0);
                localPlayed.Add(pick.Track.Guid);
            }

            return results;
        });
    }
}
