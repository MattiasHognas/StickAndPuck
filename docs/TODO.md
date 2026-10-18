# TODO

Tasks not yet done in the build plan. Each Milestone 6 rule becomes a rule setting with SHL, Arcade and NHL defaults. Each Milestone 10 task is a setting (on/off and strength) with league presets, and adds its English and Swedish strings.

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
| A21 | League ad sets | 3 | M8 NHL league | Two sets of board ad strips from art/scripts/ads_pixel.py (today one atlas, art/ads/ads_53x80.png, with ten made-up sponsors in a 3x5 pixel font): Swedish-style products for SHL and Canadian and American-style products for NHL. Made-up brands in each region's style, not real logos (after A17, art runs one at a time) |
| W137 | League-specific arena ads | 4 | M8 NHL league | The arena picks the ad atlas by league instead of the one fixed Assets.AdStrip, so SHL matches show the Swedish set and NHL matches the North American set (needs W117, A21) |
| A18 | Crowd art | 1 | M9 crowd and atmosphere | Animated crowd frames (sit, stand, cheer, wave, boo) that can be tinted with club colours, replacing the static crowd in arena.glb's stands |
| W119 | Reacting crowd | 2 | M9 crowd and atmosphere | The crowd visibly stands and cheers on goals, big hits, saves and fights, and rises with a momentum boost; builds on the stands in Arena/ArenaSystem.cs, the crowd swell in Momentum/MomentumSystem.cs and the Crowd cues in Audio/SoundCues.cs (needs A18) |
| W121 | Home and away chants | 2 | M9 crowd and atmosphere | Chants for the home club, with a smaller away section that cheers the visitors, so playing away the crowd is against you; new chant sounds in Audio/SfxTable.cs and Audio/SoundCues.cs, mixed with the Momentum crowd voices (needs W119 for the away section) |
| A19 | Banners and tifo art | 2 | M9 crowd and atmosphere | Banners, scarves and a tifo per club, made from the crests and club colours (after A18, art runs one at a time) |
| W120 | Home banners and tifo | 3 | M9 crowd and atmosphere | Home sections wave the home club's banners and scarves, and a tifo goes up at the start of the match and after home goals; builds on the Arena stands and the club colours and crests in the data (needs W119, A19) |
| W122 | Dressing room screen | 1 | M10 season story | A screen after each season result and before the next game, with the coach's talk, news, injuries and a play button, replacing the jump to the standings |
| W123 | Carry-over form | 1 | M10 season story | A hidden -5 to +5 trait nudge per player from recent games (points, plus/minus, saves, ice time), shown as an arrow next to the name |
| W124 | Team morale | 1 | M10 season story | One 0-100 number per club that moves with results, streaks and events; it sets the momentum meter's start value and shifts QuickSim odds |
| A20 | Dressing room and coach art | 1 | M10 season story | The dressing room backdrop and coach portrait for W122 |
| W125 | Pre-game coach talk | 2 | M10 season story | A short coach talk, then you pick a tone (calm, fire them up, demand discipline), each with an effect on that match |
| W126 | Intermission talk when trailing | 2 | M10 season story | When behind after period 1 or 2, a one-choice talk (push, stay patient) during the Zamboni break that changes shifts and aggression |
| W127 | Coach post-game reaction | 2 | M10 season story | A post-game coach line picked from what happened (hat trick, blown lead, shutout, a lost fight) with the player's name filled in |
| W128 | Injuries | 2 | M10 season story | Hard checks, blocked slap shots and fights can injure a player for 1 to 6 games, so lines change and depth players matter |
| W129 | Suspensions | 2 | M10 season story | A second fight in a game or a match penalty sits the player out the next 1 to 3 games |
| W130 | News events | 2 | M10 season story | A random event every few games from a weighted deck in a data file (flu, rookie call-up, contract dispute, travel delay, fan night) |
| W131 | Rivalries | 2 | M10 season story | Rival pairs marked in data get a pre-game banner, more checking and fights, and a bigger morale swing |
| W132 | Young player development | 2 | M10 season story | Young players gain a few trait points over the season from ice time and form, old ones may lose some; adds an age field to the club files |
| W133 | Board expectation and hot seat | 3 | M10 season story | The board sets a season goal from the club's strength, the dressing room tracks it, and an optional hot seat fires you if you miss badly |
| W134 | Mid-season milestones | 3 | M10 season story | Short three-game challenges from the coach ("win two of three") with a morale reward |
| W135 | Trade deadline | 3 | M10 season story | Around game 35, one or two player-for-player trade offers from AI clubs to accept or decline |
| W136 | Season recap | 3 | M10 season story | An end-of-season screen with the record by month, best game, top scorer, injuries, the board's verdict and generated headlines |
