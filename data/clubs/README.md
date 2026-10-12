# Club files

One JSON file per club. The game loads every `*.json` here; `SAP_HOME` and `SAP_AWAY` pick clubs by `id`, otherwise the first two by id play.

- `id`: file-safe name, also used for kit atlases (`art/kits/{id}_{home|away}_{skater|goalie}_64.png`).
- `name`, `shortName`: full name and the club's SHL code for the scorebug (three letters, four for HV71 and SAIK).
- `home`, `away`: kit colours as hex (`primary` jersey, `secondary` stripes and numbers, `accent` trim, `helmet`). The visiting club wears `away`. These are the club's real colours; the kit script snaps them to the palette.
- `roster`: the match squad in line order: two goalies (starter first), then four forward lines and three defence pairs as C, LW, RW, D, D per line (line 4 has no D). The first player of each position starts.
- `traits`: 0 to 100, 50 is an average SHL player: `acceleration`, `topSpeed`, `shotAccuracy`, `shotPower`, `checking`, `puckControl`, `hockeyIq`, `goalieReflexes`, and for goalies `goalieGlove`, `goalieBlocker`, `goalieReboundControl`, `goaliePuckHandling`. `hockeyIq` is how well the player reads the play and feeds every AI read (vision, passing lanes, re-think interval, positioning, getting open); it sets the odds, and the AI's skill-scaled randomness rolls against them. For a goalie `hockeyIq` is positioning: how far out and how square to the puck it stands. `goalieGlove` and `goalieBlocker` are the reach and sureness of each hand; the weaker one is the weak side AI shooters pick, and a goalie's `shoots` is its catching hand (Left puts the glove on the left). `goalieReboundControl` is how often a shot is held and how soft and wide the rebounds are; `goaliePuckHandling` is whether the goalie passes a held puck to a teammate and leaves the crease to move a loose puck on from behind the net. A trait name the game does not know fails the load.

## Sources

Rosters, numbers, handedness and lines come from SHL's official team pages (shl.se) as of 2026-10-09, using each club's latest SHL lineup. Players outside the 20-man squad (injured or extra) are left out.

Colours come from each club's crest: Frölunda green `#00573f`, red `#c8102e`, white; Djurgården blue `#0568af`, red `#e21f26`, yellow `#ffec0d`. Both wear white away.

The other twelve (D1b) follow the same rules: squads are each club's SHL lineup from 2026-10-08, with gaps (a missing backup goalie or an empty line slot) filled from the club's earlier 2026-27 lineups. Handedness missing on shl.se comes from the NHL's player records or, failing that, Wikipedia, hockeydb and Hockey Canada profiles; Andro Kaderli (Skellefteå) is set Left but sources disagree. Home colours come from each crest on shl.se and every club wears white away; check these against the real jerseys:

| Club | Home primary / secondary / accent |
|---|---|
| Brynäs | black `#1a1a1a`, yellow `#ffcc00`, white |
| Färjestad | green `#008e52`, white, gold `#e9b646` |
| HV71 | navy `#002d59`, yellow `#ffd500`, white |
| Linköping | navy `#002e5f`, white, red `#c8102e` |
| Luleå | black `#1a1a1a`, red `#d20a11`, yellow `#ffcc00` |
| Malmö | red `#ba0c2f`, black, gold `#f3c944` |
| Örebro | red `#d70728`, black, white |
| Rögle | green `#007a33`, white, yellow `#f6be00` |
| Skellefteå | yellow `#fbb906`, black, white (black helmet) |
| Timrå | blue `#124a97`, white, red `#e33125` |
| Växjö | navy `#002f60`, orange `#ec732b`, gold `#be9e55` |
| Björklöven | green `#0b5640`, yellow `#ffd000`, white |

Traits are seeded, not official: line (1st line higher), position, age, size and 2026-27 per-game goals, assists, shots and hits so far, clamped to 25..92. Goalies are seeded from save percentage; their glove, blocker, rebound control and puck handling come from `add_goalie_traits.py` (2026-10-11): one weaker hand per goalie, picked from the name. `hockeyIq` folds the earlier `smartPasses` and `smartPositioning` (2026-10-10): the rounded average for skaters, the positioning value for goalies. Edit any value by hand; nothing in code overrides these files.
