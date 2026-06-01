#!/usr/bin/env python3

import argparse
import io
import json
import os
import re
import threading
import wave
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

from piper.voice import PiperVoice

VOICE_ID = re.compile(r"^[A-Za-z0-9_-]+$")


class Engine:
    def __init__(self, voices_dir):
        self.voices_dir = voices_dir
        self._voices = {}
        self._dict_lock = threading.Lock()
        self._voice_locks = {}

    def _voice_lock(self, voice):
        with self._dict_lock:
            return self._voice_locks.setdefault(voice, threading.Lock())

    def _load(self, voice):
        with self._dict_lock:
            loaded = self._voices.get(voice)
        if loaded is None:
            loaded = PiperVoice.load(os.path.join(self.voices_dir, voice + ".onnx"))
            with self._dict_lock:
                self._voices[voice] = loaded
        return loaded

    def synthesize(self, voice, text):
        with self._voice_lock(voice):
            loaded = self._load(voice)
            buffer = io.BytesIO()
            with wave.open(buffer, "wb") as wav_file:
                loaded.synthesize_wav(text, wav_file)
            return buffer.getvalue()

    def has_voice(self, voice):
        return bool(VOICE_ID.match(voice)) and os.path.exists(os.path.join(self.voices_dir, voice + ".onnx"))


engine = None


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def do_GET(self):
        if self.path == "/health":
            self._send(200, b"ok", "text/plain")
        else:
            self._send(404, b"not found", "text/plain")

    def do_POST(self):
        if self.path != "/synthesize":
            self._send(404, b"not found", "text/plain")
            return
        try:
            length = int(self.headers.get("Content-Length", 0))
            body = json.loads(self.rfile.read(length) or b"{}")
            voice = body.get("voice", "")
            text = body.get("text", "")
            if not engine.has_voice(voice):
                self._send(400, b"unknown voice", "text/plain")
                return
            if not text:
                self._send(400, b"empty text", "text/plain")
                return
            self._send(200, engine.synthesize(voice, text), "audio/wav")
        except Exception as err:  # noqa: BLE001 — report any failure to the caller
            self._send(500, str(err).encode("utf-8"), "text/plain")

    def _send(self, code, body, content_type):
        self.send_response(code)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


def main():
    global engine
    parser = argparse.ArgumentParser()
    parser.add_argument("--voices-dir", required=True)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5001)
    args = parser.parse_args()

    engine = Engine(os.path.abspath(args.voices_dir))
    server = ThreadingHTTPServer((args.host, args.port), Handler)
    print(f"piper-server listening on {args.host}:{args.port}", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
