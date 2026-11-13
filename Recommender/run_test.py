import sys
import os
sys.path.insert(0, os.path.dirname(__file__))

from engine import MusicRecommenderEngine
sys.stdout.reconfigure(encoding="utf-8")

def main():
    engine = MusicRecommenderEngine()
    engine.initialize()

    seeds = ["Guess the Incident", "MEGALOVANIA", "Touch-Tone", "Pokazi"]
    for q in seeds:
        t = engine.find_track(q)
        if not t:
            continue
        print(f"\n=======================================================")
        print(f"SEED: '{t.title}' by '{t.artist}'")
        print(f"Album: '{t.album}' | Genre: '{t.genre}' | Cluster: '{t.cluster}' | Dur: {t.duration_seconds():.0f}s")
        print(f"=======================================================")
        radio = engine.generate_radio(t.guid, count=6, temperature=0.12)
        for i, item in enumerate(radio, 1):
            tk = item["track"]
            dur = tk.get("duration", 0) / 1000.0
            print(f"  {i}. '{tk['title']}' by '{tk['artist']}' | Album: '{tk['album']}' | Dur: {dur:.0f}s | Reason: {item['reason']}")

if __name__ == "__main__":
    main()
