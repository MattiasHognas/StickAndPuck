# Stick and puck

A silly attempt at rendering 3D as pixel art in the form of a hockey game. Pick a club, chase the puck, and try to stay out of the box.

![Frölunda and Djurgården playing on the pixel-art rink](docs/images/match.png)

## Drop the puck

**[Download the latest release](https://github.com/MattiasHognas/StickAndPuck/releases/latest)** for Windows, Linux, Mac or the browser. The game includes .NET and its assets—no SDK needed.

- **Windows:** run `StickAndPuck-vX.Y.Z-win-x64.exe`.
- **Linux:** make the downloaded file executable with `chmod +x StickAndPuck-vX.Y.Z-linux-x64`, then run it.
- **Mac (Apple silicon):** unzip `StickAndPuck-vX.Y.Z-osx-arm64.zip` and open `StickAndPuck.app`. The app is not signed by Apple, so the first time right-click it and pick Open, or allow it under System Settings → Privacy & Security → Open Anyway.
- **Browser:** unzip `StickAndPuck-vX.Y.Z-browser-wasm.zip` and serve the folder over HTTP.

(Replace `vX.Y.Z` with the version you downloaded.)

Pick a league, the SHL (14 clubs, a 52-game season) or the NHL (32 clubs, an 82-game season by division and conference), then start an exhibition, take a club through a season and playoffs for the Le Mat trophy or the Stanley Cup, or try arcade mode with one loss or three lives. Seasons autosave after matches. Menus are available in English and Swedish.

## One or two players

- **Single player:** play an exhibition, a season or an arcade run against the computer.
- **Local versus:** two people share one computer, one on the keyboard and one on a gamepad (or two gamepads).
- **Online versus:** two people play over the network, one hosting and one joining by address (desktop only). See [Playing: online versus](docs/PLAYING.md#online-versus).

![Choose clubs and rules before an exhibition](docs/images/teams.png)

## Get moving

| Action | Keyboard / mouse | Gamepad |
| --- | --- | --- |
| Skate / sprint | WASD or arrows / Shift | Left stick or d-pad / RT |
| Shoot | Space or left click; hold to load up | A |
| Pass | E or right click | X |
| Check / deke | Q / R | B / RB |
| Switch player / change line | C / L | Y / LB |
| Pause | Esc or P | Start |

Press shoot or pass **after the puck drops** to win a faceoff. Pressing early is a violation: your centre is waved out and a winger takes the draw. Rebind controls, choose difficulty and rules, or adjust sound in settings.

![Both centres crouched over the faceoff dot](docs/images/faceoff.png)

## Behind the boards

- [Playing](docs/PLAYING.md): full controls, settings and saves.
- [Development](docs/DEVELOPMENT.md): build/run commands, environment variables, art and releases.
- [Testing](docs/TESTING.md): automated tests, every visual scene and screen preview, screenshots and replays.
- [Architecture](docs/ARCHITECTURE.md): folders, systems, world state and the pixel pipeline.
