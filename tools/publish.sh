#!/usr/bin/env bash
# Publishes the game as one self-contained executable per platform under publish/<rid>/.
# Usage: tools/publish.sh [rid ...]   (default: linux-x64 win-x64)
# Set VERSION=0.1.2 to stamp that version into the executable (the release workflow does).
set -euo pipefail
cd "$(dirname "$0")/.."
rids=("$@")
[ ${#rids[@]} -eq 0 ] && rids=(linux-x64 win-x64)
for rid in "${rids[@]}"; do
    dotnet publish src/StickAndPuck.App/StickAndPuck.App.csproj -c Release -r "$rid" -o "publish/$rid" ${VERSION:+-p:Version=$VERSION}
    echo "publish/$rid:"; ls -lh "publish/$rid"
done
