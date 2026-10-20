# Playing Stick and puck

[Back to the game](../README.md) · [Playing](PLAYING.md) · [Development](DEVELOPMENT.md) · [Testing](TESTING.md) · [Architecture](ARCHITECTURE.md)

## Download and start

Download the Linux or Windows executable from [Releases](https://github.com/MattiasHognas/StickAndPuck/releases/latest). The game bundles .NET, raylib and its assets; you do not need the SDK to play. On Linux, make the downloaded file executable with `chmod +x StickAndPuck-vX.Y.Z-linux-x64`, then run it. On Windows, run `StickAndPuck-vX.Y.Z-win-x64.exe`. Replace `vX.Y.Z` with the downloaded version.

Choose a new season, an exhibition, an arcade run, local versus or online versus from the start screen. Seasons autosave after matches; Continue resumes your run. Each club has a team morale (0–100, MOR on the standings) that rises with wins and streaks and falls with losses; a club in good spirits starts its games with some momentum and does better in simulated games. The rules page turns it off or makes it milder or stronger. Every few games the dressing room may bring news: a flu that weakens a few players, a rookie called up, a star in a contract dispute, a travel delay that starts you tired or a fan night that lifts morale; the rules page turns news off or makes it rarer or more frequent. Before a season game played from the dressing room the coach gives a pre-game talk: keep calm (steadier passing, fewer penalties), fire them up (a momentum head start and more hits, but more penalties) or demand discipline (far fewer penalties, a little less bite); the coach also passes on a scouting tip about the opponent (its forecheck, entries, style, power play, top scorer or where it is stronger or weaker than you). The rules page turns the talk or the tips off or sets the talk's strength. English and Swedish are available in settings.

## Online versus

Online versus plays one person on each of two computers (desktop only; the browser build has no network play). Both need the same game version and club files. In NEW GAME, pick ONLINE VERSUS:

- The host picks HOST A MATCH, then their club (they play home) and the opponent's club, and waits. The waiting screen shows the port and this computer's address. The host's rules, period length and gameplay settings are used for the match.
- The other player types the host's address and port on the lobby's ADDRESS and PORT rows (digits and dots; backspace erases), then picks JOIN A MATCH.

The default port is 7777 (TCP); the host must accept connections on it. INPUT DELAY (default 4 steps, 67 ms) sets how far ahead each press is sent; raise it on a slow connection if the "waiting for the opponent" banner shows often. Each player uses their own controls and key bindings. A network match cannot be paused: press Esc, P or Start twice to leave it. If the two computers ever disagree about the match, it ends with an "out of sync" message; it also ends when the opponent leaves or the connection is lost.

In a season, players carry their form from game to game: points, plus/minus and ice time (a goalie's save percentage) nudge every trait by up to 5 points either way. The stats page shows it as a green arrow up or a red arrow down by the name. The rules page's Season group turns it off or sets its strength (mild, normal, strong; Arcade plays strong).

Players also develop with age over a season. Young players (23 or younger) who play gain trait points, faster the younger they are, the more ice time they get and the better their form; veterans (31 or older) slowly lose some, slower while in form. A season adds up to 5 points or takes up to 4 on every trait. The stats page shows it as a green "+2" or a red "-1" after the name, and the dressing room news tells you when one of your players levels up or slows down. PLAYER DEVELOPMENT in the Season group turns it off or sets its strength (the NHL's longer season develops mildly; Arcade not at all).

Pick the SHL or the NHL with LEAGUE on the start screen or on team select. The league decides the clubs you choose from, the season and arcade run that Continue resumes, and the rule preset (picking a league selects its preset; you can still change the rules after). Each league has its own season and arcade save: the SHL keeps `season.json` and `arcade.json`, so saves from earlier versions continue as SHL saves, and the NHL uses `season_nhl.json` and `arcade_nhl.json` beside them. Arcade runs always play the Arcade rules. The NHL standings show the four divisions side by side; left and right switch to the wild card view, each conference's division leaders beside its wild card race with the cut line under the second wild card. The SHL champion lifts the Le Mat trophy, the NHL champion the Stanley Cup after the Stanley Cup final.

Hard checks, blocked slap shots and fights can injure a skater (goalies are spared). He plays the game out, then misses his club's next 1 to 6 games: the players behind him move up the lines and the fourth line fills the gap. Games simulated on paper injure a player now and then too. The dressing room lists your club's injured players and the games they still miss. The rules page's Season group turns injuries off or sets how often they happen (rare, normal, frequent; Arcade has none).

A second fight in one game, or a match penalty (a game misconduct, or a major and game misconduct: every fight under SHL rules, a dangerous hit), suspends the player for his club's next 1 to 3 games: one game, two for a dangerous hit or for two offences in the same game. He is left out of the lines until he has sat them out, and the dressing room lists him as suspended. The rules page's Season group turns suspensions off or sets their length (mild takes a game off, strong adds one; Arcade has none).

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
| Change line early (press again to rush the change) | L | LB |
| Block (in play and in a fight) | Left Ctrl | RB |
| Accept a fight | F | Y |
| Punch (when the marker is in the green) | Space | A |
| Time-out (at a stoppage, once a game; also in the pause menu) | T | LT |
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

The rules page provides SHL, Arcade and NHL presets with individual overrides. The intermission rule sets the Zamboni break between periods and before a playoff overtime: Standard (SHL, NHL) drives the whole lap, Quick (Arcade) half as long, Off skips it; enter, space or A skips a break. The time-out rule (on in SHL and NHL, off in Arcade) gives each club one 30 second time-out a game: call it at a stoppage with T or LT, or from the pause menu, and the skaters on the ice get their legs back; the AI calls its own late in a close game when its line on the ice is tired, above all after icing the puck. Under SHL and NHL rules a puck lifted over the glass leaves the rink and stops play, and shooting or clearing it straight over the glass from your own zone is a delay-of-game minor (not a deflection); in Arcade the netting keeps it in play. Pressing change line again while your change on the fly is under way rushes it: the fresh skaters jump on at once while the tired ones still skate for the bench, and if either plays the puck while a man going off is still more than a few metres from the bench it is too many men on the ice, a bench minor (Lenient in SHL and NHL; Strict calls it from five feet, Off in Arcade). The AI never rushes a change. A trip, hook or slash from behind on a skater on a clean breakaway (past the last defender in the attacking half, only the goalie to beat) is a penalty shot instead of a minor under SHL and NHL rules: he takes it alone from centre against the goalie, and play resumes with a faceoff at centre, goal or not (off in Arcade). The GPU setting lists detected cards and shows the active renderer; on Linux with multiple cards, a changed GPU choice relaunches the game. Overall, Effects and Music each have a 0–10 volume control. Overall at zero mutes everything; an older saved Volume value carries over to Effects. Music plays on menus, at intermission and after the game, with no music during live play.

For environment overrides and alternate save paths, see [Development](DEVELOPMENT.md). For captures and replays, see [Testing](TESTING.md).
