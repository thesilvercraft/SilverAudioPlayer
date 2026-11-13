import glob
import json
import os
import sys
from collections import defaultdict, Counter

sys.stdout.reconfigure(encoding="utf-8")

LB_DIR = sys.argv[1] if len(sys.argv) > 1 else os.environ.get("LISTENBRAINZ_EXPORT_DIR")
if not LB_DIR:
    print("Usage: ingest_listenbrainz.py <listenbrainz_export_dir> (or set LISTENBRAINZ_EXPORT_DIR)", file=sys.stderr)
    sys.exit(1)
CACHE_DIR = os.path.join(os.environ.get("LOCALAPPDATA", os.path.expanduser("~")), "SLibIndexRecommender")
os.makedirs(CACHE_DIR, exist_ok=True)

OUT_PROFILE_PATH = os.path.join(CACHE_DIR, "user_profile.json")
OUT_FEEDBACK_PATH = os.path.join(CACHE_DIR, "user_feedback.json")

print(f"Reading ListenBrainz export from: {LB_DIR}")

# 1. Parse feedback.jsonl
loved_mbids = set()
disliked_mbids = set()
fb_path = os.path.join(LB_DIR, "feedback.jsonl")
if os.path.exists(fb_path):
    with open(fb_path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            d = json.loads(line)
            score = d.get("score", 0)
            mbid = d.get("recording_mbid")
            if mbid:
                if score > 0:
                    loved_mbids.add(mbid.lower())
                elif score < 0:
                    disliked_mbids.add(mbid.lower())

print(f"Loaded {len(loved_mbids)} loved MBIDs and {len(disliked_mbids)} disliked MBIDs.")

# 2. Parse all listens
listen_files = sorted(glob.glob(os.path.join(LB_DIR, "listens", "*", "*.jsonl")))
listens = []
loved_titles = set()
loved_artists = Counter()
disliked_titles = set()
disliked_artists = Counter()
artist_play_counts = Counter()
track_play_counts = Counter()

for lf in listen_files:
    with open(lf, "r", encoding="utf-8") as fp:
        for line in fp:
            line = line.strip()
            if not line:
                continue
            d = json.loads(line)
            tm = d.get("track_metadata", {})
            artist = (tm.get("artist_name") or "").strip()
            title = (tm.get("track_name") or "").strip()
            ts = float(d.get("listened_at") or d.get("inserted_at") or 0.0)

            mb_map = tm.get("mbid_mapping") or {}
            mbid = (mb_map.get("recording_mbid") or "").lower()

            if artist and title:
                a_lower = artist.lower()
                t_lower = title.lower()
                listens.append((ts, a_lower, t_lower, mbid))
                artist_play_counts[a_lower] += 1
                track_play_counts[f"{a_lower} - {t_lower}"] += 1

                if mbid in loved_mbids:
                    loved_titles.add(t_lower)
                    loved_artists[a_lower] += 1
                elif mbid in disliked_mbids:
                    disliked_titles.add(t_lower)
                    disliked_artists[a_lower] += 1

listens.sort(key=lambda x: x[0])
print(f"Total valid listens processed: {len(listens)}")

# 3. Extract artist transitions & session co-occurrences
artist_transitions = defaultdict(lambda: defaultdict(int))
session_cooccurrences = defaultdict(lambda: defaultdict(int))

# Within-session threshold: 40 minutes (2400 seconds)
for i in range(len(listens) - 1):
    ts1, a1, t1, _ = listens[i]
    ts2, a2, t2, _ = listens[i + 1]
    diff = ts2 - ts1
    if 0 < diff <= 2400:
        if a1 != a2:
            artist_transitions[a1][a2] += 1

# Sliding window of 5 songs for co-occurrences
window = 5
for i in range(len(listens)):
    ts_i, a_i, _, _ = listens[i]
    for j in range(i + 1, min(i + window + 1, len(listens))):
        ts_j, a_j, _, _ = listens[j]
        if 0 < (ts_j - ts_i) <= 1800 and a_i != a_j:
            session_cooccurrences[a_i][a_j] += 1
            session_cooccurrences[a_j][a_i] += 1

# Convert defaultdicts to regular dicts for JSON
serializable_transitions = {
    k: dict(v) for k, v in artist_transitions.items() if len(v) > 0
}
serializable_cooccurrences = {
    k: dict(v) for k, v in session_cooccurrences.items() if len(v) > 0
}

user_profile = {
    "loved_mbids": list(loved_mbids),
    "disliked_mbids": list(disliked_mbids),
    "loved_titles": list(loved_titles),
    "loved_artists": dict(loved_artists),
    "disliked_titles": list(disliked_titles),
    "disliked_artists": dict(disliked_artists),
    "top_artists": dict(artist_play_counts.most_common(100)),
    "artist_transitions": serializable_transitions,
    "session_cooccurrences": serializable_cooccurrences,
}

with open(OUT_PROFILE_PATH, "w", encoding="utf-8") as f:
    json.dump(user_profile, f, indent=2, ensure_ascii=False)

print(f"User profile written to {OUT_PROFILE_PATH} ({os.path.getsize(OUT_PROFILE_PATH) / 1024:.1f} KB)")

# Also initialize user_feedback.json if not present
if not os.path.exists(OUT_FEEDBACK_PATH):
    initial_feedback = {
        "liked_guids": [],
        "disliked_guids": [],
        "liked_artists": [a for a, c in loved_artists.most_common(50)],
        "disliked_artists": [a for a, c in disliked_artists.most_common(50)],
        "events": [],
    }
    with open(OUT_FEEDBACK_PATH, "w", encoding="utf-8") as f:
        json.dump(initial_feedback, f, indent=2, ensure_ascii=False)
    print(f"Initialized {OUT_FEEDBACK_PATH}")
