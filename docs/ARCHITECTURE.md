# Architecture

[Back to the game](../README.md) · [Playing](PLAYING.md) · [Development](DEVELOPMENT.md) · [Testing](TESTING.md)

Stick and puck is a C# game using Raylib_cs. Simulation runs at a fixed 60 Hz; the renderer turns 3D models into a low-resolution, palette-limited image, then scales it by whole pixels.

## Repository layout

| Path | Responsibility |
| --- | --- |
| `src.slnx` | Root solution. |
| `Directory.Build.props`, `Directory.Packages.props`, `global.json` | Build rules, repository root, package versions and SDK/test runner selection. |
| `src/StickAndPuck.App/` | Game project; executable name `StickAndPuck`. |
| `tests/StickAndPuck.Tests/` | TUnit tests, world builders and shared simulation fixtures. |
| `analyzers/` | Build-time enforcement, including the ban on nullable types and null literals in app code. |
| `art/` | Blender/Aseprite sources, generation scripts and exported models, textures, UI and audio. |
| `shaders/` | Toon lighting, masks, ice and the pixel post-processing pass. |
| `data/clubs/`, `data/lang/` | Editable clubs/rosters/traits and English/Swedish string tables. |
| `tools/` | Publish, version, capture, sound-effect and music tools. |

## Match and app state

[`Core/World.cs`](../src/StickAndPuck.App/Core/World.cs) owns one match: setup, settings, players, puck, score/clock, camera, simulation time and tick, events, and typed slots for each system's state. Systems read the shared world and update the state they own. `PlayerList` invalidates the cached on-ice lists when membership changes.

`MatchSetup` describes the clubs, two input slots, period format and resolved rules. `PlayerCommand` is the common intent format for humans and AI. `RuleSet` and the settings override layer resolve SHL, Arcade or NHL rules before a match. Closed state types, `Maybe<T>` and typed events represent alternatives and absence without nullable declarations in app code.

[`Core/AppState.cs`](../src/StickAndPuck.App/Core/AppState.cs) holds what survives a match: screen flow, season/playoff and arcade sessions, the last stats sheet, gamepads, menu scripting and rendering state. `Game` owns it and passes it to successive worlds. Tests can create independent worlds and app states.

## Systems and update order

[`Core/Game.cs`](../src/StickAndPuck.App/Core/Game.cs) owns the window, fixed-step loop and draw passes. [`Core/Systems.cs`](../src/StickAndPuck.App/Core/Systems.cs) creates the ordered `ISystem` list. The order matters: input and AI prepare commands before movement and puck handling; physics resolves contacts before rules evaluate the result; HUD, audio and other presentation systems consume the resulting events.

| App folder | Responsibility |
| --- | --- |
| `Core/` | Shared types, world/app state, settings, assets, events, input snapshots, simulation clock and system composition. |
| `Players/` | Input, skating, possession, shooting/passing, checks, switching and skater rendering. |
| `AI/` | Positioning, defensive reads, special teams, tactics, decisions and difficulty. |
| `Physics/`, `Goalie/` | Puck and body collisions, nets, goalie positioning and saves. |
| `Rules/`, `Referee/` | Match flow, faceoffs, clock, offside, icing, penalties, overtime/shootouts and referee movement/signals. |
| `Lines/`, `Fights/`, `Momentum/` | Fatigue and changes, goalie replacement/pull, fights and short team boosts. |
| `Pregame/`, `Intermission/`, `Benches/` | Warm-up, Zamboni, bench and penalty-box presentation. |
| `Camera/`, `Render/`, `Arena/`, `Lighting/` | Broadcast/close-up cameras, visual goal replay, pixel pipeline, rink and lighting moods. |
| `Hud/`, `Screens/`, `Audio/` | Scorebug/messages, menus and mode flow, generated effects and streamed music. |
| `Season/`, `Versus/` | Schedule, standings, stats, saves, playoffs, arcade and local two-player setup. |
| `Replay/` | Input-log recording, replay execution, scrubbing, state dumps and checksums. |

`MatchEvents` is the current step's typed event list. The simulation publishes goals, calls, saves and other outcomes; readers react during that step. AI command ownership and merge rules live in `AI/AiCommand.cs` and the AI layers, so evaluating a candidate pass does not mutate committed player intent.

## Determinism and replays

`SimClock` converts render time into whole `Game.Step` updates. `World.Tick` counts simulation steps; `World.Time` advances with them. Captures use one step per frame, while fast-forward runs many steps per frame. Simulation uses `SimMath` where platform-dependent transcendental results would affect replay checks.

`Seeds` derives independent random streams from the match seed and each system's name. Seed zero selects the fixed fallback streams used by dev scenes. `DeviceInput` snapshots human input and supplies recorded frames during playback. A replay stores setup, settings, seed, inputs at their ticks, events and checksums; `WorldChecksum` checks simulation state while `StateDump` makes a selected tick readable.

Rendering-only changes should preserve simulation checksums. Gameplay changes may intentionally change them and require reviewed golden updates. Replay compatibility should be checked against the same revision, not assumed across releases. See [Testing: replays](TESTING.md#replays) for recording, scrubbing and take-over commands.

## Rendering and assets

The scene and per-player masks render off-screen, followed by outlines, palette snapping and ordered dithering. The normal frame is 480×270; close shots can request 240×135 or 160×90. The result is upscaled by an integer factor. Camera snapping and distance texture filtering help keep detail stable in motion.

Coordinates use metres and Y-up: X runs along the rink and +Z points toward the broadcast camera. Blender `(x, y, z)` becomes game `(x, z, -y)`. `RinkSpec` holds the shared rink dimensions. Player clips are baked as held poses and sampled as whole frames at 24 fps, without animation blending. The palette is loaded from `art/palette/stickandpuck_dusk.gpl`.

`Assets` names asset paths; `AssetRoot` finds the source/build/published asset folder while keeping captures in the launch directory. Generated art stays reproducible through `art/scripts/` and its source files. Club data and localisation stay outside code; `Core/Text.cs` looks up string-table keys.
