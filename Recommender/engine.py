import os
import sys
import json
import math
import random
import urllib.request
from typing import List, Dict, Any, Optional, Tuple, Set
import numpy as np

try:
    import onnxruntime as ort
    from tokenizers import Tokenizer
except ImportError:
    ort = None
    Tokenizer = None

SLIBINDEX_DEFAULT_URL = "http://192.168.1.173:32542/api"
MODEL_URL = "https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/onnx/model_quantized.onnx"
TOKENIZER_URL = "https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/tokenizer.json"

CACHE_DIR = os.path.join(os.environ.get("LOCALAPPDATA", os.path.expanduser("~")), "SLibIndexRecommender")
MODEL_DIR = os.path.join(CACHE_DIR, "model")
EMBEDDINGS_PATH = os.path.join(CACHE_DIR, "track_embeddings.npy")
METADATA_PATH = os.path.join(CACHE_DIR, "track_metadata.json")
EMBEDDINGS_BIN_PATH = os.path.join(CACHE_DIR, "embeddings.bin")
KB_PATH = os.path.join(CACHE_DIR, "artist_knowledge.json")

# Global artist knowledge base loaded from external datasets (ListenBrainz/Last.fm/MusicBrainz)
ARTIST_KB: Dict[str, Dict[str, Any]] = {}

def load_artist_kb() -> Dict[str, Dict[str, Any]]:
    global ARTIST_KB
    if ARTIST_KB:
        return ARTIST_KB
    if os.path.exists(KB_PATH):
        try:
            with open(KB_PATH, "r", encoding="utf-8") as f:
                ARTIST_KB = json.load(f)
        except Exception as e:
            print(f"Failed to load artist knowledge base: {e}")
    return ARTIST_KB


def get_artist_tags(artist_name: str) -> List[str]:
    kb = load_artist_kb()
    if not artist_name:
        return []
    info = kb.get(artist_name)
    if not info:
        # Check case-insensitive
        for k, v in kb.items():
            if k.lower() == artist_name.lower():
                return v.get("tags", [])
        return []
    return info.get("tags", [])


def get_similar_artists(artist_name: str) -> Set[str]:
    kb = load_artist_kb()
    if not artist_name:
        return set()
    info = kb.get(artist_name)
    if not info:
        for k, v in kb.items():
            if k.lower() == artist_name.lower():
                return set(s.lower() for s in v.get("similar", []))
        return set()
    return set(s.lower() for s in info.get("similar", []))


def classify_track_cluster(track_data: Dict[str, Any]) -> str:
    title = (track_data.get("title") or "").lower()
    artist = (track_data.get("artist") or "").strip()
    artist_lower = artist.lower()
    album = (track_data.get("album") or "").lower()
    genre = (track_data.get("genre") or "").lower()
    entries = track_data.get("entries") or {}
    rel = (entries.get("RELEASETYPE") or "").lower()
    loc = (track_data.get("location") or "").lower()

    # 1. Check artist tags from external music dataset (ListenBrainz/Last.fm/MusicBrainz)
    artist_tags = [t.lower() for t in get_artist_tags(artist)]

    # Lemon Demon / Neil Cicierega
    if "lemon demon" in artist_lower or "cicierega" in artist_lower or any("geek rock" in t for t in artist_tags):
        return "LEMON_DEMON_INDIE"

    # Dance / House / Eurodance / Club / 90s Dance
    dance_tags = ["dance", "house", "eurodance", "club", "trance", "techno", "french house", "disco"]
    if any(t in dance_tags for t in artist_tags) or any(t in genre for t in ["dance", "house", "eurodance", "club"]):
        if not any(k in genre for k in ["game", "soundtrack"]):
            return "DANCE_CLUB_HOUSE"

    # Soundtrack / Game Music / Anime
    ost_keywords = ["soundtrack", "ost", "vgm", "score", "game", "anime", "persona", "undertale", "splatoon", "nintendo"]
    if any(k in genre for k in ["soundtrack", "game", "vgm", "score", "anime"]):
        return "SOUNDTRACK_VGM"
    if any(k in rel for k in ["soundtrack"]):
        return "SOUNDTRACK_VGM"
    if any(k in album for k in ["soundtrack", "ost", "score", "splatune", "original sound", "toby fox"]):
        return "SOUNDTRACK_VGM"
    if any(t in ["soundtrack", "video game music", "game soundtrack", "chiptune"] for t in artist_tags):
        return "SOUNDTRACK_VGM"
    if any(k in loc for k in ["/anime/", "/persona", "undertale", "soundtrack"]):
        return "SOUNDTRACK_VGM"

    # Balkan / Macedonian
    balkan_keywords = ["darko dimitrov", "vlado janevski", "rebeka", "erzana", "tose", "karolina", "calgii", "oro"]
    if any(b in artist_lower for b in balkan_keywords) or any("macedon" in t for t in artist_tags):
        return "BALKAN_REGIONAL"
    if any(b in loc for b in ["/balkan/", "/makedonsk", "darko dimitrov"]):
        return "BALKAN_REGIONAL"

    # Electronic / Synth / Vaporwave
    if any(k in genre for k in ["vaporwave", "electronic", "electro", "synth", "dream"]) or any("electronic" in t or "synthpop" in t for t in artist_tags):
        return "ELECTRONIC_SYNTH"

    # Rock / Alternative
    if any(k in genre for k in ["rock", "alternative", "indie", "punk", "metal"]) or any("rock" in t or "indie" in t for t in artist_tags):
        return "ROCK_ALTERNATIVE"

    # Hip-Hop / Rap
    if any(k in genre for k in ["rap", "hip-hop", "hip hop"]) or any("hip-hop" in t or "rap" in t for t in artist_tags):
        return "HIP_HOP_RAP"

    # Pop / Mainstream
    if any(k in genre for k in ["pop"]) or any("pop" in t for t in artist_tags):
        return "POP_MAINSTREAM"

    return "GENERAL"


def calculate_metadata_quality(track_data: Dict[str, Any]) -> float:
    title = (track_data.get("title") or "").strip()
    artist = (track_data.get("artist") or "").strip()
    album = (track_data.get("album") or "").strip()
    genre = (track_data.get("genre") or "").strip()

    if not artist or artist in ("????", "Unknown", "Various Artists", "Various"):
        return 0.1
    if artist.lower() == title.lower():
        return 0.1
    if album.lower().startswith("cd") and len(album) <= 4:
        return 0.15

    score = 0.35
    if album and album.lower() not in ("unknown", "various", "untitled"):
        score += 0.25

    tags = get_artist_tags(artist)
    if genre and genre.lower() not in ("unknown", "other", "general"):
        score += 0.2
    elif tags:
        score += 0.2

    entries = track_data.get("entries") or {}
    if entries.get("MUSICBRAINZ_TRACKID") or entries.get("MUSICBRAINZ_ARTISTID") or entries.get("RELEASETYPE"):
        score += 0.2

    return min(1.0, score)


class TrackItem:
    def __init__(self, data: Dict[str, Any]):
        self.guid = (data.get("guid") or data.get("Guid") or "").lower()
        self.title = data.get("title") or data.get("Title") or ""
        self.artist = data.get("artist") or data.get("Artist") or "Unknown"
        self.album = data.get("album") or data.get("Album") or "Unknown"
        self.genre = data.get("genre") or data.get("Genre") or ""
        self.year = data.get("year") or data.get("Year") or 0
        self.track_number = data.get("trackNumber") or data.get("TrackNumber") or 0
        self.disc_number = data.get("discNumber") or data.get("DiscNumber") or 1
        self.duration = float(data.get("duration") or data.get("Duration") or 0.0)
        self.file_name = data.get("fileName") or data.get("FileName") or ""
        self.art_hash = data.get("artHash") or data.get("ArtHash") or ""
        self.location = data.get("location") or data.get("Location") or ""

        entries = data.get("entries") or {}
        self.mb_artist_id = entries.get("MUSICBRAINZ_ARTISTID") or ""
        self.mb_album_id = entries.get("MUSICBRAINZ_ALBUMID") or ""
        self.artist_sort = entries.get("ARTISTS_SORT") or entries.get("ALBUMARTISTS_SORT") or ""
        self.artist_credit = entries.get("ARTIST_CREDIT") or entries.get("ALBUMARTIST_CREDIT") or ""
        self.release_type = entries.get("RELEASETYPE") or entries.get("MUSICBRAINZ_ALBUMTYPE") or ""

        # Enriched from external knowledge base
        self.artist_tags = get_artist_tags(self.artist)
        self.similar_artists = get_similar_artists(self.artist)

        self.cluster = classify_track_cluster(data)
        self.quality = calculate_metadata_quality(data)

        if not self.genre:
            if self.artist_tags:
                self.genre = self.artist_tags[0].title()
            elif self.cluster == "DANCE_CLUB_HOUSE":
                self.genre = "Dance"
            elif self.cluster == "SOUNDTRACK_VGM":
                self.genre = "Soundtrack"
            elif self.cluster == "LEMON_DEMON_INDIE":
                self.genre = "Indie Pop"
            elif self.cluster == "ELECTRONIC_SYNTH":
                self.genre = "Electronic"
            elif self.cluster == "ROCK_ALTERNATIVE":
                self.genre = "Rock"
            elif self.cluster == "POP_MAINSTREAM":
                self.genre = "Pop"
            else:
                self.genre = "General"

    def duration_seconds(self) -> float:
        return self.duration / 1000.0 if self.duration > 0 else 0.0

    def to_dict(self) -> Dict[str, Any]:
        return {
            "guid": self.guid,
            "title": self.title,
            "artist": self.artist,
            "album": self.album,
            "genre": self.genre,
            "year": self.year,
            "trackNumber": self.track_number,
            "discNumber": self.disc_number,
            "duration": self.duration,
            "fileName": self.file_name,
            "artHash": self.art_hash,
            "cluster": self.cluster,
            "quality": self.quality,
            "artistTags": self.artist_tags,
            "mbArtistId": self.mb_artist_id,
            "artistSort": self.artist_sort,
            "releaseType": self.release_type,
        }

    def text_representation(self) -> str:
        parts = []
        if self.title:
            parts.append(f"Title: {self.title}")

        artist_parts = [self.artist]
        if self.artist_sort and self.artist_sort.lower() != self.artist.lower():
            artist_parts.append(self.artist_sort)
        if self.artist_credit and self.artist_credit.lower() != self.artist.lower():
            artist_parts.append(self.artist_credit)
        parts.append(f"Artist: {' / '.join(set(artist_parts))}")

        if self.album:
            parts.append(f"Album: {self.album}")
        if self.genre:
            parts.append(f"Genre: {self.genre}")
        if self.artist_tags:
            parts.append(f"Tags: {', '.join(self.artist_tags[:4])}")
        if self.cluster:
            parts.append(f"Style: {self.cluster}")
        if self.year and self.year > 1900:
            parts.append(f"Year: {self.year}")

        return " | ".join(parts)


class MusicRecommenderEngine:
    def __init__(self, server_url: str = SLIBINDEX_DEFAULT_URL):
        self.server_url = server_url.rstrip("/")
        if not self.server_url.endswith("/api"):
            self.server_url += "/api"

        os.makedirs(MODEL_DIR, exist_ok=True)
        load_artist_kb()
        self.tracks: List[TrackItem] = []
        self.guid_to_index: Dict[str, int] = {}
        self.embeddings: Optional[np.ndarray] = None
        self.artist_counts: Dict[str, int] = {}
        self.session: Optional[Any] = None
        self.tokenizer: Optional[Any] = None

    def ensure_model_files(self):
        model_file = os.path.join(MODEL_DIR, "model_quantized.onnx")
        tok_file = os.path.join(MODEL_DIR, "tokenizer.json")

        if not os.path.exists(model_file):
            print(f"Downloading quantized ONNX model from {MODEL_URL}...")
            urllib.request.urlretrieve(MODEL_URL, model_file)

        if not os.path.exists(tok_file):
            print(f"Downloading tokenizer from {TOKENIZER_URL}...")
            urllib.request.urlretrieve(TOKENIZER_URL, tok_file)

        return model_file, tok_file

    def load_model(self):
        if self.session is not None and self.tokenizer is not None:
            return

        model_file, tok_file = self.ensure_model_files()
        opts = ort.SessionOptions()
        opts.intra_op_num_threads = 2
        opts.execution_mode = ort.ExecutionMode.ORT_SEQUENTIAL
        opts.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_ALL

        self.session = ort.InferenceSession(model_file, opts, providers=["CPUExecutionProvider"])
        self.tokenizer = Tokenizer.from_file(tok_file)
        self.tokenizer.enable_padding(length=96)
        self.tokenizer.enable_truncation(max_length=96)

    def fetch_library_tracks(self) -> List[TrackItem]:
        for ep in ["/tracks", "/tracks/lite"]:
            url = f"{self.server_url}{ep}"
            try:
                print(f"Fetching library tracks from {url}...")
                req = urllib.request.Request(url, headers={"User-Agent": "SilverAudioPlayer-Recommender/1.0"})
                with urllib.request.urlopen(req, timeout=40) as resp:
                    data = json.loads(resp.read().decode("utf-8"))
                tracks = [TrackItem(item) for item in data if item.get("guid") or item.get("Guid")]
                print(f"Fetched {len(tracks)} tracks from {ep}.")
                return tracks
            except Exception as e:
                print(f"Failed to fetch from {url}: {e}")

        raise RuntimeError(f"Could not fetch tracks from SLibIndex server at {self.server_url}")

    def embed_texts(self, texts: List[str], batch_size: int = 64) -> np.ndarray:
        self.load_model()
        all_embeddings = []

        for i in range(0, len(texts), batch_size):
            batch_texts = texts[i : i + batch_size]
            encoded = self.tokenizer.encode_batch(batch_texts)

            input_ids = np.array([e.ids for e in encoded], dtype=np.int64)
            attention_mask = np.array([e.attention_mask for e in encoded], dtype=np.int64)
            token_type_ids = np.array([e.type_ids for e in encoded], dtype=np.int64)

            outputs = self.session.run(None, {
                "input_ids": input_ids,
                "attention_mask": attention_mask,
                "token_type_ids": token_type_ids
            })

            last_hidden_state = outputs[0]
            mask_expanded = np.expand_dims(attention_mask, -1).astype(np.float32)
            sum_embeddings = np.sum(last_hidden_state * mask_expanded, axis=1)
            sum_mask = np.clip(mask_expanded.sum(axis=1), a_min=1e-9, a_max=None)
            batch_emb = sum_embeddings / sum_mask

            norms = np.linalg.norm(batch_emb, axis=1, keepdims=True)
            norms = np.clip(norms, a_min=1e-9, a_max=None)
            batch_emb = batch_emb / norms
            all_embeddings.append(batch_emb.astype(np.float32))

        return np.vstack(all_embeddings)

    def initialize(self, force_refresh: bool = False):
        load_artist_kb()
        if not force_refresh and os.path.exists(METADATA_PATH) and os.path.exists(EMBEDDINGS_PATH):
            print("Loading cached library and embeddings from disk...")
            with open(METADATA_PATH, "r", encoding="utf-8") as f:
                meta = json.load(f)
            self.tracks = [TrackItem(item) for item in meta]
            self.embeddings = np.load(EMBEDDINGS_PATH)
            print(f"Loaded {len(self.tracks)} tracks with embeddings shape {self.embeddings.shape}.")
        else:
            self.tracks = self.fetch_library_tracks()
            texts = [t.text_representation() for t in self.tracks]
            t0 = time.time()
            self.embeddings = self.embed_texts(texts)
            t1 = time.time()
            print(f"Computed embeddings in {t1 - t0:.2f}s.")

            print("Saving cache...")
            with open(METADATA_PATH, "w", encoding="utf-8") as f:
                json.dump([t.to_dict() for t in self.tracks], f, ensure_ascii=False, indent=2)
            np.save(EMBEDDINGS_PATH, self.embeddings)

            with open(EMBEDDINGS_BIN_PATH, "wb") as f:
                f.write(self.embeddings.tobytes())

        self.guid_to_index = {t.guid.lower(): idx for idx, t in enumerate(self.tracks)}

        self.artist_counts = {}
        for t in self.tracks:
            a = (t.artist or "").strip().lower()
            if a:
                self.artist_counts[a] = self.artist_counts.get(a, 0) + 1

    def get_track_by_guid(self, guid: str) -> Optional[TrackItem]:
        idx = self.guid_to_index.get(guid.lower())
        return self.tracks[idx] if idx is not None else None

    def find_track(self, query: str) -> Optional[TrackItem]:
        q = query.lower()
        for t in self.tracks:
            if q in t.title.lower() or q in t.file_name.lower():
                return t
        return None

    def pick_next_song(
        self,
        history: List[str],
        recent_played_guids: Optional[List[str]] = None,
        temperature: float = 0.12,
        candidate_pool_size: int = 5,
        allow_same_album: bool = False
    ) -> Optional[Dict[str, Any]]:
        if not self.tracks or self.embeddings is None:
            return None

        if recent_played_guids is None:
            recent_played_guids = []

        all_played = set(g.lower() for g in (history + recent_played_guids))

        valid_history_indices = []
        for g in history:
            idx = self.guid_to_index.get(g.lower())
            if idx is not None:
                valid_history_indices.append(idx)

        played_titles = set(
            self.tracks[idx].title.strip().lower()
            for idx in valid_history_indices
            if self.tracks[idx].title
        )

        if not valid_history_indices:
            valid_candidates = [
                i for i, t in enumerate(self.tracks)
                if t.quality >= 0.6 and 90 <= t.duration_seconds() <= 360
            ]
            pick_idx = random.choice(valid_candidates) if valid_candidates else random.randrange(len(self.tracks))
            t = self.tracks[pick_idx]
            return {
                "track": t.to_dict(),
                "score": 1.0,
                "reason": "Library start"
            }

        last_track = self.tracks[valid_history_indices[-1]]
        last_artist = (last_track.artist or "").strip().lower()
        last_album = (last_track.album or "").strip().lower()
        last_track_num = last_track.track_number
        session_cluster = last_track.cluster

        # External dataset similar artists (ListenBrainz / Last.fm collaborative filtering)
        similar_artists_set = get_similar_artists(last_track.artist)

        # Context vector with exponential decay (0.70)
        decay = 0.70
        context_vec = np.zeros(self.embeddings.shape[1], dtype=np.float32)
        total_weight = 0.0

        for pos, idx in enumerate(reversed(valid_history_indices)):
            weight = math.pow(decay, pos)
            context_vec += weight * self.embeddings[idx]
            total_weight += weight
            if pos >= 5:
                break

        context_vec /= max(total_weight, 1e-6)
        context_norm = np.linalg.norm(context_vec)
        if context_norm > 1e-9:
            context_vec /= context_norm

        raw_similarities = np.dot(self.embeddings, context_vec)

        # Track consecutive plays by same artist
        consecutive_same_artist = 0
        for idx in reversed(valid_history_indices):
            if (self.tracks[idx].artist or "").strip().lower() == last_artist:
                consecutive_same_artist += 1
            else:
                break

        scores = np.copy(raw_similarities)

        for idx in range(len(self.tracks)):
            t = self.tracks[idx]
            t_guid = t.guid.lower()

            # 1. Hard recency & duplicate title exclusion
            if t_guid in all_played or (t.title and t.title.strip().lower() in played_titles):
                scores[idx] = -999.0
                continue

            dur_sec = t.duration_seconds()

            # 2. DURATION CONSTRAINT (no really short or really long songs)
            if dur_sec < 60.0 or dur_sec > 600.0:
                scores[idx] = -999.0
                continue
            if dur_sec < 90.0:
                scores[idx] -= (90.0 - dur_sec) * 0.012
            if dur_sec > 420.0:
                scores[idx] -= (dur_sec - 420.0) * 0.002

            # 3. METADATA QUALITY CONSTRAINT
            if t.quality < 0.35:
                scores[idx] = -999.0
                continue
            scores[idx] *= (0.6 + 0.4 * t.quality)

            t_artist = (t.artist or "").strip().lower()
            t_album = (t.album or "").strip().lower()

            # 4. GENRE & MUSICAL SCENE CONSTRAINT (hard anchor to musical universe)
            if session_cluster != "GENERAL":
                if t.cluster != session_cluster:
                    scores[idx] -= 0.70
                else:
                    scores[idx] += 0.25

            # 5. DATASET SIMILAR ARTIST AFFINITY (ListenBrainz / Last.fm collaborative filtering)
            if t_artist in similar_artists_set:
                scores[idx] += 0.35

            # 6. ARTIST PACING
            if t_artist and t_artist == last_artist:
                if consecutive_same_artist == 1:
                    scores[idx] += 0.08
                elif consecutive_same_artist >= 2:
                    scores[idx] -= 0.40
            else:
                if t.cluster == session_cluster:
                    scores[idx] += 0.12

            # 7. ALBUM MONOTONY BREAKER
            if t_album and t_album == last_album:
                if not allow_same_album:
                    scores[idx] -= 0.35
                    if t.track_number == last_track_num + 1:
                        scores[idx] -= 0.30

        valid_indices = np.where(scores > -100.0)[0]
        if len(valid_indices) == 0:
            unplayed = [
                i for i, t in enumerate(self.tracks)
                if t.guid.lower() not in all_played and t.quality >= 0.4 and 75 <= t.duration_seconds() <= 400
            ]
            pick_idx = random.choice(unplayed) if unplayed else random.randrange(len(self.tracks))
            return {
                "track": self.tracks[pick_idx].to_dict(),
                "score": 0.5,
                "reason": "Library fallback"
            }

        top_k = min(candidate_pool_size, len(valid_indices))
        top_indices = valid_indices[np.argpartition(scores[valid_indices], -top_k)[-top_k:]]
        top_indices = top_indices[np.argsort(-scores[top_indices])]

        top_scores = scores[top_indices]
        effective_temp = max(temperature, 0.08)
        shifted = (top_scores - np.max(top_scores)) / effective_temp
        probs = np.exp(shifted)
        probs /= np.sum(probs)

        chosen_idx = np.random.choice(top_indices, p=probs)
        chosen_track = self.tracks[chosen_idx]
        chosen_score = float(raw_similarities[chosen_idx])

        reasons = []
        if (chosen_track.artist or "").strip().lower() in similar_artists_set:
            reasons.append("dataset similar artist match")
        elif chosen_track.cluster == session_cluster and chosen_track.cluster != "GENERAL":
            reasons.append(f"{chosen_track.genre} scene")
        if chosen_track.artist and chosen_track.artist != last_track.artist:
            reasons.append(f"transition to {chosen_track.artist}")
        elif chosen_track.artist:
            reasons.append(f"by {chosen_track.artist}")
        reasons.append(f"{int(chosen_score * 100)}% match")

        return {
            "track": chosen_track.to_dict(),
            "score": chosen_score,
            "reason": ", ".join(reasons),
            "candidates": [
                {
                    "title": self.tracks[i].title,
                    "artist": self.tracks[i].artist,
                    "album": self.tracks[i].album,
                    "dur": f"{self.tracks[i].duration_seconds():.0f}s",
                    "score": round(float(scores[i]), 3),
                    "raw_sim": round(float(raw_similarities[i]), 3)
                }
                for i in top_indices[:5]
            ]
        }

    def generate_radio(
        self,
        seed_guid: str,
        count: int = 10,
        temperature: float = 0.12
    ) -> List[Dict[str, Any]]:
        seed_track = self.get_track_by_guid(seed_guid)
        if seed_track is None:
            return []

        results = []
        history = [seed_guid]
        recent = [seed_guid]

        for _ in range(count):
            pick = self.pick_next_song(
                history=history,
                recent_played_guids=recent,
                temperature=temperature
            )
            if not pick:
                break
            next_track = pick["track"]
            results.append(pick)
            history.append(next_track["guid"])
            recent.append(next_track["guid"])

        return results
