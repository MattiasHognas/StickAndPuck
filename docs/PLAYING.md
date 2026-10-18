# Playing Stick and puck

[Back to the game](../README.md) · [Playing](PLAYING.md) · [Development](DEVELOPMENT.md) · [Testing](TESTING.md) · [Architecture](ARCHITECTURE.md)

## Download and start

Download the Linux or Windows executable from [Releases](https://github.com/MattiasHognas/StickAndPuck/releases/latest). The game bundles .NET, raylib and its assets; you do not need the SDK to play. On Linux, make the downloaded file executable with `chmod +x StickAndPuck-vX.Y.Z-linux-x64`, then run it. On Windows, run `StickAndPuck-vX.Y.Z-win-x64.exe`. Replace `vX.Y.Z` with the downloaded version.

Choose a new season, an exhibition, an arcade run or local versus from the start screen. Seasons autosave after matches; Continue resumes your run. Each club has a team morale (0–100, MOR on the standings) that rises with wins and streaks and falls with losses; a club in good spirits starts its games with some momentum and does better in simulated games. The rules page turns it off or makes it milder or stronger. English and Swedish are available in settings.

In a season, players carry their form from game to game: points, plus/minus and ice time (a goalie's save percentage) nudge every trait by up to 5 points either way. The stats page shows it as a green arrow up or a red arrow down by the name. The rules page's Season group turns it off or sets its strength (mild, normal, strong; Arcade plays strong).

## Controls

| Action | Keyboard and mouse | Gamepad |
|---|---|---|
| Skate | Arrows or WASD | Left stick or d-pad |
| Sprint | Shift | Right trigger |
| Shoot (tap wrist shot, hold slap shot) | Space or left click | A |
| Pass | E or right click | X |
| Check | Q | B |
| Deke | R | RB |
| Switch player | C | Y |
| Change line early | L | LB |
| Block (in play and in a fight) | Left Ctrl | RB |
| Accept a fight | F | Y |
| Punch (when the marker is in the green) | Space | A |
| Pause | Esc or P | Start |
| Menus: move, pick, back | Arrows or WASD, Enter or Space, Esc or Backspace | D-pad or left stick, A, B |
| Skip replay | Enter or Space | A or Start |
| Toggle camera zoom (settings: camera zoom) | F6 | |
| Swap kits (settings: swap kits) | Tab | |
| Save screenshot.png | F12 | |

In one-player scenes the keyboard and the first gamepad both control your player. In `versus` the keyboard plays home and the first gamepad plays away.

These are the default bindings; change them in the settings key-mapping page or reset them to defaults. Gamepad labels use Xbox-style names. During a faceoff, press shoot or pass after the drop. Pressing early is a faceoff violation: your centre is waved out and a winger takes the draw, and a second violation in the same faceoff loses the draw (a delay-of-game minor under NHL rules; in Arcade the first early press loses it). To arm a one-timer, press shoot after passing while the puck travels to the receiver.

## Settings and saves

The settings screen holds every switch: control switching, line changes, difficulty (easy, normal, hard), period length (2, 3, 5, 10 or 20 minutes, default 5), sim this game, auto deke, one-timers, penalties, fights, controls (keyboard or gamepad), player 2 controls (off, keyboard, gamepad 1 or 2), control marker, camera zoom, lighting mood, swap kits, benches, overall/effects/music volumes, language, GPU, and the debug overlays (fps line, match debug line, lines panel; all off by default). Opening it from the pause menu keeps the match paused. They are saved as `settings.json` in `~/.config/StickAndPuck/` on Linux or `%APPDATA%\StickAndPuck\` on Windows, next to the season save; `SAP_SETTINGS=path` moves the file.

Screenshots (F12, `SAP_SCREENSHOT`) and recordings are saved in the folder the game was started from.

The rules page provides SHL, Arcade and NHL presets with individual overrides. The intermission rule sets the Zamboni break between periods and before a playoff overtime: Standard (SHL, NHL) drives the whole lap, Quick (Arcade) half as long, Off skips it; enter, space or A skips a break. The GPU setting lists detected cards and shows the active renderer; on Linux with multiple cards, a changed GPU choice relaunches the game. Overall, Effects and Music each have a 0–10 volume control. Overall at zero mutes everything; an older saved Volume value carries over to Effects. Music plays on menus, at intermission and after the game, with no music during live play.

For environment overrides and alternate save paths, see [Development](DEVELOPMENT.md). For captures and replays, see [Testing](TESTING.md).
