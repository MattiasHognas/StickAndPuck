#!/usr/bin/env bash
# Prints the next release version from the newest vX.Y.Z tag.
# Usage: tools/next_version.sh patch|minor   (no tag yet -> v0.0.1 for patch, v0.1.0 for minor)
set -euo pipefail
bump="${1:-patch}"
last=$(git tag --list 'v*' --sort=-v:refname | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | head -n1 || true)
[ -z "$last" ] && last=v0.0.0
IFS=. read -r major minor patch <<< "${last#v}"
case "$bump" in
    patch) patch=$((patch + 1)) ;;
    minor) minor=$((minor + 1)); patch=0 ;;
    *) echo "unknown bump: $bump" >&2; exit 1 ;;
esac
echo "v$major.$minor.$patch"
