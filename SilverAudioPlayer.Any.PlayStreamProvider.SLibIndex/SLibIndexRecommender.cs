using System.Diagnostics;
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
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SLibIndexRecommender");

    private static readonly string EmbeddingsBinPath = Path.Combine(CacheDir, "embeddings.bin");
    private static readonly string MetadataJsonPath = Path.Combine(CacheDir, "track_metadata.json");
    private static readonly string ArtistKnowledgeJsonPath = Path.Combine(CacheDir, "artist_knowledge.json");
    private static readonly string UserProfileJsonPath = Path.Combine(CacheDir, "user_profile.json");
    private static readonly string UserFeedbackJsonPath = Path.Combine(CacheDir, "user_feedback.json");

    private readonly HttpClient _httpClient;
    private readonly string _serviceUrl;

    // In-memory cache for native C# fallback engine (< 15 MB RAM, SIMD accelerated)
    private float[]? _embeddingsFlat; // TotalCount * Dimension (384)
    private int _trackCount;
    private const int Dimension = 384;
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
    private readonly float[] _sessionSteeringVector = new float[Dimension];

    private bool _internalEngineLoaded;

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

    public class UserFeedbackData
    {
        public List<string> LikedGuids { get; set; } = [];
        public List<string> ContextualMismatches { get; set; } = [];
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
            History.Add(trackGuid);
            PlayedInSession.Add(trackGuid);
            if (History.Count > 25)
            {
                History.RemoveAt(0);
            }
        }
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
            // 1. Session fatigue: add to current session so it won't repeat immediately in this session
            PlayedInSession.Add(trackGuid);

            // 2. Latent vector steering: steer the active session representation away from this song's subspace
            if (_guidToIndex.TryGetValue(trackGuid, out var idx) && idx < _trackCount && _embeddingsFlat != null)
            {
                var offset = idx * Dimension;
                for (var d = 0; d < Dimension; d++)
                {
                    _sessionSteeringVector[d] -= 0.40f * _embeddingsFlat[offset + d];
                }
            }

            // 3. Contextual transition penalty: record that fromArtist -> candidateArtist was a poor transition
            if (!string.IsNullOrWhiteSpace(fromArtist) && !string.IsNullOrWhiteSpace(candidateArtist) &&
                !string.Equals(fromArtist, candidateArtist, StringComparison.OrdinalIgnoreCase))
            {
                var key = (fromArtist.Trim().ToLowerInvariant(), candidateArtist.Trim().ToLowerInvariant());
                _transitionMismatches[key] = _transitionMismatches.GetValueOrDefault(key) + 1;
            }
        }

        SaveFeedback();
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

            // Positive latent vector steering
            if (_guidToIndex.TryGetValue(trackGuid, out var idx) && idx < _trackCount && _embeddingsFlat != null)
            {
                var offset = idx * Dimension;
                for (var d = 0; d < Dimension; d++)
                {
                    _sessionSteeringVector[d] += 0.20f * _embeddingsFlat[offset + d];
                }
            }

            // Reinforce transition
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
            UserFeedbackData data;
            lock (_sync)
            {
                data = new UserFeedbackData
                {
                    LikedGuids = _likedGuids.Select(g => g.ToString("D")).ToList(),
                    ContextualMismatches = _transitionMismatches.Where(kv => kv.Value > 0)
                        .Select(kv => $"{kv.Key.FromArtist} -> {kv.Key.ToArtist} ({kv.Value})").ToList()
                };
            }

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(UserFeedbackJsonPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save user feedback: {ex.Message}");
        }
    }

    public async Task<RecommendationResult?> PickNextAsync(
        IReadOnlyList<SLibIndexLiteTrack> libraryTracks,
        SLibIndexLiteTrack? currentTrack = null,
        double temperature = 0.12,
        bool allowSameAlbum = false)
    {
        if (currentTrack != null)
        {
            lock (_sync)
            {
                if (History.Count == 0 || History[^1] != currentTrack.Guid)
                {
                    History.Add(currentTrack.Guid);
                    PlayedInSession.Add(currentTrack.Guid);
                    if (History.Count > 25) History.RemoveAt(0);
                }
            }
        }

        // Native C# high-performance engine (< 15 MB RAM, SIMD accelerated, session-steered)
        return await Task.FromResult(PickNextInternal(libraryTracks, temperature, allowSameAlbum));
    }

    private void EnsureInternalEngineLoaded(IReadOnlyList<SLibIndexLiteTrack> libraryTracks)
    {
        if (_internalEngineLoaded) return;
        lock (this)
        {
            if (_internalEngineLoaded) return;

            if (File.Exists(EmbeddingsBinPath) && File.Exists(MetadataJsonPath))
            {
                try
                {
                    var bytes = File.ReadAllBytes(EmbeddingsBinPath);
                    _trackCount = bytes.Length / (Dimension * sizeof(float));
                    _embeddingsFlat = new float[_trackCount * Dimension];
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
                        foreach (var gStr in fb.LikedGuids)
                            if (Guid.TryParse(gStr, out var g)) _likedGuids.Add(g);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load user feedback: {ex.Message}");
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

    private static float ComputeTagJaccard(List<string> tags1, List<string> tags2)
    {
        if (tags1.Count == 0 || tags2.Count == 0) return 0f;
        var set1 = new HashSet<string>(tags1, StringComparer.OrdinalIgnoreCase);
        var set2 = new HashSet<string>(tags2, StringComparer.OrdinalIgnoreCase);
        var intersect = set1.Count(t => set2.Contains(t));
        var union = set1.Count + set2.Count - intersect;
        return union > 0 ? (float)intersect / union : 0f;
    }

    private static bool IsSoundtrack(List<string> tags, string? genre)
    {
        var g = genre?.ToLowerInvariant() ?? "";
        if (g.Contains("soundtrack") || g.Contains("vgm") || g.Contains("score") || g.Contains("game"))
            return true;
        foreach (var t in tags)
        {
            var tl = t.ToLowerInvariant();
            if (tl is "soundtrack" or "video game music" or "vgm" or "score" or "ost" or "chiptune")
                return true;
        }
        return false;
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

        return Math.Min(1.0f, score);
    }

    private bool IsMetadataAcceptable(SLibIndexLiteTrack track)
    {
        return CalculateMetadataQuality(track) >= 0.35f;
    }

    private RecommendationResult? PickNextInternal(
        IReadOnlyList<SLibIndexLiteTrack> libraryTracks,
        double temperature,
        bool allowSameAlbum)
    {
        EnsureInternalEngineLoaded(libraryTracks);
        if (libraryTracks.Count == 0) return null;

        List<Guid> historySnapshot;
        HashSet<Guid> playedSnapshot;
        lock (_sync)
        {
            historySnapshot = [.. History];
            playedSnapshot = [.. PlayedInSession];
        }

        if (_embeddingsFlat == null || _guidToIndex.Count == 0)
        {
            return PickNextByMetadataFeatures(libraryTracks, historySnapshot, playedSnapshot, allowSameAlbum);
        }

        var validHistory = new List<int>();
        var playedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var guid in historySnapshot)
        {
            if (_guidToIndex.TryGetValue(guid, out var idx) && idx < _trackCount)
            {
                validHistory.Add(idx);
                var t = libraryTracks.FirstOrDefault(x => x.Guid == guid);
                if (t?.Title != null) playedTitles.Add(t.Title.Trim());
            }
        }

        if (validHistory.Count == 0)
        {
            var wellTagged = libraryTracks.Where(t => IsMetadataAcceptable(t) && (t.Duration ?? 0) / 1000.0 is >= 75 and <= 360).ToList();
            var pick = wellTagged.Count > 0 ? wellTagged[Random.Shared.Next(wellTagged.Count)] : libraryTracks[Random.Shared.Next(libraryTracks.Count)];
            return new RecommendationResult
            {
                Track = pick,
                Score = 0.5,
                Reason = "Library start"
            };
        }

        // Context vector with exponential decay (0.70) + session latent steering (pushes away from skipped/mismatched styles)
        var context = new float[Dimension];
        var totalWeight = 0.0f;
        var decay = 0.70f;

        for (var i = 0; i < validHistory.Count && i < 5; i++)
        {
            var hIdx = validHistory[validHistory.Count - 1 - i];
            var weight = MathF.Pow(decay, i);
            var offset = hIdx * Dimension;

            for (var d = 0; d < Dimension; d++)
            {
                context[d] += weight * _embeddingsFlat[offset + d];
            }
            totalWeight += weight;
        }

        for (var d = 0; d < Dimension; d++)
        {
            context[d] /= Math.Max(totalWeight, 1e-6f);
            // Apply latent vector steering (contrastive session nudge away from skipped / not-a-fit subspaces)
            context[d] += _sessionSteeringVector[d] * 0.35f;
        }

        var normSq = 0.0f;
        for (var d = 0; d < Dimension; d++) normSq += context[d] * context[d];
        var norm = MathF.Sqrt(normSq);
        if (norm > 1e-7f)
        {
            for (var d = 0; d < Dimension; d++) context[d] /= norm;
        }

        var lastTrack = libraryTracks.FirstOrDefault(t => t.Guid == historySnapshot[^1]);
        var lastArtist = lastTrack?.Artist?.Trim() ?? "";
        var lastAlbum = lastTrack?.Album?.Trim() ?? "";
        var lastTrackNum = lastTrack?.TrackNumber ?? 0;

        var lastTags = GetArtistTags(lastArtist);
        var lastIsOst = IsSoundtrack(lastTags, lastTrack?.Genre);
        var similarArtistsSet = GetSimilarArtists(lastArtist);

        // Count consecutive plays by same artist
        var consecutiveSameArtist = 0;
        for (var i = historySnapshot.Count - 1; i >= 0; i--)
        {
            var t = libraryTracks.FirstOrDefault(x => x.Guid == historySnapshot[i]);
            if (string.Equals(t?.Artist?.Trim(), lastArtist, StringComparison.OrdinalIgnoreCase))
                consecutiveSameArtist++;
            else
                break;
        }

        var candidates = new List<(SLibIndexLiteTrack track, float score, float rawSim)>();

        foreach (var track in libraryTracks)
        {
            // 1. Session fatigue exclusion only (no permanent track blocking!)
            if (playedSnapshot.Contains(track.Guid)) continue;

            var tTitle = track.Title?.Trim();
            if (!string.IsNullOrEmpty(tTitle) && playedTitles.Contains(tTitle)) continue;

            // 2. Metadata quality constraint
            var quality = CalculateMetadataQuality(track);
            if (quality < 0.35f) continue;

            // 3. Duration constraint (no really short or really long songs)
            var durSec = (track.Duration ?? 0) / 1000.0;
            if (durSec is > 0 and (< 60.0 or > 600.0)) continue;

            if (!_guidToIndex.TryGetValue(track.Guid, out var idx) || idx >= _trackCount) continue;

            var sim = DotProduct(context, _embeddingsFlat, idx * Dimension);
            var score = sim;

            if (durSec is > 0 and < 90.0)
                score -= (float)(90.0 - durSec) * 0.012f;
            if (durSec is > 420.0)
                score -= (float)(durSec - 420.0) * 0.002f;

            score *= (0.6f + 0.4f * quality);

            var tArtist = track.Artist?.Trim() ?? "";
            var tAlbum = track.Album?.Trim() ?? "";
            var tTags = GetArtistTags(tArtist);
            var tIsOst = IsSoundtrack(tTags, track.Genre);

            // 4. Dynamic Tag Jaccard & Soundtrack/VGM separation (NO HARDCODED ARTISTS!)
            if (lastTags.Count > 0 && tTags.Count > 0)
            {
                if (lastIsOst != tIsOst)
                {
                    score -= 0.65f;
                }
                else
                {
                    var jaccard = ComputeTagJaccard(lastTags, tTags);
                    score += jaccard * 0.45f;
                }
            }
            else if (!string.IsNullOrEmpty(track.Genre) && !string.IsNullOrEmpty(lastTrack?.Genre))
            {
                if (lastIsOst != tIsOst)
                {
                    score -= 0.65f;
                }
            }

            // 5. Collaborative filtering dataset similar artist affinity (Last.fm / ListenBrainz)
            if (!string.IsNullOrEmpty(tArtist) && similarArtistsSet.Contains(tArtist))
            {
                score += 0.35f;
            }

            // 6. User's personal ListenBrainz transitions & co-occurrences
            if (_userProfile?.ArtistTransitions != null &&
                _userProfile.ArtistTransitions.TryGetValue(lastArtist.ToLowerInvariant(), out var transMap) &&
                transMap.ContainsKey(tArtist.ToLowerInvariant()))
            {
                score += 0.35f;
            }
            else if (_userProfile?.SessionCooccurrences != null &&
                     _userProfile.SessionCooccurrences.TryGetValue(lastArtist.ToLowerInvariant(), out var coMap) &&
                     coMap.ContainsKey(tArtist.ToLowerInvariant()))
            {
                score += 0.20f;
            }

            // 7. Contextual transition fit (learned from "Not a fit" feedback in this specific transition)
            if (!string.IsNullOrEmpty(lastArtist) && !string.IsNullOrEmpty(tArtist))
            {
                var transKey = (lastArtist.ToLowerInvariant(), tArtist.ToLowerInvariant());
                if (_transitionMismatches.TryGetValue(transKey, out var mismatchCount))
                {
                    score -= Math.Min(0.50f, mismatchCount * 0.25f);
                }
                if (_transitionAffinities.TryGetValue(transKey, out var transAffinity))
                {
                    score += transAffinity;
                }
            }

            // 8. User's loved artist / track bonus
            if (_userProfile != null)
            {
                if (_userProfile.LovedArtists != null && _userProfile.LovedArtists.ContainsKey(tArtist.ToLowerInvariant()))
                {
                    score += 0.20f;
                }
                if (!string.IsNullOrEmpty(track.Title) && _userProfile.LovedTitles != null && _userProfile.LovedTitles.Contains(track.Title.ToLowerInvariant()))
                {
                    score += 0.25f;
                }
            }

            // 9. Artist pacing (variance is good, too much variance is not)
            if (!string.IsNullOrEmpty(tArtist) && string.Equals(tArtist, lastArtist, StringComparison.OrdinalIgnoreCase))
            {
                if (consecutiveSameArtist == 1)
                    score += 0.08f;
                else if (consecutiveSameArtist >= 2)
                    score -= 0.40f;
            }

            // 10. Album monotony breaker (not always is it best to play the whole album)
            if (!string.IsNullOrEmpty(tAlbum) && string.Equals(tAlbum, lastAlbum, StringComparison.OrdinalIgnoreCase))
            {
                if (!allowSameAlbum)
                {
                    score -= 0.35f;
                    if (track.TrackNumber == lastTrackNum + 1)
                    {
                        score -= 0.30f;
                    }
                }
            }

            candidates.Add((track, score, sim));
        }

        if (candidates.Count == 0)
        {
            var unplayed = libraryTracks
                .Where(t => !playedSnapshot.Contains(t.Guid) && IsMetadataAcceptable(t) && (t.Duration ?? 0) / 1000.0 is >= 75 and <= 400)
                .ToList();
            var pick = unplayed.Count > 0 ? unplayed[Random.Shared.Next(unplayed.Count)] : libraryTracks[Random.Shared.Next(libraryTracks.Count)];
            return new RecommendationResult
            {
                Track = pick,
                Score = 0.5,
                Reason = "Library fallback"
            };
        }

        var top5 = candidates.OrderByDescending(c => c.score).Take(5).ToList();

        // Low temperature softmax sampling
        var maxScore = top5[0].score;
        var temp = (float)Math.Max(temperature, 0.08);
        var exps = top5.Select(c => MathF.Exp((c.score - maxScore) / temp)).ToArray();
        var sumExp = exps.Sum();
        var roll = Random.Shared.NextSingle() * sumExp;
        var running = 0.0f;
        var chosen = top5[0];

        for (var i = 0; i < top5.Count; i++)
        {
            running += exps[i];
            if (roll <= running)
            {
                chosen = top5[i];
                break;
            }
        }

        var reasons = new List<string>();
        var chosenArtist = chosen.track.Artist ?? "";
        var chosenTags = GetArtistTags(chosenArtist);

        if (_userProfile?.ArtistTransitions != null &&
            _userProfile.ArtistTransitions.TryGetValue(lastArtist.ToLowerInvariant(), out var tMap) &&
            tMap.ContainsKey(chosenArtist.ToLowerInvariant()))
        {
            reasons.Add("user history transition");
        }
        else if (!string.IsNullOrEmpty(chosenArtist) && similarArtistsSet.Contains(chosenArtist))
        {
            reasons.Add("collaborative filtering match");
        }
        else if (lastTags.Count > 0 && chosenTags.Count > 0)
        {
            var shared = lastTags.Intersect(chosenTags, StringComparer.OrdinalIgnoreCase).Take(2).ToList();
            if (shared.Count > 0)
            {
                reasons.Add($"tags: {string.Join(", ", shared)}");
            }
        }
        else if (!string.IsNullOrEmpty(chosen.track.Genre))
        {
            reasons.Add($"{chosen.track.Genre} genre");
        }

        if (!string.IsNullOrEmpty(chosenArtist) && !string.Equals(chosenArtist, lastArtist, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add($"transition to {chosenArtist}");
        }
        else if (!string.IsNullOrEmpty(chosenArtist))
        {
            reasons.Add($"by {chosenArtist}");
        }
        reasons.Add($"{(int)(chosen.rawSim * 100)}% match");

        return new RecommendationResult
        {
            Track = chosen.track,
            Score = chosen.rawSim,
            Reason = string.Join(", ", reasons)
        };
    }

    private static float DotProduct(float[] a, float[] b, int bOffset)
    {
        var sum = 0.0f;
        var vectorSize = Vector<float>.Count;
        var i = 0;

        for (; i <= Dimension - vectorSize; i += vectorSize)
        {
            var va = new Vector<float>(a, i);
            var vb = new Vector<float>(b, bOffset + i);
            sum += Vector.Dot(va, vb);
        }

        for (; i < Dimension; i++)
        {
            sum += a[i] * b[bOffset + i];
        }

        return sum;
    }

    private RecommendationResult PickNextByMetadataFeatures(
        IReadOnlyList<SLibIndexLiteTrack> libraryTracks,
        List<Guid> historySnapshot,
        HashSet<Guid> playedSnapshot,
        bool allowSameAlbum)
    {
        var lastTrack = libraryTracks.FirstOrDefault(t => historySnapshot.Count > 0 && t.Guid == historySnapshot[^1]);
        var lastArtist = lastTrack?.Artist?.Trim() ?? "";
        var lastAlbum = lastTrack?.Album?.Trim() ?? "";

        var candidates = libraryTracks
            .Where(t => !playedSnapshot.Contains(t.Guid) && IsMetadataAcceptable(t) && (t.Duration ?? 0) / 1000.0 is >= 60 and <= 480)
            .Select(t =>
            {
                var score = 0.1f;
                if (!string.IsNullOrEmpty(t.Artist) && string.Equals(t.Artist, lastArtist, StringComparison.OrdinalIgnoreCase)) score += 0.2f;
                if (!allowSameAlbum && !string.IsNullOrEmpty(t.Album) && string.Equals(t.Album, lastAlbum, StringComparison.OrdinalIgnoreCase)) score -= 0.35f;

                score += Random.Shared.NextSingle() * 0.08f;
                return (track: t, score);
            })
            .OrderByDescending(c => c.score)
            .Take(5)
            .ToList();

        var pick = candidates.Count > 0 ? candidates[Random.Shared.Next(candidates.Count)].track : libraryTracks[0];
        return new RecommendationResult
        {
            Track = pick,
            Score = 0.7,
            Reason = "Metadata smart match"
        };
    }

    public async Task<List<SLibIndexLiteTrack>> GenerateRadioMixAsync(
        IReadOnlyList<SLibIndexLiteTrack> libraryTracks,
        SLibIndexLiteTrack seedTrack,
        int count = 10)
    {
        var results = new List<SLibIndexLiteTrack>();
        RecordPlayed(seedTrack.Guid);

        for (var i = 0; i < count; i++)
        {
            var pick = await PickNextAsync(libraryTracks, seedTrack);
            if (pick?.Track == null) break;
            results.Add(pick.Track);
            RecordPlayed(pick.Track.Guid);
            seedTrack = pick.Track;
        }

        return results;
    }
}
