# Club files

One JSON file per club. The game loads every `*.json` here; `SAP_HOME` and `SAP_AWAY` pick clubs by `id`, otherwise the first two by id play.

- `id`: file-safe name, also used for kit atlases (`art/kits/{id}_{home|away}_{skater|goalie}_64.png`).
- `name`, `shortName`: full name and the club's SHL code for the scorebug (three letters, four for HV71 and SAIK).
- `home`, `away`: kit colours as hex (`primary` jersey, `secondary` stripes and numbers, `accent` trim, `helmet`). The visiting club wears `away`. These are the club's real colours; the kit script snaps them to the palette.
- `roster`: the match squad in line order: two goalies (starter first), then four forward lines and three defence pairs as C, LW, RW, D, D per line (line 4 has no D). The first player of each position starts.
- `traits`: 0 to 100, 50 is an average SHL player: `acceleration`, `topSpeed`, `shotAccuracy`, `shotPower`, `checking`, `puckControl`, `hockeyIq`, `goalieReflexes`, and for goalies `goalieGlove`, `goalieBlocker`, `goalieReboundControl`, `goaliePuckHandling`. `hockeyIq` is how well the player reads the play and feeds every AI read (vision, passing lanes, re-think interval, positioning, getting open); it sets the odds, and the AI's skill-scaled randomness rolls against them. For a goalie `hockeyIq` is positioning: how far out and how square to the puck it stands. `goalieGlove` and `goalieBlocker` are the reach and sureness of each hand; the weaker one is the weak side AI shooters pick, and a goalie's `shoots` is its catching hand (Left puts the glove on the left). `goalieReboundControl` is how often a shot is held and how soft and wide the rebounds are; `goaliePuckHandling` is whether the goalie passes a held puck to a teammate and leaves the crease to move a loose puck on from behind the net. A trait name the game does not know fails the load.

## Sources

Rosters, numbers, handedness and lines come from SHL's official team pages (shl.se) as of 2026-10-09, using each club's latest SHL lineup. Players outside the 20-man squad (injured or extra) are left out.

The other twelve (D1b) follow the same rules: squads are each club's SHL lineup from 2026-10-08, with gaps (a missing backup goalie or an empty line slot) filled from the club's earlier 2026-27 lineups. Handedness missing on shl.se comes from the NHL's player records or, failing that, Wikipedia, hockeydb and Hockey Canada profiles; Andro Kaderli (Skellefteå) is set Left but sources disagree.

Kit colours were checked against each club's 2026-27 replica jerseys (A6, 2026-10-12): club shops and Intersport supporter pages, with colours sampled from product photos where no brand hex exists. Helmets could not be confirmed and follow the home sweater. Djurgården, HV71, Luleå and Skellefteå wear a coloured away kit; the rest wear white.

| Club | Home primary / secondary / accent | Away | Source |
|---|---|---|---|
| Björklöven | green `#0b5640`, yellow `#ffd000`, white | white | [club shop](https://bjorklovenshopen.se/) |
| Brynäs | black `#1a1a1a`, yellow `#ffcc00`, red `#e30613` | white, yellow and red trim | [Intersport](https://www.intersport.se/supporterklubbar/brynas-if) |
| Djurgården | navy `#2b3a6e`, yellow `#ffec0d`, dark red `#a6192e` | yellow `#f6b83c`, navy and dark red | [DIF butiken](https://difbutiken.se/ccm/) |
| Färjestad | green `#008e52`, white, gold `#e9b646` | white | [club shop](https://shop.farjestadbk.se/matchtrojor) |
| Frölunda | red `#c8102e`, green `#00573f`, white | white, green shoulders | [Intersport](https://www.intersport.se/products/frolunda-hockey-home-26-27-matchtroja/red-168435801), [club](https://www.frolundahockey.com/article/b0datje-24601/view) |
| HV71 | navy `#002d59`, yellow `#ffd500`, white | yellow, navy | [Intersport](https://www.intersport.se/products/hv71-away-26-27-jr-matchtroja-barn/yellow-168436101) |
| Linköping | navy `#002e5f`, white, red `#c8102e` | white (away not found) | [Hockeytown](https://www.hockeytown.se/sv/articles/2.1343.37642/ccm-matchtroja-replica-lhc-2627-sr) |
| Luleå | red `#d20a11`, black, yellow `#ffcc00` | yellow, black (white on some away games) | [club](https://www.luleahockey.se/article/qkjeajktm-30c4d/view) |
| Malmö | black `#1a1a1a`, red `#ba0c2f`, white | white (away not found) | [club shop](https://redhawksshop.com/products/matchreplica-hemma-2026-27) |
| Örebro | red `#d70728`, black, white | white (a black alternate exists) | [club shop](https://shop.orebrohockey.se/matchtrojor) |
| Rögle | green `#007a33`, white, yellow `#f6be00` | white (away not found) | [club shop](https://1932store.roglebk.se/collections/alla-matchtrojor) |
| Skellefteå | black `#1a1a1a`, yellow `#fbb906`, red `#c8102e` | yellow, black | [Intersport](https://www.intersport.se/products/skelleftea-aik-pro-7000-home-26-27-matchtroja/black-169164301) |
| Timrå | red `#c8102e`, white, blue `#124a97` | white, red | [club shop](https://clubshopen.timraik.se/) |
| Växjö | navy `#002f60`, orange `#ec732b`, white | white | [club shop](https://teamstore.vaxjolakers.se/products/matchtroja-navy-s) |

Traits are seeded, not official: line (1st line higher), position, age, size and 2026-27 per-game goals, assists, shots and hits so far, clamped to 25..92. Goalies are seeded from save percentage; their glove, blocker, rebound control and puck handling come from `add_goalie_traits.py` (2026-10-11): one weaker hand per goalie, picked from the name. `hockeyIq` folds the earlier `smartPasses` and `smartPositioning` (2026-10-10): the rounded average for skaters, the positioning value for goalies. Edit any value by hand; nothing in code overrides these files.
