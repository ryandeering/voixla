set -euo pipefail

VOICES_DIR="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/backend/voices}"
BASE="https://huggingface.co/rhasspy/piper-voices/resolve/main"

VOICES=(
  "en_US-lessac-medium"
  "en_US-ryan-high"
  "en_GB-alba-medium"
  "en_GB-cori-high"
  "en_GB-northern_english_male-medium"
)

mkdir -p "$VOICES_DIR"
failed=0

for key in "${VOICES[@]}"; do
  lang_code="${key%%-*}"
  family="${lang_code%%_*}"
  rest="${key#*-}"
  dataset="${rest%-*}"
  quality="${key##*-}"
  dir="$family/$lang_code/$dataset/$quality"

  echo "==> $key"
  for ext in onnx onnx.json; do
    out="$VOICES_DIR/$key.$ext"
    if [[ -f "$out" ]]; then
      echo "    exists: $key.$ext"
      continue
    fi
    url="$BASE/$dir/$key.$ext"
    if ! curl -fSL "$url" -o "$out"; then
      echo "!! Failed to download $url" >&2
      rm -f "$out"
      failed=1
    fi
  done
done

echo "==> Voices installed in: $VOICES_DIR"
ls -1 "$VOICES_DIR"/*.onnx 2>/dev/null || echo "   (none yet)"

if [[ "$failed" -ne 0 ]]; then
  echo "!! One or more voices failed to download" >&2
  exit 1
fi
