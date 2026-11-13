import json
import urllib.request
import urllib.parse
import sys
import os
import tempfile
from concurrent.futures import ThreadPoolExecutor, as_completed

sys.stdout.reconfigure(encoding="utf-8")

CACHE_DIR = os.path.join(os.environ.get("LOCALAPPDATA", os.path.expanduser("~")), "SLibIndexRecommender")
os.makedirs(CACHE_DIR, exist_ok=True)
KB_PATH = os.path.join(CACHE_DIR, "artist_knowledge.json")

LASTFM_API_KEY = "b25b959554ed76058ac220b7b2e0a026"

TRACKS_PATH = sys.argv[1] if len(sys.argv) > 1 else os.environ.get(
    "SLIBINDEX_TRACKS_JSON", os.path.join(tempfile.gettempdir(), "tracks_full.json")
)
with open(TRACKS_PATH, "r", encoding="utf-8") as f:
    tracks = json.load(f)

# Load existing KB if present
kb = {}
if os.path.exists(KB_PATH):
    try:
        with open(KB_PATH, "r", encoding="utf-8") as f:
            kb = json.load(f)
        print(f"Loaded {len(kb)} existing artists from knowledge base.")
    except Exception:
        kb = {}

# Also add artists that already have genres from library
for t in tracks:
    a = (t.get("artist") or "").strip()
    g = (t.get("genre") or "").strip()
    if a and g and g.lower() not in ("unknown", "other", "general"):
        if a not in kb:
            kb[a] = {"tags": [g], "source": "slibindex"}

# Collect artists that still need enrichment
all_artists = set()
for t in tracks:
    a = (t.get("artist") or "").strip()
    if a and a not in ("????", "Unknown") and a.lower() != (t.get("title") or "").strip().lower():
        all_artists.add(a)

needed = [a for a in all_artists if a not in kb or not kb[a].get("tags")]
print(f"Total artists needing enrichment from dataset: {len(needed)}")

def clean_artist_for_search(name: str) -> str:
    # Remove 'feat.', 'vs.', etc. for better search match
    for sep in [" feat. ", " ft. ", " featuring ", " vs. ", " & ", " / "]:
        if sep in name:
            return name.split(sep)[0].strip()
    return name

def fetch_artist_info(artist_name: str):
    search_name = clean_artist_for_search(artist_name)
    encoded = urllib.parse.quote(search_name)
    url = f"http://ws.audioscrobbler.com/2.0/?method=artist.getinfo&artist={encoded}&api_key={LASTFM_API_KEY}&format=json"
    req = urllib.request.Request(url, headers={"User-Agent": "SilverAudioPlayer/1.0"})
    try:
        with urllib.request.urlopen(req, timeout=6) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            artist_data = data.get("artist", {})
            tags = [t["name"].lower() for t in artist_data.get("tags", {}).get("tag", []) if t.get("name")]
            similar = [s["name"] for s in artist_data.get("similar", {}).get("artist", []) if s.get("name")]
            bio = artist_data.get("bio", {}).get("summary", "")
            return artist_name, {"tags": tags, "similar": similar[:8], "source": "lastfm"}
    except Exception:
        return artist_name, None

# Fetch in parallel with 8 worker threads
success_count = 0
with ThreadPoolExecutor(max_workers=8) as executor:
    futures = {executor.submit(fetch_artist_info, a): a for a in needed}
    for future in as_completed(futures):
        a_name, res = future.result()
        if res and res.get("tags"):
            kb[a_name] = res
            success_count += 1
            if success_count % 25 == 0:
                print(f"Enriched {success_count}/{len(needed)} artists...")

print(f"Successfully enriched {success_count} artists with global dataset tags!")

# Save to disk
with open(KB_PATH, "w", encoding="utf-8") as f:
    json.dump(kb, f, ensure_ascii=False, indent=2)
print(f"Saved {len(kb)} artists to {KB_PATH}.")
