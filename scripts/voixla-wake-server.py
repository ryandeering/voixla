#!/usr/bin/env python3
import http.server
import subprocess

CONTAINER = "voixla"


class WakeHandler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        subprocess.Popen(["docker", "start", CONTAINER],
                         stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        self.send_response(200)
        self.send_header("Content-Type", "text/plain")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(b"OK")

    def log_message(self, *args):
        pass


http.server.HTTPServer(("127.0.0.1", 8083), WakeHandler).serve_forever()
