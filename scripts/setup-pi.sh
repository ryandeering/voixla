#!/usr/bin/env bash

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VENV="$ROOT/.venv-piper"
VOICES_DIR="$ROOT/backend/voices"
CACHE_DIR="$ROOT/backend/cache"

echo "==> Voixla Pi setup (root: $ROOT)"

if ! command -v python3 >/dev/null; then
  echo "!! python3 not found. Install it:  sudo apt install -y python3 python3-venv python3-pip" >&2
  exit 1
fi

mkdir -p "$VOICES_DIR" "$CACHE_DIR"

if [[ ! -x "$VENV/bin/piper" ]]; then
  echo "==> Creating venv and installing piper-tts"
  python3 -m venv "$VENV"
  "$VENV/bin/pip" install --upgrade pip >/dev/null
  "$VENV/bin/pip" install piper-tts
fi
echo "==> piper: $VENV/bin/piper"
"$VENV/bin/piper" --help >/dev/null && echo "    piper OK"

"$ROOT/scripts/download-voices.sh" "$VOICES_DIR"

echo
echo "==> Done. Next:"
echo "    1. Point the backend at python:  Piper:PythonPath = $VENV/bin/python"
echo "       (the voixla.service unit already sets this; adjust the path if ROOT differs)"
echo "    2. Build:  cd $ROOT/frontend && npm ci && npm run build"
echo "               cd $ROOT/backend  && dotnet publish -c Release -r linux-arm64 -o ../publish"
echo "    3. Install the service: see scripts/voixla.service"
