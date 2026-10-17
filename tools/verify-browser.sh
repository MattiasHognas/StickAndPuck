#!/usr/bin/env bash
# Requires wasm-tools, Node 20+, Playwright's Chromium/Firefox and a desktop display (or xvfb-run).
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet build src/StickAndPuck.App -c Release
tools/publish.sh browser-wasm
npm ci --prefix tools/browser
npm --prefix tools/browser run test:storage
npm --prefix tools/browser test
