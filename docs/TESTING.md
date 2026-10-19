# Testing and captures

[Back to the game](../README.md) · [Playing](PLAYING.md) · [Development](DEVELOPMENT.md) · [Testing](TESTING.md) · [Architecture](ARCHITECTURE.md)

Before running tests, see [Formatting and static analysis](DEVELOPMENT.md#formatting-and-static-analysis) for the formatter sequence, analyzer precedence, and checks that leave files unchanged. Release builds also check CSharpier formatting, and analyzer warnings fail the build.

## Automated tests

```sh
dotnet test --project tests/StickAndPuck.Tests -c Release
```

The suite uses TUnit and Microsoft.Testing.Platform. The former in-game self-tests now live in `tests/StickAndPuck.Tests`; `SAP_SCENE` selects visual fixtures, not those automated checks. Tests run in parallel, using separate worlds and app state. `Support/WorldBuilder.cs` constructs fixtures; `Support/AllAiMatches.cs` shares the expensive all-AI match results within a run.

To run one class:

```sh
dotnet test --project tests/StickAndPuck.Tests -c Release -- --treenode-filter "/*/*/LinesTests/*"
```

Checks report PASS or FAIL; failures include the failed checks. CI runs the Release suite on every PR. Use Release for local timing too: simulation is substantially faster than Debug. `SAP_REPLAYTEST_PRINT=1` prints replacement replay goldens after a deliberate gameplay change; inspect the change before accepting new values.

## Screenshots and recordings

- Run without a screen or GPU: `xvfb-run -a env LIBGL_ALWAYS_SOFTWARE=1 SAP_SCREENSHOT=1 dotnet run --project src/StickAndPuck.App`.
- `SAP_SCREENSHOT=1`: saves screenshot.png after one second of game time and quits.
- `SAP_SCREEN_KEYS="down enter ..."`: presses scripted menu buttons in the screens (`up down left right enter back pause`, `wait`, and `shot:name` saves name.png). Default: real input.
- `SAP_RECORD=seconds`: saves every frame for that many seconds as numbered PNGs, then quits. `SAP_FRAMES` sets the folder (default `frames/`).
- `SAP_STEPS=n`: runs n fixed steps as fast as the machine can, without waiting for the screen, then quits.
- `tools/record.sh out.mp4 seconds [input.sh]`: records an mp4 headless (needs xvfb-run and ffmpeg; xdotool for input.sh). `SAP_AUDIO=1` records in real time with sound.

`dotnet test` does not need a graphical session. Visual captures do: Xvfb supplies a virtual X display and `LIBGL_ALWAYS_SOFTWARE=1` selects software rendering. On a desktop you can omit both and run the same scene on the GPU.

Use separate save paths for scripted screen runs, especially scenes that create a season:

```sh
mkdir -p /tmp/stickandpuck-preview
SAP_SETTINGS=/tmp/stickandpuck-preview/settings.json \
SAP_SAVE=/tmp/stickandpuck-preview/season.json \
SAP_ARCADE_SAVE=/tmp/stickandpuck-preview/arcade.json \
SAP_SCENE=start SAP_SCREEN=stats SAP_SCREENSHOT=1 \
dotnet run --project src/StickAndPuck.App -c Release
```

The PNG is saved in the working directory from which you launched the game. Prefix the command with `xvfb-run -a env LIBGL_ALWAYS_SOFTWARE=1` on a machine without a display. Frame recording runs one fixed simulation step per image, independently of rendering speed; encode the frames at 60 fps. `SAP_AUDIO=1 tools/record.sh ...` instead captures real-time video and audio and also needs PulseAudio.

## Visual scenes (`SAP_SCENE`)

An unknown name stops the game and prints the list of scenes. Names are not case sensitive.

- `start` (default): the start screen, and from there season, other modes, settings and the match.
- `season`: continues the saved season (or starts one) and plays your next game, then shows the table.
- `playoffs`: a playoff test setup on its own save: settles the regular season on paper and plays your next playoff game.
- `arcade`: continues the saved arcade run (or starts one) and plays your next game.
- `versus`: player vs player straight away, keyboard and mouse on the home centre and the first gamepad on the away centre.
- `versussetup`: the start screen flow opened on player vs player's team select.
- `match`: full exhibition, 3 x 20 minute periods, intro and clock, you on the home centre.
- `aimatch`: full exhibition with every skater played by the AI.
- `warmup`: full exhibition opened with the pre-game warm-up: the arena flyover (a high sweep, a close-up on the home squad, then the broadcast view; the first start press skips it) while both squads skate round their own ends until space, enter or a pad's A or start is pressed, then the opening faceoff.
- `lineup`: both clubs' starters at the opening faceoff, you on the home centre.
- `practice`: one home skater with the puck on an empty rink.
- `overtime`: a match that ends 0-0, then 3 on 3 overtime and a shootout; `SAP_PLAYOFF=1` plays the SHL playoff format instead (5 on 5 periods until a goal, no shootout), each overtime after a Zamboni break; with `SAP_START=overtime` it opens in the break before the first, and `SAP_ZAMBONI_AT` starts that drive that many seconds in.
- `penalty`: an AI exhibition where a home skater trips someone two seconds in: arm up, box, power play.
- `fight`: an AI exhibition where the two centres drop the gloves shortly after the opening drop.
- `offsidedemo`: scripted plays after each faceoff: an offside pass, a clean carry-in and an icing.
- `matchdemo`: match flow with automatic shots at each net, 25 second periods.
- `positioning`: 5 on 5 with every skater AI, stand-in shooting and passing.
- `lines`: 5 on 5 AI match where skaters tire 6x faster, so lines change every few seconds.
- `goalieswap`: match demo with 2 minute periods until a goalie is pulled for the backup.
- `referee`: match demo with the referee set for the opening drop.
- `skating`: three skaters with slow, average and fast traits race; a fourth is yours.
- `handling`: carry, pass and shoot at centre ice with teammates and one defender.
- `handlingdemo`: a carry, pass and shot that plays itself in a loop.
- `checks`: hunt down an away carrier and check them off the puck.
- `checksdemo`: a home skater checks the away carrier, over and over.
- `goalie`: the away goalie faces a loop of shots with a save/rebound/goal tally.
- `puck`: an empty rink where the puck bounces through a loop of shots.
- `camera`: both lineups and a shot into the right-hand net for the camera to follow.
- `hud`: the scorebug at 2-1 in the second period with one banner up.
- `seasontable`, `playoffbracket`, `arcadesummary`: the saved season's table, the playoff bracket or the arcade run's panel over the rink, nothing played.
- `outlines`: twelve skaters packed together to check the outlines.
- `faceoff`: repeats faceoffs, transfers the human slot to the centre, and scripts a press 0.15 seconds after each drop. `SAP_FACEOFF_VIOLATION=n` also presses n times before each drop, a faceoff violation each: with 1 the centre is waved out and a winger takes the draw; with 2 and `SAP_RULES=nhl` the second is a delay-of-game minor. `SAP_RULES=shl|arcade|nhl` picks the rules (default SHL).
- `celebration`: the last seconds of a decided game; `SAP_CLOCK` sets the remaining time (default 4), `SAP_WINNER=away` puts the visitors ahead instead.
- `goaliepull`: late in the third period, down a goal, to show the extra attacker; `SAP_AI_HUMAN=1` gives the human control and G pulls/returns the goalie in this scene.
- `specialteams`: a permanent power play, with the away side short-handed by default; `SAP_ST_KILL=home` reverses it and `SAP_ST_WAIT=1` advances to an established attacking shape, and `SAP_ST_RULE=off` plays it with the SpecialTeams rule off (even-strength positioning a man up).
- `tactics`: Frölunda against Luleå by default, with tactical choices shown; `SAP_TACTICS_HOME` and `SAP_TACTICS_AWAY` accept `balanced`, `aggressive` or `trap`, and `SAP_STYLE_HOME` and `SAP_STYLE_AWAY` accept `balanced`, `physical`, `shootFromAnywhere` or `defensiveShell`. The line at the bottom shows each club's forecheck, entry and style with its dumps, checks thrown, shots from distance, skaters deep on the forecheck and skaters in front of its own net when defending.
- `momentum`: a boost after a home goal; `SAP_MOMENTUM_SIDE=away` boosts the visitors instead.
- `shotdemo`: repeated tap, half-held and full-held shots for checking shot height and shadows; goals do not count.
- `onetimer`: repeated passes to an armed receiver, alternating a successful feed with an interception that cancels the wind-up.
- `aimoves`: AI deke drill (`SAP_MOVE=deke`, default), or an AI one-timer drill (`SAP_MOVE=onetimer`).
- `padtest`: gamepad detection and input display.
- `camera-depth`: puck movement between the far and near boards; `SAP_PUCK_Z` fixes its starting depth in metres and `SAP_PUCK_VZ` sets its depth velocity in m/s.
- `benches`: cycles through bench reactions to goals, penalties, fights, hits, stoppages and a tired line, with a close camera and draw-time logging.
- `penaltybox`: box entry, sitting and release; `SAP_BOX=in` (default), `two` or `out` chooses the script.
- `zamboni`: opens in the first intermission; `SAP_ZAMBONI_AT` starts the drive that many seconds in. The corner tag shows the next period, the time left and the skip button; the rules' intermission length (Standard, Quick in Arcade, or Off) sets how long it lasts.
- `netlobby`: the start screen flow opened on the online versus lobby.
- `nethost`: the lobby's host flow with `SAP_HOME` and `SAP_AWAY` picked, listening on `SAP_NET_PORT` until a player joins, then the network match.
- `netjoin`: the lobby's join flow, connecting to `SAP_NET_ADDRESS` on `SAP_NET_PORT`, then the network match.

`lineup` is a static starter layout rather than the timed faceoff flow; use `referee` or `faceoff` to inspect the crouch and drop. Scene implementations are discovered automatically through `IScene`; most live in their system's `Dev/` folder. General match and rendering overrides, including `SAP_TACTICS_LINE` and `SAP_ST_SPOTS`, are in [Development](DEVELOPMENT.md#visuals-sound-and-input).

### Penalty fixture variables

| Variable | Values and effect |
| --- | --- |
| `SAP_PENALTY_OT` | `1` starts `SAP_SCENE=penalty` in 3-on-3 overtime, so the call gives a 4-on-3 power play. Unset or `0` uses regulation. |
| `SAP_PENALTY_MAJOR` | `1` changes the penalty scene's scripted tripping minor into boarding with a major and game misconduct. The skater is ejected and the line refills; `SAP_LINES_DEBUG=1` shows the units. Unset or `0` keeps the minor. |
| `SAP_PENALTY_SKIP` | A positive number of seconds to simulate before rendering in `penalty` or `goaliepull`. Unset, zero or negative means no skip; useful for capturing the box or extra attacker. |

### Network play on one machine

Run two game processes on localhost with separate settings and save paths: one with `SAP_SCENE=nethost`, the other with `SAP_SCENE=netjoin` (same `SAP_NET_PORT`). `SAP_HUMAN=0` on a side lets the AI play its club while inputs still go through lockstep, and `SAP_WARMUP=0` skips the warm-up. `SAP_CHECKSUMS=file` on both writes every step's checksum; the match's lines must be identical. Both processes record a replay that verifies with `SAP_REPLAY=file SAP_SCREENSHOT=1`. `SAP_NET_DESYNC=step` on one side nudges its skaters after that step, so both sides show the out-of-sync message at the next checksum exchange.

The `NetworkTests` class runs two lockstep peers over a real loopback TCP connection with scripted inputs and checks every step's checksum; it also checks that a divergence on one side is caught.

### Variable coverage

The reference covers all 90 current `SAP_*` names found in the app, tests and recording tools: 88 in app code, `SAP_REPLAYTEST_PRINT` in the test harness, and `SAP_AUDIO` in `tools/record.sh`. The GPU relaunch variables are internal and documented separately in [Development](DEVELOPMENT.md#internal-gpu-relaunch-variables).

`SAP_MATCHES` appears only in an old comment in `DefenceTests.cs`; no current code reads it. It is not a supported override for the TUnit suite.

## Screen previews (`SAP_SCREEN`)

Use these with `SAP_SCENE=start` (the default scene). `SAP_LEAGUE=nhl` loads the 32 NHL clubs instead of the SHL's for the whole run, so `season`, `playoffs`, `standings` and `bracket` show an NHL season (82 games, NHL rules, division standings, 16-club playoffs); unset or `shl` keeps the SHL. For example `SAP_LEAGUE=nhl SAP_SAVE=/tmp/nhl.json SAP_SCREEN=bracket` settles an NHL season and shows its bracket, and the same save with `SAP_SCREEN=standings` shows its final standings. Values are case-insensitive; an unknown value stops the game. This is the complete set accepted by `Screens/ScreenScenes.cs`:

| Value | Opens |
| --- | --- |
| `start` | Start screen; also the default when unset. |
| `settings` | Settings, including controls, rules, language, GPU and volumes. |
| `modes` | Game mode selection. |
| `teams` | Exhibition team selection, club strengths and rules choice. |
| `pause` | A match paused with the pause menu open. |
| `results` | A sample 4–3 overtime result with long player names and a goal list. |
| `standings` | The saved season's table; starts a season if none exists. With `SAP_LEAGUE=nhl`, the NHL standings: the four divisions side by side under their conferences, with the playoff spots as they stand (1-3 and WC1/WC2). |
| `bracket` | The playoff test season's bracket. With `SAP_LEAGUE=nhl`, the NHL bracket (its own test save, `playoffs-test-nhl.json`): 16 clubs by division and wild card, first round on the outside, final in the middle. |
| `leaders` | Alias of `stats`. |
| `stats` | Season statistics and player of the game from a fresh test season with 12 user games simulated. |
| `table` | The standings of a fresh test season with 20 user games simulated, played by the `SAP_RULES` preset's table points and tiebreak (SHL 3-2-1-0, NHL 2-2-1-0). |
| `sim` | A test season with five user games simulated, then “sim this game”. |
| `trophy` | The season champion after a test season and playoffs. |
| `arcadetrophy` | The trophy for a completed three-lives arcade run. |
| `bestruns` | The arcade best-run board with sample finished runs. |

`SAP_STATS_VIEW` picks the initial stats list: `POINTS`, `GOALS`, `ASSISTS`, `HITS`, `GOALIES`, `STARS` or `YOUR CLUB` in English (translated labels and string keys also work). Unknown names leave the default points view. The `stats`, `sim` and trophy fixtures use named test saves beside the normal save paths; they regenerate their sample data. Use the isolated paths above to keep preview files together.

## Replays

- Every match started from the menus is recorded to `replays/` beside the settings file (`~/.config/StickAndPuck/replays/` on Linux, `%APPDATA%\StickAndPuck\replays\` on Windows); the newest 50 are kept. `SAP_REPLAY_OUT=file` records to that file instead, and also records a `SAP_SCENE` match; `SAP_REPLAY_OUT=0` records nothing.
- `SAP_REPLAY=file`: plays a replay back: the same clubs, rules, settings and seed, and every key, button and stick move on the step it happened.
- `SAP_AT=2:12:30` (period 2 with 12:30 on the clock; `2:750` in seconds or `t3600` for a step also work): runs to that moment without drawing, prints the state (`STATE` lines: score, clock, puck, every player) and saves `replay_at.png`, then plays on from there. With `SAP_SCREENSHOT=1` it quits after the picture.
- `SAP_TAKEOVER=1` with `SAP_AT`: from that moment your keyboard and pads play, and a new replay (`name_takeover.sapreplay`) records the match from the start.
- `SAP_REPLAY=file SAP_SCREENSHOT=1`: replays the whole match headless as fast as it can and exits 0 when the score, every event and the world checksums match the recording, 1 when they do not.
- `SAP_CHECKSUMS=file`: writes "step checksum" for every step of the recorded or replayed match, to compare two machines line by line.

Use the same game revision for recording and replay validation. A seed alone does not describe human input: keep the replay file. The automatic goal replay camera uses a short visual buffer; `.sapreplay` files record inputs for deterministic resimulation.

A full replay check is the validation command above without `SAP_AT`. Scrubbing with `SAP_AT` stops early, so the current end-of-replay comparison may report a different final score/event count even when all checksums up to that tick agree.


## Browser verification

Install the `wasm-tools` workload, Node 20 or newer, and the browser dependencies once:

```sh
dotnet workload install wasm-tools
npm ci --prefix tools/browser
(cd tools/browser && npx playwright install --with-deps chromium firefox)
```

Run `tools/verify-browser.sh` with a working desktop display, or `xvfb-run -a tools/verify-browser.sh` on a headless Linux machine. The script builds desktop Release, publishes a fresh trimmed browser site, checks storage failure behavior, and tests Chromium and Firefox. `BROWSERS=chromium` selects one installed engine; `SITE_ROOT` can point to another published `wwwroot`.

Web Audio verification also needs a working audio output. On a headless Ubuntu runner, install `pulseaudio`, start it with `pulseaudio --start --exit-idle-time=-1`, then run `pactl load-module module-null-sink sink_name=browser_test` and `pactl set-default-sink browser_test`. This provides an audio device while retaining the check that the game produces nonzero PCM samples.

The browser suite checks actual menu navigation and saved settings, season and arcade reloads, native asset/animation/font/music loading, nonzero Web Audio PCM after Start, keyboard movement, mouse input, and simulated standard gamepad connect/buttons/axes/disconnect. Physical controller/driver behavior still needs hardware testing. It exercises replay downloads during a running recording, uploads, invalid-file rejection and verification without changing saves.

Two desktop fixtures (1,800 AI-match steps and 6,000 match-demo steps, with separate seeds) are recorded through the real game loop. Each browser must match all 7,800 per-step checksums, events and final results. A browser input recording must also verify on desktop. The suite writes screenshots, console logs, replay fixtures, checksum logs and `results.json` under `TestResults/browser/`.

Performance reports include observed drawing FPS, simulation Hz, clamped frame counts, and mean/p95/max callback work over a three-second practice sample. Headless software rendering is a reference measurement, not a hardware GPU benchmark. The suite checks that simulation steps match 60 Hz over the frame time accepted by `SimClock`, including its existing 250 ms catch-up limit for stalls. CI runs the suite on a virtual display with a virtual audio output and uploads the deployable `wwwroot` plus reports as artifacts.

Browser verification also advances the skating fixture in 1 ms callbacks and checks that the WebGL canvas changes while the simulation state remains identical. This exercises interpolation through the real scene and mask passes; 30/60/120/144 Hz scheduling and discontinuities are covered by the .NET tests.

The browser suite also resets WebGL’s generic vertex colour to black before drawing the practice scene and checks actual framebuffer pixels for bright ice. This catches uncoloured arena meshes accidentally inheriting context state instead of explicitly using white.

For Electron/VS Code GPU regressions, serve the published site and run:

```bash
npm --prefix tools/browser run test:electron -- /path/to/electron http://localhost:8000
```

This opens a temporary test window using Electron's normal GPU selection. It checks framebuffer ice colours, high-precision vertex transforms in all 3D passes, correctly disabled colour writes in depth-only passes, and ice stability during actual camera panning. Results include the GPU renderer in `TestResults/browser/electron-gpu.json`. Run this on real hardware as well as the software-rendered Chromium/Firefox suite; software drivers can promote mediump arithmetic and hide precision errors.
