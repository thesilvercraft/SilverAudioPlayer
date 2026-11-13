import os
import sys
import json
import urllib.parse
from http.server import HTTPServer, BaseHTTPRequestHandler
from socketserver import ThreadingMixIn

sys.path.insert(0, os.path.dirname(__file__))
from engine import MusicRecommenderEngine, SLIBINDEX_DEFAULT_URL

HOST = "127.0.0.1"
PORT = 32543

engine: MusicRecommenderEngine = None


class ThreadedHTTPServer(ThreadingMixIn, HTTPServer):
    daemon_threads = True


class RecommenderHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        # Concise logging
        sys.stderr.write(f"[SilverRadio Server] {args[0]} - {args[1]}\n")

    def send_json(self, status: int, data: dict):
        body = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_OPTIONS(self):
        self.send_response(204)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()

    def do_GET(self):
        parsed = urllib.parse.urlparse(self.path)
        path = parsed.path
        query = urllib.parse.parse_qs(parsed.query)

        if path in ["/", "/health", "/api/status"]:
            self.send_json(200, {
                "status": "online",
                "tracksCount": len(engine.tracks),
                "hasEmbeddings": engine.embeddings is not None,
                "serverUrl": engine.server_url
            })
            return

        if path == "/api/recommend/next":
            # Can pass ?guid=... or ?history=guid1,guid2
            history = []
            if "history" in query:
                history.extend([g.strip() for g in query["history"][0].split(",") if g.strip()])
            if "guid" in query and query["guid"][0] not in history:
                history.append(query["guid"][0])

            temp = float(query.get("temperature", [0.35])[0])
            pick = engine.pick_next_song(history=history, temperature=temp)
            if pick:
                self.send_json(200, {"success": True, **pick})
            else:
                self.send_json(404, {"success": False, "error": "No recommendation found"})
            return

        if path == "/api/recommend/radio":
            seed = query.get("seed", [""])[0]
            count = int(query.get("count", [10])[0])
            temp = float(query.get("temperature", [0.35])[0])
            radio = engine.generate_radio(seed_guid=seed, count=count, temperature=temp)
            self.send_json(200, {"success": True, "count": len(radio), "items": radio})
            return

        self.send_json(404, {"error": f"Endpoint not found: {path}"})

    def do_POST(self):
        parsed = urllib.parse.urlparse(self.path)
        path = parsed.path

        content_length = int(self.headers.get("Content-Length", 0))
        post_data = self.rfile.read(content_length) if content_length > 0 else b"{}"

        try:
            body = json.loads(post_data.decode("utf-8")) if post_data else {}
        except Exception:
            body = {}

        if path == "/api/recommend/next":
            history = body.get("history", [])
            recent = body.get("recent", [])
            temp = float(body.get("temperature", 0.35))
            pool_size = int(body.get("poolSize", 15))
            allow_same_album = bool(body.get("allowSameAlbum", False))

            pick = engine.pick_next_song(
                history=history,
                recent_played_guids=recent,
                temperature=temp,
                candidate_pool_size=pool_size,
                allow_same_album=allow_same_album
            )
            if pick:
                self.send_json(200, {"success": True, **pick})
            else:
                self.send_json(404, {"success": False, "error": "No recommendation found"})
            return

        if path == "/api/reindex":
            engine.initialize(force_refresh=True)
            self.send_json(200, {"success": True, "tracksCount": len(engine.tracks)})
            return

        self.send_json(404, {"error": f"Endpoint not found: {path}"})


def run_server(server_url: str = SLIBINDEX_DEFAULT_URL, port: int = PORT):
    global engine
    print(f"Initializing Recommender Engine with SLibIndex ({server_url})...")
    engine = MusicRecommenderEngine(server_url=server_url)
    engine.initialize()
    print(f"Engine ready with {len(engine.tracks)} tracks.")

    server_address = (HOST, port)
    httpd = ThreadedHTTPServer(server_address, RecommenderHandler)
    print(f"SilverRadio recommendation server running at http://{HOST}:{port}")
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\nShutting down server...")
        httpd.server_close()


if __name__ == "__main__":
    url = sys.argv[1] if len(sys.argv) > 1 else SLIBINDEX_DEFAULT_URL
    p = int(sys.argv[2]) if len(sys.argv) > 2 else PORT
    run_server(url, p)
