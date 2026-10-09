#!/usr/bin/env bash
# Records a short movie of the game, also on a machine with no screen or GPU (xvfb + software GL).
#
#   tools/record.sh out.mp4 seconds [input.sh]
#   SAP_SCENE=skating tools/record.sh skating.mp4 12 keys.sh
#
# The usual SAP_ variables pick the scene and clubs. Every saved frame is one 60 Hz step
# (SAP_RECORD in Game.cs), so motion is smooth however slow the renderer is; frames are the
# game's 3x whole-pixel upscale (1440x810). input.sh, if given, runs alongside the game and can
# hold keys with xdotool ("xdotool keydown Right; sleep 3; xdotool keyup Right"); its sleeps are
# wall-clock, so key timing is approximate.
#
# SAP_AUDIO=1 records in real time instead, with sound from a null PulseAudio sink: for audio
# work, where a choppy frame rate matters less than hearing the game.
#
# Needs: dotnet, xvfb-run, ffmpeg; xdotool for input; pulseaudio for SAP_AUDIO.
set -euo pipefail
out=$(realpath -m "$1"); seconds=$2; input=${3:+$(realpath "$3")}
cd "$(dirname "$0")/.."
dotnet build -c Release -v q -nologo >/dev/null
bin=$(ls -d bin/Release/net*/StickAndPuck | head -1)
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
export LIBGL_ALWAYS_SOFTWARE=1 DOTNET_ROOT=${DOTNET_ROOT:-$(dirname "$(realpath "$(command -v dotnet)")")}

cat >"$work/run.sh" <<EOF
set -e
sleep 0.5
if [ -n "$input" ]; then (sleep 4; bash "$input") & fi
if [ "${SAP_AUDIO:-}" = 1 ]; then
  pulseaudio -D --exit-idle-time=-1 --system=false -n --load=module-null-sink --load=module-native-protocol-unix
  sleep 1
  "$bin" >"$work/game.log" 2>&1 &
  sleep 4
  ffmpeg -y -loglevel error -f x11grab -draw_mouse 0 -framerate 30 -video_size 1440x810 -i \$DISPLAY+0,0 \
    -f pulse -i null.monitor -t $seconds -c:v libx264 -crf 14 -pix_fmt yuv420p -c:a aac -b:a 160k "$out"
  kill %% 2>/dev/null || true; pulseaudio -k || true
else
  SAP_RECORD=$seconds SAP_FRAMES="$work/frames" "$bin" >"$work/game.log" 2>&1
fi
EOF
xvfb-run -a -s "-screen 0 1440x810x24" bash "$work/run.sh"
if [ "${SAP_AUDIO:-}" != 1 ]; then
  ffmpeg -y -loglevel error -framerate 60 -pattern_type glob -i "$work/frames/*.png" \
    -c:v libx264 -crf 14 -preset slow -pix_fmt yuv420p -movflags +faststart "$out"
fi
grep '^SAP:' "$work/game.log" || true
echo "saved $out"
