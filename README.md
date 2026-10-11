# Stick and puck

A silly attempt at rendering 3D as pixel art in the form of a hockey game.

## Running the game

### Running

- `dotnet run` from the repo root (needs the .NET 10 SDK) builds and starts the game on the start screen.
- Settings are environment variables in front of the command, for example `SAP_SCENE=match SAP_HOME=frolunda SAP_AWAY=djurgarden dotnet run`. On Windows PowerShell: `$env:SAP_SCENE="match"; dotnet run`.
- Release builds: download from the GitHub Releases page. `StickAndPuck-vX.Y.Z-linux-x64` needs `chmod +x` before it runs; `StickAndPuck-vX.Y.Z-win-x64.exe` runs as is. Neither needs .NET installed, and both read the same `SAP_*` variables.
- The settings screen sets the period length (2, 3, 5, 10 or 20 minutes, default 5), controls (keyboard or gamepad), camera zoom, penalties, fights and volume. They are saved as `settings.json` in `~/.config/StickAndPuck/` on Linux or `%APPDATA%\StickAndPuck\` on Windows, next to the season save; `SAP_SETTINGS=path` moves the file.
- Screenshots (F12, `SAP_SCREENSHOT`) and recordings are saved in the folder the game was started from.

### Scenes (`SAP_SCENE`)

An unknown name stops the game and prints the list of scenes. Names are not case sensitive.

- `start` (default): the start screen, and from there season, other modes, settings and the match.
- `season`: continues the saved season (or starts one) and plays your next game, then shows the table.
- `playoffs`: a playoff test setup on its own save: settles the regular season on paper and plays your next playoff game.
- `arcade`: continues the saved arcade run (or starts one) and plays your next game.
- `versus`: player vs player straight away, keyboard and mouse on the home centre and the first gamepad on the away centre.
- `versussetup`: the start screen flow opened on player vs player's team select.
- `match`: full exhibition, 3 x 20 minute periods, intro and clock, you on the home centre.
- `aimatch`: full exhibition with every skater played by the AI.
- `lineup`: both clubs' starters at the opening faceoff, you on the home centre.
- `practice`: one home skater with the puck on an empty rink.
- `overtime`: a match that ends 0-0, then 3 on 3 overtime and a shootout; `SAP_PLAYOFF=1` plays the SHL playoff format instead (5 on 5 periods until a goal, no shootout).
- `penalty`: an AI exhibition where a home skater trips someone two seconds in: arm up, box, power play.
- `fight`: an AI exhibition where the two centres drop the gloves a quarter second after the drop.
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

### Match settings

- `SAP_HOME`, `SAP_AWAY`: club ids, one of `bjorkloven brynas djurgarden farjestad frolunda hv71 linkoping lulea malmo orebro rogle skelleftea timra vaxjo`. Default: the first two clubs alphabetically.
- `SAP_CLUB`: your club id for a new season, playoff or arcade run. Default: `SAP_HOME`, else the first club.
- `SAP_PERIOD`: seconds per period. Default: the settings screen's period length in `start`, `season`, `playoffs`, `arcade` and `versussetup`; 1200 in `match`, `aimatch`, `versus`, `positioning`, `lines`, `penalty` and `fight`; 25 in `matchdemo`; 120 in `goalieswap`; 5 in `overtime`.
- `SAP_OT`: seconds of an overtime period in the `overtime`, `season`, `playoffs` and `arcade` scenes. Default 15 in `overtime`, the normal 5 minutes (20 in `playoffs`) elsewhere.
- `SAP_START`: `overtime` or `shootout` skips straight there in the `overtime` scene. Default: play regulation first.
- `SAP_HUMAN`: `1` gives you the home club in the `overtime` scene (default off there); `0` lets the AI play both clubs in `start`, `season`, `playoffs`, `arcade`, `versus` and `versussetup` (default: you play).
- `SAP_AI_HUMAN=1`: you control the home centre in `aimatch`, `positioning`, `lines`, `penalty` and `fight`. Default off (all AI).
- `SAP_SEASON=new`: starts a new season over the save in `season` and `playoffs`. Default: continue the save.
- `SAP_ARCADE=new`: starts a new arcade run over the save. Default: continue the save.
- `SAP_ARCADE_RULE`: `lives` (default) or `suddendeath`, the rule for a new arcade run.
- `SAP_SEED`: integer; fixes the season schedule or the arcade opponent order. Default: random.
- `SAP_SAVE`, `SAP_ARCADE_SAVE`: paths for the season and arcade save files. Default: `season.json` and `arcade.json` in the settings folder above.
- `SAP_VERSUS_P2`: `keyboard`, `pad1` or `pad2`, player two's device in `versus`. Default: the first gamepad, or the keyboard when player one holds the pad. Both players never get the same device.
- `SAP_SMART_HOME`, `SAP_SMART_AWAY`: 0 to 100, sets every skater's smart passes trait for that club in `aimatch`. Default: each player's own value.
- `SAP_GOALIE_REFLEXES`: 0 to 100, overrides the goalie's reflexes in the `goalie` scene. Default: the goalie's own value.
- `SAP_FATIGUE`: how many times faster than normal skaters tire and rest. Default 1 (6 in `lines` and `goalieswap`).
- `SAP_PENALTY_STRICT`: scales how often the referee calls penalties in the `penalty` scene. Default 1.
- `SAP_FIGHT_CHANCE`: 0 to 1, the chance a hard check drops the gloves in the `fight` scene. Default 0.4.
- `SAP_HUD_MESSAGE`: the banner in the `hud` scene: `goal` (default), `period`, `final`, `tie`, `offside`, `icing`, `intro` or `none`.
- `SAP_SCREEN`: opens one screen straight away in the `start` scene: `settings`, `modes`, `teams`, `pause`, `results`, `standings` or `bracket`. Default: the start screen.
- `SAP_REPLAY_AT`: seconds; fakes a goal at that time so the replay plays (used with the `camera` scene). Default off.

### Visuals and sound

- `SAP_LIGHTING`: `early`, `afternoon` or `late` fixes the arena lighting mood. Default: random each match.
- `SAP_CAMERA_ZOOM`: `1` camera zooms with speed, `0` fixed zoom. Default: the settings screen's value (off). F6 toggles it while playing.
- `SAP_MATCH_DEBUG=1`: plain score and clock line at the top. Default off (on in the demo scenes).
- `SAP_LINES_DEBUG=1`: energy panel showing each unit on the ice. Default off (on in `lines` and `goalieswap`).
- `SAP_AI_TARGETS=1`: markers where each AI skater is heading in `positioning`. Default off.
- `SAP_SFX=demo`: plays every event's sound in turn. Default off.

### Controls

| Action | Keyboard and mouse | Gamepad |
|---|---|---|
| Skate | Arrows or WASD | Left stick or d-pad |
| Sprint | Shift | Right trigger |
| Shoot (tap wrist shot, hold slap shot) | Space or left click | A |
| Pass | E or right click | X |
| Check | Q | B |
| Switch player | C | Y |
| Change line early | L | LB |
| Block (in play and in a fight) | Left Ctrl | RB |
| Accept a fight | F | Y |
| Punch (when the marker is in the green) | Space | A |
| Pause | Esc or P | Start |
| Menus: move, pick, back | Arrows or WASD, Enter or Space, Esc or Backspace | D-pad or left stick, A, B |
| Skip replay | Enter or Space | A or Start |
| Toggle camera zoom | F6 | |
| Swap kits (home/away) | Tab | |
| Save screenshot.png | F12 | |

In one-player scenes the keyboard and the first gamepad both control your player. In `versus` the keyboard plays home and the first gamepad plays away.

### Headless runs and tests

- Run without a screen or GPU: `xvfb-run -a env LIBGL_ALWAYS_SOFTWARE=1 SAP_SCREENSHOT=1 dotnet run`.
- `SAP_SCREENSHOT=1`: saves screenshot.png after one second of game time and quits.
- `SAP_SCREEN_KEYS="down enter ..."`: presses scripted menu buttons in the screens (`up down left right enter back pause`, `wait`, and `shot:name` saves name.png). Default: real input.
- `SAP_RECORD=seconds`: saves every frame for that many seconds as numbered PNGs, then quits. `SAP_FRAMES` sets the folder (default `frames/`).
- `tools/record.sh out.mp4 seconds [input.sh]`: records an mp4 headless (needs xvfb-run and ffmpeg; xdotool for input.sh). `SAP_AUDIO=1` records in real time with sound.
- Test scenes run with no window, print one line per check and exit 0 when all pass, 1 otherwise: `pucktest`, `handlingtest`, `checkstest`, `goalietest`, `matchtest`, `offsidetest`, `overtimetest`, `refereetest`, `positioningtest`, `aimatchtest`, `linestest`, `lightingtest`, `hudtest`, `penaltytest`, `fighttest`, `seasontest`, `playofftest`, `arcadetest`, `versustest`. Example: `SAP_SCENE=matchtest dotnet run`.

### Building a release

- `tools/publish.sh` builds self-contained single executables in `publish/linux-x64/StickAndPuck` and `publish/win-x64/StickAndPuck.exe`, with .NET, raylib and all assets packed inside.
- `tools/publish.sh linux-x64` builds just one platform. `VERSION=0.1.2 tools/publish.sh` stamps a version.
- Every merge to main builds, tags and publishes a GitHub Release automatically; `[milestone]` in the squash title (or a `milestone` PR label) bumps the minor version instead of the patch. Pull requests only get a build check.

## Publish

`tools/publish.sh` builds one self-contained executable per platform: `publish/linux-x64/StickAndPuck`
and `publish/win-x64/StickAndPuck.exe`. The .NET runtime, raylib and all assets are packed inside and
unpacked on first start, so the file runs from any folder without .NET installed. Pass a runtime id to
build just one, e.g. `tools/publish.sh linux-x64`.
