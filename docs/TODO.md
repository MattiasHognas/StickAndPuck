# TODO

Tasks not yet done in the build plan. Each Milestone 6 rule becomes a rule setting with SHL, Arcade and NHL defaults.

| ID | Task | Wave | Milestone | Context |
| --- | --- | --- | --- | --- |
| W70 | Time-out | 1 | M6 rules | One 30 s time-out per club; the human calls it at a stoppage, the AI calls it late in close games |
| W71 | Penalty shot | 1 | M6 rules | Awarded for a foul from behind on a clean breakaway |
| W72 | Trapezoid and goalie limits | 1 | M6 rules | Trapezoid markings behind the goals (needs arena art) and a delay-of-game minor for a goalie playing the puck outside it |
| W73 | Puck over the glass | 1 | M6 rules | Clearing the puck over the glass from the defending zone is a minor |
| W74 | Too many men | 1 | M6 rules | Bench minor when a manual line change comes on too early |
| W75 | Playoff home order | 1 | M6 rules | Best-of-7 series played in a 2-2-1-1-1 home order |
| W76 | Relegation Play Out | 1 | M6 rules | 13th v 14th best of 7; the loser is replaced by a promoted club (needs one more club in the data) |
| W77 | Standings tiebreak | 1 | M6 rules | Ties broken by head-to-head after goals scored; NHL counts regulation wins first |
| W78 | Points system per preset | 1 | M6 rules | 3-2-1-0 points for SHL, 2-2-1-0 for NHL |
| W79 | Goal review banner | 1 | M6 rules | A review banner shows after a doubtful goal |
| W80 | Coach's challenge | 1 | M6 rules | A coach can challenge a goal; NHL preset only |
| W81 | Goalie interference | 1 | M6 rules | A goal is disallowed when an attacker interferes with the goalie |
| W82 | Faceoff violations | 1 | M6 rules | Faceoff violations and the stick-down order at the dot |
| W83 | Smart faceoff dots | 1 | M6 rules | After a stoppage the faceoff spot is chosen in favour of the non-offending club |
| W84 | Intentional offside | 1 | M6 rules | An intentional offside moves the faceoff into the offender's zone |
| W86 | Dressed roster | 1 | M6 rules | A dressed roster of 22, with spare players outside it |
| W87 | Intermission length | 1 | M6 rules | Intermissions get a set length with a skippable timer |
| W88 | Shootout repeat order | 1 | M6 rules | NHL order for shooters repeating after every skater has shot in a long shootout |
| W91 | Special teams switch | 1 | M6 rules | A SpecialTeams switch on the rules page turns power plays and penalty kills on and off |
| W85 | Diving | 2 | M6 rules | Diving penalties; needs players who can embellish first, which has no task yet |
| W89 | Playoff overtime break | 2 | M6 rules | A break before playoff overtime, shown on the W87 intermission screen |
| W103 | Network play (online versus) | 1 | M7 network play | Online versus with lockstep input over the network, a host/join lobby, and desync detection from per-step checksums |
| W115 | NHL clubs and rosters | 1 | M8 NHL league | Data files for the 32 NHL clubs: names, home and away colours, real rosters, and seeded traits for every player and goalie, built with the SHL data tools (lineups.py already reads the NHL API) |
| A16 | NHL kits and crests | 1 | M8 NHL league | Home and away kits and crest art for all 32 clubs, built through make_kits.py; the 64-colour palette has too few free slots for 32 more clubs, so it has to grow |
| W116 | NHL season and playoffs | 2 | M8 NHL league | An NHL season played with the NHL rule preset: conferences and divisions, 82 games, and a 16-club playoff (needs W115) |
| W117 | League choice | 2 | M8 NHL league | Pick SHL or NHL when starting a season, arcade or versus game, with saves kept per league (needs W115) |
| A17 | NHL arena graphics | 2 | M8 NHL league | NHL centre ice, board ads and arena dressing, swapped in when an NHL match is played (after A16, art runs one at a time) |
| W118 | NHL presentation | 3 | M8 NHL league | Scorebug, lineup and standings screens showing NHL crests and names, with English and Swedish strings (needs W116, A16) |
