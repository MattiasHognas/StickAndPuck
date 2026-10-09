#!/usr/bin/env bash
# Publishes the game as one self-contained executable per platform under publish/<rid>/.
# Usage: tools/publish.sh [rid ...]   (default: linux-x64 win-x64)
set -euo pipefail
cd "$(dirname "$0")/.."
rids=("$@")
[ ${#rids[@]} -eq 0 ] && rids=(linux-x64 win-x64)
for rid in "${rids[@]}"; do
    dotnet publish StickAndPuck.csproj -c Release -r "$rid" -o "publish/$rid"
    echo "publish/$rid:"; ls -lh "publish/$rid"
done
