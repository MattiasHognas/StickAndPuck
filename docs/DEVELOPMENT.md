# Development

[Back to the game](../README.md) · [Playing](PLAYING.md) · [Development](DEVELOPMENT.md) · [Testing](TESTING.md) · [Architecture](ARCHITECTURE.md)

## Build and run

Run commands from the repository root. Install a .NET SDK compatible with [global.json](../global.json), currently `11.0.100-rc.2.26504.105` (prerelease allowed, `latestFeature` roll-forward). The solution targets .NET 11 and C# 15. A graphical session with OpenGL is needed to play; [Testing](TESTING.md) covers running under Xvfb.

```sh
dotnet build
dotnet run --project src/StickAndPuck.App
```

For a Release build, add `-c Release`. The executable is still named `StickAndPuck`; the project is `src/StickAndPuck.App/StickAndPuck.App.csproj`. The root `Directory.Build.props` defines `RepoRoot` for asset paths, and `Directory.Packages.props` holds package versions.

Development overrides are environment variables in front of the command, for example `SAP_SCENE=match SAP_HOME=frolunda SAP_AWAY=djurgarden dotnet run --project src/StickAndPuck.App`. On Windows PowerShell: `$env:SAP_SCENE="match"; dotnet run --project src/StickAndPuck.App`.

Environment variables override saved settings for the run. `SAP_SETTINGS=path` chooses a different settings JSON file; leaving it unset uses the platform default. They also work with the downloaded release executable. See [Playing](PLAYING.md) for settings and save locations, and [Testing](TESTING.md#visual-scenes-sap_scene) for every scene.

## Formatting and static analysis

CSharpier owns C# and XML whitespace formatting. Analyzer conflicts follow this
precedence: **CSharpier > Roslyn (IDE/CA) > Sonar > Meziantou**. The
[`.editorconfig`](../.editorconfig) documents confirmed overlaps and disables the
lower-priority rule; unrelated rules retain their defaults. Analyzer diagnostics
are promoted to warnings, and `Directory.Build.props` treats warnings as errors
and enforces code style during builds.

Restore the pinned local tools, apply code-style and analyzer fixes, then format:

```sh
dotnet tool restore
dotnet build analyzers/StickAndPuck.Analyzers/StickAndPuck.Analyzers.csproj
dotnet format style src.slnx
dotnet format analyzers src.slnx
dotnet csharpier format .
```

Build the analyzer project first on a fresh checkout. `dotnet format` does not
build project-referenced source generators; without the discovery generator,
unused-type analysis can incorrectly flag modes referenced by the generated
registry. The default Debug build supplies the generator used by these commands.

Use the `style` and `analyzers` subcommands instead of plain `dotnet format`.
The plain command also runs Roslyn's whitespace formatter, which can change
CSharpier's output even though IDE0055 is disabled. CSharpier does not offer a
switch to disable its whitespace formatting. Review automated code fixes for
behavior changes before committing.

After the tool restoration and analyzer build above, check without applying changes:

```sh
dotnet format style src.slnx --verify-no-changes
dotnet format analyzers src.slnx --verify-no-changes
dotnet csharpier check .
```

The CI formatting job runs these checks on pull requests and releases, using the
pinned SDK and restored local tools. Publishing requires this job to pass.

The matching `CSharpier.MsBuild` package formats project sources during Debug
builds and checks formatting during Release builds. Install the recommended
CSharpier VS Code extension to use the workspace's format-on-save settings;
builds and the CLI work without the extension. Keep the tool and package versions
in sync when upgrading.

## Match settings

- `SAP_HOME`, `SAP_AWAY`: club ids, one of `bjorkloven brynas djurgarden farjestad frolunda hv71 linkoping lulea malmo orebro rogle skelleftea timra vaxjo`, or an NHL club id from data/clubs/nhl/ (for example `oilers`, `maple_leafs`). Default: the first two clubs alphabetically.
- `SAP_CLUB`: your club id for a new season, playoff or arcade run. Default: `SAP_HOME`, else the first club.
- `SAP_PERIOD`: seconds per period. Default: the settings screen's period length in `start`, `season`, `playoffs`, `arcade` and `versussetup`; 1200 in `match`, `aimatch`, `versus`, `positioning`, `lines`, `penalty` and `fight`; 25 in `matchdemo`; 120 in `goalieswap`; 5 in `overtime`.
- `SAP_OT`: seconds of an overtime period in the `overtime`, `season`, `playoffs` and `arcade` scenes. Default 15 in `overtime`, the normal 5 minutes (20 in `playoffs`) elsewhere.
- `SAP_START`: `overtime` or `shootout` skips straight there in the `overtime` scene (`overtime` with `SAP_PLAYOFF=1` opens in the Zamboni break before it). Default: play regulation first.
- `SAP_HUMAN`: `1` gives you the home club in the `overtime` scene (default off there); `0` lets the AI play both clubs in `start`, `season`, `playoffs`, `arcade`, `versus` and `versussetup` (default: you play).
- `SAP_AI_HUMAN=1`: you control the home centre in `aimatch`, `positioning`, `lines`, `penalty` and `fight`. Default off (all AI).
- `SAP_SEASON=new`: starts a new season over the save in `season` and `playoffs`. Default: continue the save.
- `SAP_ARCADE=new`: starts a new arcade run over the save. Default: continue the save.
- `SAP_ARCADE_RULE`: `lives` (default) or `suddendeath`, the rule for a new arcade run.
- `SAP_SEED`: integer; fixes the season schedule or the arcade opponent order, and is the match seed: every system draws its dice from its own stream of it, so the same seed and the same inputs play the same match on every machine. Default: random for a season or arcade and for a match started from the menus (printed on the "screens: match" line and kept in its replay), fixed test seeds for a scene.
- `SAP_SAVE`, `SAP_ARCADE_SAVE`: paths for the season and arcade save files. Default: `season.json` and `arcade.json` in the [settings folder](PLAYING.md#settings-and-saves).
- `SAP_VERSUS_P2`: `keyboard`, `pad1` or `pad2`, player two's device in `versus`. Default: the settings screen's player 2 controls (the first gamepad), or the keyboard when player one holds that device; OFF hands the visitors to the AI. Both players never get the same device.
- `SAP_IQ_HOME`, `SAP_IQ_AWAY`: 0 to 100, sets every skater's hockey IQ for that club in `aimatch`. Default: each player's own value.
- `SAP_GOALIE_REFLEXES`: 0 to 100, overrides the goalie's reflexes in the `goalie` scene. Default: the goalie's own value.
- `SAP_FATIGUE`: how many times faster than normal skaters tire and rest. Default 1 (6 in `lines` and `goalieswap`).
- `SAP_PENALTY_STRICT`: scales how often the referee calls penalties in the `penalty` scene. Default 1.
- `SAP_FIGHT_CHANCE`: 0 to 1, the chance a hard check drops the gloves in the `fight` scene. Default 0.4.
- `SAP_HUD_MESSAGE`: the banner in the `hud` scene: `goal` (default), `period`, `final`, `tie`, `offside`, `icing`, `intro` or `none`.
- `SAP_WARMUP`: `0` skips the pre-game warm-up in every match you play; a number of seconds starts the game by itself after that long. Default: warm up until start is pressed.
- `SAP_FLYOVER`: `0` leaves out the arena flyover at the start of the warm-up (the warm-up keeps its plain close-up). Default: the flyover plays, and the first start press skips it.
- `SAP_REPLAY_AT`: seconds; fakes a goal at that time so the replay plays (used with the `camera` scene). Default off.

- `SAP_NET_ADDRESS`, `SAP_NET_PORT`, `SAP_NET_DELAY`: the online versus lobby's starting host address to join (default `127.0.0.1`), TCP port (default 7777) and the host's input delay in steps (1 to 15, default 4). The lobby screen changes them for the run. Desktop only.

- `SAP_LEAGUE=shl|nhl`: the league whose clubs the run loads; `nhl` plays the season, playoff and screen scenes as an NHL season (82 games by division and conference, NHL rules, the 16-club division playoff). Default `shl`.
- `SAP_RULES=shl|arcade|nhl`: chooses the rule preset in the menu flow and the overtime and faceoff scenes without saving that override; arcade modes retain their Arcade preset. Individual dev scenes can set up their own rules.

## Visuals, sound and input

- `SAP_LIGHTING`: `early`, `afternoon` or `late` fixes the arena lighting mood. Default: the settings screen's lighting (random each match).
- `SAP_CROWD_POSE`: `sit`, `stand`, `cheer`, `wave` or `boo` puts every section of the crowd in that pose for the whole run, to preview the crowd frames. Default: seated, as the match sets it.
- `SAP_DITHER`: `0` turns off the ordered dithering at toon band edges and palette snaps, for before and after comparisons. Default: on.
- `SAP_DIFFICULTY`: `easy`, `normal` or `hard`. Default: the settings screen's difficulty (normal).
- `SAP_CONTROL_MARKER`, `SAP_SIM_GAME`, `SAP_AUTO_DEKE`, `SAP_ONE_TIMERS`: `0` or `1`, override the matching settings screen switches (defaults on, on, off, on).
- `SAP_SWAP_KITS=1`: the clubs wear each other's kits. Default: the settings screen's swap kits (off). Tab toggles it while playing.
- `SAP_BENCHES=0`: empty benches; `1` shows both clubs' players off the ice on their benches, watching the puck and standing up for changes. Default: the settings screen's benches (on).
- `SAP_FPS=1`: fps and key help line top left. Default: the settings screen's fps line (off).
- `SAP_CAMERA_ZOOM`: `1` camera zooms with speed, `0` fixed zoom. Default: the settings screen's value (off). F6 toggles it while playing.
- `SAP_MATCH_DEBUG=1`: plain score and clock line at the top. Default: the settings screen's match debug line (off; on in the demo scenes).
- `SAP_LINES_DEBUG=1`: energy panel showing each unit on the ice. Default: the settings screen's lines panel (off; on in `lines` and `goalieswap`).
- `SAP_AI_TARGETS=1`: markers where each AI skater is heading in `positioning`. Default off.
- `SAP_SFX=demo`: plays every event's sound in turn. Default off.

- `SAP_LANGUAGE=english|swedish`: overrides the saved language.
- `SAP_GPU`: a GPU id or part of its name, such as `nvidia` or `amd`; an unknown value falls back to the default card. Automatic GPU relaunch is supported on Linux when more than one card is found.
- `SAP_PAD_MAPPINGS=0`: disables the game's extra gamepad mappings for detection troubleshooting; normally leave it unset. Use `SAP_SCENE=padtest` to inspect a pad.
- `SAP_RUMBLE=0`: disables gamepad rumble.
- `SAP_AI_TRACE=1`: logs AI command owners and reasons for debugging.
- `SAP_TACTICS_LINE=1`: shows the tactics debug line. Off by default; the `tactics` scene enables it itself.
- `SAP_ST_SPOTS=1`: draws the special-teams target spots on the ice. Off by default; match debug can also show them.

For scene-specific fixtures, menu scripts, recording and replay variables, see [Testing](TESTING.md).

### Internal GPU relaunch variables

These are set by the game, rather than player-facing options:

- `SAP_GPU_LAUNCHED`: the id of the card selected for the relaunched process; used to recognise the running card and avoid repeated relaunches.
- `SAP_GPU_REVERT`: the previous card's id while a new choice awaits confirmation. The game opens the settings screen to ask whether to keep it, then clears this value when confirmed.

## Art and data

Club names, colours, rosters and traits are editable files in [data/clubs](../data/clubs/README.md). English and Swedish strings live in `data/lang/`. Asset paths are centralised in `Core/Assets.cs`.

Models, the rink and stepped animation clips are generated with Blender scripts under `art/scripts/`; `.blend` sources and exported `.glb` files live under `art/`. Aseprite sources and Lua/Python scripts produce the pixel atlases, kits and UI. Keep changes in their scripts and source assets so exports can be regenerated. Run only one Blender job at a time.

The entry scripts describe their expected inputs: `build_player.py`, `anim.py`, `build_scene.py`, `export_arena.py`, `make_kits.py` and `make_ui.py`.

The crowd is drawn by the game from sprite frames, not baked into `arena.glb`: `export_arena.py` writes one seat per spectator to `art/scene/crowd_seats.json`, `build_crowd.py` (Blender) renders the sit, stand, cheer, wave and boo frames as flat part ids, and `crowd_atlas.py` (python3 with Pillow) paints them in dusk palette colours into `art/crowd/crowd_256.png`, with a shirt mask the game tints in the home club's colours, plus a contact sheet in `art/crowd/renders/`. NES-style effects come from `tools/sfx/sfx.py` into `art/audio/effects/`; music comes from `tools/music/music.py` into `art/audio/music/`.

## Building a release

- `tools/publish.sh` builds self-contained single executables in `publish/linux-x64/StickAndPuck` and `publish/win-x64/StickAndPuck.exe`, with .NET, raylib and all assets packed inside.
- `tools/publish.sh linux-x64` builds just one platform. `VERSION=0.1.2 tools/publish.sh` stamps a version.
- Every merge to main builds, tags and publishes a GitHub Release automatically; `[milestone]` in the squash title (or a `milestone` PR label) bumps the minor version instead of the patch. Pull requests run both the build/publish check and the Release test suite.
- Releases also attach `StickAndPuck-vX.Y.Z-browser-wasm.zip`, containing the verified browser site with `index.html` at the ZIP root. Browser and desktop builds share the selected release version; extract the ZIP and serve its contents over HTTP. The browser site and verification reports remain available as Actions artifacts.

The .NET runtime, raylib and all assets are packed inside and unpacked on first start, so the executable runs from any folder. The two output paths are `publish/linux-x64/StickAndPuck` and `publish/win-x64/StickAndPuck.exe`.

### Browser build and storage

With the .NET SDK in `global.json` and the `wasm-tools` workload installed:

```sh
tools/publish.sh browser-wasm
python -m http.server 8000 --directory publish/browser-wasm/wwwroot
```

Open `http://localhost:8000`, wait for loading, then click Start game. The browser launcher opens the start menu; `?scene=practice` runs one controllable skater with the puck and `?scene=skating` opens the movement fixture. The launcher and fingerprinted runtime resource manifest are generated/copied by the WebAssembly SDK on each publish. Serve the entire `wwwroot` folder over HTTP.

JavaScript owns `requestAnimationFrame`. `BrowserHost.Start` initializes a `Game.Session` synchronously in the Start click so Web Audio can initialize during a user gesture; `Frame(timestamp)` advances the existing fixed 60 Hz accumulator and draws once; `Stop` releases resources. A resumed background tab catches up at most 0.25 seconds. Desktop `Game.Run` drives the same session in its existing loop. Browser frames avoid raylib 6.0's `WindowShouldClose`, which calls `emscripten_sleep`.

Drawing samples the previous and current fixed-step transforms using the accumulator remainder, so 120/144 Hz displays can show movement between simulation ticks. Players, goalies, the puck and shadow, control markers, referee and pixel-snapped camera share that fraction in both scene and outline passes. Sampling never changes world state or replay checksums. Teleports, scene/phase changes, replay cuts, pause and render-scale changes snap to current state; screenshots, frame captures and fast verification also draw the exact current state. This interpolation adds up to one tick (16.7 ms) of visual delay. The authored held animation poses retain their 24 fps cadence.

The bundled raylib ES2 renderer uses WebGL 1 and GLSL ES 1.00 variants in `shaders/web`. `ResourceLoader` selects those shaders and extracts filename-only assets into the runtime's shared Emscripten filesystem; GLBs, animation clips, fonts and music require no separate resource download list. Language tables also load from bundled resources. `ModelPose` temporarily corrects raylib 6.0's animated normal upload buffer index in browser builds. All 3D browser passes use high-precision vertex transforms, including unlit meshes and masks: raylib's default ES2 vertex shader uses mediump, which causes depth errors and flicker on hardware with actual 16-bit mediump arithmetic. `ColorWrite` passes scalar bytes for browser depth-only colour masks, avoiding the CBool struct interop that produced mixed masks. Revisit these workarounds when upgrading raylib. Arena draws also restore the white generic vertex colour for uncoloured WebGL meshes: this value belongs to the context rather than the VAO, and raylib initializes it only during upload on its VAO path.

GPU detection/relaunch, GPU selection and native rumble are skipped in the browser. Keyboard taps are queued until a simulation step consumes them; mouse clicks retain one step even when both pointer events fall between animation frames. Standard browser gamepads use raylib's browser Gamepad API mapping, without loading desktop SDL mappings.

`SavedFiles` keeps settings, seasons and arcade progress in synchronous `localStorage` entries under `stickandpuck:v1:/saves/`. Saves are independent of the build's asset extraction folder, available immediately after reload, and isolated by site origin. Storage failures appear beside the canvas; an unsuccessful write leaves the previous saved value intact. Desktop saves still use atomic file replacement. Replays cannot write or delete progress.

The launcher offers Stop, replay upload, playback, download and verification. Download during a match produces a snapshot while recording continues. Replay files live in the runtime filesystem during the page session; download them to keep them after closing the page. Upload validates the gzip format/version before replacing an existing file. Verify runs fixed steps in batches with audio unopened, reports the result/events/checksums, and retains every step's checksum for diagnostics. Normal Start/Play initializes audio in the click handler.

Browser publishing enables trimming. `DiscoveryGenerator` generates direct constructor registrations for all concrete `IScene` and `IGameMode` implementations, including inherited arcade modes; runtime type scanning and `Activator` are no longer needed. `DiscoveryTests` compares the registry with all source implementations in the desktop test build.

Simulation interpolation and stick-blade rotation use explicit fused rounding and `SimMath` trigonometry so desktop and WASM agree. This corrects small platform-dependent differences; older recordings can consequently report checksum differences on the corrected simulation. The replay file format remains compatible.
