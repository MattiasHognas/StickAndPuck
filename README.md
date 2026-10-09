# Stick and puck

A silly attempt at rendering 3D as pixel art in the form of a hockey game.
## Publish

`tools/publish.sh` builds one self-contained executable per platform: `publish/linux-x64/StickAndPuck`
and `publish/win-x64/StickAndPuck.exe`. The .NET runtime, raylib and all assets are packed inside and
unpacked on first start, so the file runs from any folder without .NET installed. Pass a runtime id to
build just one, e.g. `tools/publish.sh linux-x64`.
