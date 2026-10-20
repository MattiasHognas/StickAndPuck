#!/usr/bin/env bash
# Requires wasm-tools, Node 20+, Playwright's Chromium/Firefox and a desktop display (or xvfb-run).
# BROWSER_VERIFY_PREBUILT=1 skips both builds and verifies the existing desktop Release build
# (src/StickAndPuck.App/bin/Release/net11.0) and browser site (publish/browser-wasm/wwwroot), as CI does.
set -euo pipefail
cd "$(dirname "$0")/.."
if [ "${BROWSER_VERIFY_PREBUILT:-}" != 1 ]; then
    dotnet build src/StickAndPuck.App -c Release
    tools/publish.sh browser-wasm
fi
npm ci --prefix tools/browser
npm --prefix tools/browser run test:storage
npm --prefix tools/browser test
