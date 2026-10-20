#!/usr/bin/env bash
# Publishes desktop executables or a browser site under publish/<rid>/ (browser site: wwwroot/).
# Usage: tools/publish.sh [rid ...]   (default: linux-x64 win-x64 osx-arm64)
# Set VERSION=0.1.2 to stamp that version into the executable (the release workflow does).
# Windows publishes a GUI exe (no console window); macOS also gets publish/osx-*/StickAndPuck.app (tools/macos_app.py).
# Desktop RIDs are restored once and published in parallel; each RID's output is printed when it finishes.
# Publishing skips the Roslyn analyzers and the CSharpier check (the build, test and formatting checks run them;
# source generators still run, so the binaries are the same). PUBLISH_CHECKS=1 runs them anyway.
set -euo pipefail
cd "$(dirname "$0")/.."
project=src/StickAndPuck.App/StickAndPuck.App.csproj
rids=("$@")
[ ${#rids[@]} -eq 0 ] && rids=(linux-x64 win-x64 osx-arm64)
options=(-c Release ${VERSION:+-p:Version=$VERSION})
[ "${PUBLISH_CHECKS:-}" = 1 ] || options+=(-p:RunAnalyzers=false -p:CSharpier_Bypass=true)

browser_staging=""
logs=""
trap 'rm -rf -- ${browser_staging:+"$browser_staging"} ${logs:+"$logs"}' EXIT

desktop=()
for rid in "${rids[@]}"; do
    [ "$rid" = browser-wasm ] || desktop+=("$rid")
done

if [ ${#desktop[@]} -gt 0 ]; then
    # One restore for every desktop RID, and the analyzer/generator project built once up front, so the
    # parallel publishes share no build step (each RID has its own obj/ and bin/ folder).
    dotnet restore "$project" -p:RuntimeIdentifiers=\""$(IFS=';'; echo "${desktop[*]}")"\"
    dotnet build analyzers/StickAndPuck.Analyzers/StickAndPuck.Analyzers.csproj --no-restore "${options[@]}"
    logs=$(mktemp -d)
    pids=()
    for rid in "${desktop[@]}"; do
        dotnet publish "$project" -r "$rid" -o "publish/$rid" --no-restore -p:BuildProjectReferences=false "${options[@]}" \
            > "$logs/$rid.log" 2>&1 &
        pids+=($!)
    done
    failed=()
    for i in "${!desktop[@]}"; do
        rid=${desktop[$i]}
        status=0
        wait "${pids[$i]}" || status=$?
        echo "===== dotnet publish $rid (exit $status) ====="
        cat "$logs/$rid.log"
        [ "$status" -eq 0 ] || failed+=("$rid")
    done
    if [ ${#failed[@]} -gt 0 ]; then
        echo "Publish failed for: ${failed[*]}" >&2
        exit 1
    fi
    for rid in "${desktop[@]}"; do
        if [[ "$rid" == osx-* ]]; then
            python3 -I tools/macos_app.py "publish/$rid" "${VERSION:-0.0.0}"
        fi
        echo "publish/$rid:"; ls -lh "publish/$rid"
    done
fi

for rid in "${rids[@]}"; do
    [ "$rid" = browser-wasm ] || continue
    mkdir -p publish
    browser_staging=$(mktemp -d "$PWD/publish/.browser-wasm.XXXXXX")
    dotnet publish "$project" -r "$rid" -o "$browser_staging" "${options[@]}"
    # Fingerprinted resources from earlier builds must not accumulate in the deployable site.
    rm -rf -- publish/browser-wasm
    mv -- "$browser_staging" publish/browser-wasm
    browser_staging=""
    echo "Browser site: publish/browser-wasm/wwwroot"
    du -sh publish/browser-wasm/wwwroot
done
