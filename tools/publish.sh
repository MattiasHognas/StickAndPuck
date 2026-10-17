#!/usr/bin/env bash
# Publishes desktop executables or a browser site under publish/<rid>/ (browser site: wwwroot/).
# Usage: tools/publish.sh [rid ...]   (default: linux-x64 win-x64 osx-arm64)
# Set VERSION=0.1.2 to stamp that version into the executable (the release workflow does).
set -euo pipefail
cd "$(dirname "$0")/.."
rids=("$@")
[ ${#rids[@]} -eq 0 ] && rids=(linux-x64 win-x64 osx-arm64)
browser_staging=""
trap 'if [ -n "$browser_staging" ]; then rm -rf -- "$browser_staging"; fi' EXIT
for rid in "${rids[@]}"; do
    if [ "$rid" = browser-wasm ]; then
        mkdir -p publish
        browser_staging=$(mktemp -d "$PWD/publish/.browser-wasm.XXXXXX")
        dotnet publish src/StickAndPuck.App/StickAndPuck.App.csproj -c Release -r "$rid" -o "$browser_staging" ${VERSION:+-p:Version=$VERSION}
        # Fingerprinted resources from earlier builds must not accumulate in the deployable site.
        rm -rf -- publish/browser-wasm
        mv -- "$browser_staging" publish/browser-wasm
        browser_staging=""
        echo "Browser site: publish/browser-wasm/wwwroot"
        du -sh publish/browser-wasm/wwwroot
        continue
    fi
    dotnet publish src/StickAndPuck.App/StickAndPuck.App.csproj -c Release -r "$rid" -o "publish/$rid" ${VERSION:+-p:Version=$VERSION}
    echo "publish/$rid:"; ls -lh "publish/$rid"
done
