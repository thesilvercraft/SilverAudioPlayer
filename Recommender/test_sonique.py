import sys
import os

sys.path.insert(0, os.path.dirname(__file__))
from engine import MusicRecommenderEngine

sys.stdout.reconfigure(encoding="utf-8")

e = MusicRecommenderEngine()
e.initialize()

guid = "83dceb3c-3318-4c8f-ab15-f697f4888657"
t = e.get_track_by_guid(guid)
print(f"SEED: '{t.title}' by '{t.artist}' | Cluster: '{t.cluster}' | Tags: {t.artist_tags}")

radio = e.generate_radio(guid, count=6)
for i, item in enumerate(radio, 1):
    tk = item["track"]
    dur = tk.get("duration", 0) / 1000.0
    print(f"  {i}. '{tk['title']}' by '{tk['artist']}' | Album: '{tk['album']}' | Dur: {dur:.0f}s | Reason: {item['reason']}")
