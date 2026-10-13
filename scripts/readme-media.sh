#!/usr/bin/env bash
# Screenshots and the jump GIF for README → docs/media/.
# The game plays itself (ReadmeMedia: autopilot, English UI, 1920×1080), ffmpeg makes the GIF from the recorded frames.
# Needs Unity 6000.6.5f1, the imported original levels (Stickman Jumper → Import Placeholders) and ffmpeg.
set -euo pipefail
cd "$(dirname "$0")/.."

UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app/Contents/MacOS/Unity}"
RAW=Builds/readme-media
DOCS=docs/media
mkdir -p Builds/logs "$DOCS"

"$UNITY" -batchmode -projectPath src -executeMethod StickmanJumper.EditorTools.ReadmeMedia.Run \
  -mediaOut "$PWD/$RAW" -logFile Builds/logs/readme-media.log
cat "$RAW/media-summary.txt"

# Stills: 1600 px wide JPEG keeps README light.
for f in "$RAW"/*.png; do
  ffmpeg -loglevel error -y -i "$f" -vf "scale=1600:-1:flags=lanczos" -q:v 3 "$DOCS/$(basename "${f%.png}").jpg"
done

# The jump GIF: walking back, the run-up and take-off, then from just before the canopy to the summary — the long fall
# through empty sky in between is cut (frame numbers from ReadmeMedia's marks.txt). 25 fps recording → 12 fps, a bit
# faster, 640 px, one palette from all frames (so the small green buttons keep their colour).
eval "$(tr ' ' '\n' < "$RAW/frames/marks.txt")"
A0=0; A1=$(( jump + 50 ))
B0=$(( canopy - 25 > A1 ? canopy - 25 : A1 )); B1=$end
ffmpeg -loglevel error -y -framerate 25 -i "$RAW/frames/%04d.jpg" \
  -vf "select='between(n\,$A0\,$A1)+between(n\,$B0\,$B1)',setpts=N/(25*1.3)/TB,fps=12,scale=640:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=full[p];[b][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle" \
  "$DOCS/jump.gif"

ls -lh "$DOCS"
