# SHL clubs

The 14 Swedish clubs. See the [club format and folder convention](../README.md).

## Tactics

Each club's tactics are a judgement from how the club is known to play, not official data; change them freely. Aggressive forecheck: Björklöven, Djurgården, Färjestad, Frölunda, Skellefteå. Trap: HV71, Luleå, Malmö, Växjö. Balanced: Brynäs, Linköping, Örebro, Rögle, Timrå. Dump and chase: Björklöven, HV71, Linköping, Luleå, Timrå; the rest carry the puck in. Physical: Luleå, Skellefteå, Timrå. Shoot from anywhere: Färjestad, Frölunda, Rögle. Defensive shell: HV71, Växjö, Örebro. The rest play balanced.

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

