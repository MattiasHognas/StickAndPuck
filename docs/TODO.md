# TODO

Tasks not yet done in the build plan. Each Milestone 6 rule becomes a rule setting with SHL, Arcade and NHL defaults; its penalty chain shares Rules/Penalties, so those four run one after another. Each Milestone 10 task is a setting (on/off and strength) with league presets, and adds its English and Swedish strings.

| ID | Task | Wave | Milestone | Context |
| --- | --- | --- | --- | --- |
| W75 | League table rules | 1 | M6 rules | One task for W75, W77 and W78: the 2-2-1-1-1 playoff home order, the head-to-head standings tiebreak (NHL counts regulation wins first) and points per preset (3-2-1-0 SHL, 2-2-1-0 NHL); all in Season/Schedule and Season/Playoffs. Size S |
| W88 | Shootout repeat | 1 | M6 rules | NHL shootout order: nobody shoots twice until everyone has. Size XS |
| W82 | Faceoff rules | 1 | M6 rules | One task for W82, W83 and W84: a faceoff violation kicks the centre out for a winger, a minor on the second violation in NHL, smart dots for the remaining stoppages, and intentional offside moving the draw to the offender's zone. Size S |
| W87 | Intermission break | 1 | M6 rules | One task for W87 and W89: an intermission length key with skip on the existing Zamboni break, and a break before playoff overtime. Size S |
| W91 | Special teams switch | 1 | M6 rules | A SpecialTeams switch on the rules page turns power-play and penalty-kill shapes on and off. Size XS |
| W70 | Time-out | 2 | M6 rules | One 30 s time-out per club; the human calls it at a stoppage, the AI calls it late in close games, and it rests the skaters on the ice. Size S-M |
| W73 | Puck over the glass | 2 (penalty chain, 1st) | M6 rules | The puck can leave the rink over the glass (today netting catches everything), with an out-of-play stoppage and a minor from the defending zone. Size M |
| W74 | Too many men | 2 (penalty chain, 2nd) | M6 rules | Bench minor when a rushed human change steps on too early; lenient, and off in Arcade. Size M |
| W72 | Trapezoid and goalie limits | 2 (penalty chain, 3rd) | M6 rules | Trapezoid markings (needs arena art) and a delay-of-game minor for the human goalie playing the puck outside it; the AI goalie never breaks it. Size M |
| W71 | Penalty shot | 2 (penalty chain, 4th) | M6 rules | A foul from behind on a clean breakaway gives a penalty shot, reusing the shootout attempt. Size M |
| W79 | Goal review | 3 | M6 rules | One task for W79, W81 and W80: a held-goal state before a goal counts, goalie interference disallowing a goal, the review banner, and the NHL coach's challenge as an optional last step. Size L |
| W103 | Network play (online versus) | 1 | M7 network play | Online versus with lockstep input over the network, a host/join lobby, and desync detection from per-step checksums |
| W115 | NHL clubs and rosters | 1 | M8 NHL league | Data files for the 32 NHL clubs: names, home and away colours, real rosters, and seeded traits for every player and goalie, built with the SHL data tools (lineups.py already reads the NHL API) |
| A16 | NHL kits and crests | 1 | M8 NHL league | Home and away kits and crest art for all 32 clubs, built through make_kits.py; the 64-colour palette has too few free slots for 32 more clubs, so it has to grow |
| W116 | NHL season and playoffs | 2 | M8 NHL league | An NHL season played with the NHL rule preset: conferences and divisions, 82 games, and a 16-club playoff (needs W115) |
| W117 | League choice | 2 | M8 NHL league | Pick SHL or NHL when starting a season, arcade or versus game, with saves kept per league (needs W115) |
| A17 | NHL arena graphics | 2 | M8 NHL league | NHL centre ice and arena dressing (board ads are A21), swapped in when an NHL match is played (after A16, art runs one at a time) |
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
| W76 | Relegation Play Out | 3 | M10 season story | 13th v 14th best of 7, with the loser replaced by a promoted club; moved from M6 because it needs a next season and a 15th club in data/clubs |
| W138 | Arena flyover intro | 1 | M11 match intros and team playstyles | A camera flyover of the arena before each game, played during the existing warm-up phase in Pregame/ (PregameSystem, squads skating warm-up loops until Start) and skippable with Start; uses the Camera folder's broadcast and close-up cameras |
| W140 | Distinct club playstyles | 1 | M11 match intros and team playstyles | Make clubs play noticeably differently, building on the tactics field in the club files (forecheck balanced/aggressive/trap, entry carry/dumpAndChase, powerPlay) and AI/Tactics/TacticsSystem: stronger versions of today's styles plus new ones such as physical, shoot-from-anywhere and defensive shell, checked against the TacticsTally stats |
| A22 | Intro card art | 1 | M11 match intros and team playstyles | Art for the pre-game stats card: a versus panel with both crests, frames and stat icons in the HUD style |
| W139 | Team stats card | 2 | M11 match intros and team playstyles | A card over the flyover comparing both clubs: crests, record and table place, the attack/defence/goalie/overall ratings from Screens/ClubStrengths.cs, how they play, top scorers and recent form (needs W138, A22) |
| W141 | Coach scouting tip | 3 | M11 match intros and team playstyles | Before the game the coach says something useful about the opponent, such as their forecheck, entry style or best line, read from their real tactics and strengths so the tip is always true; fits into the W125 pre-game talk (needs W139, W140, W125) |

## Parked

| ID | Task | Reason |
| --- | --- | --- |
| W85 | Diving | Needs AI divers, a human dive input and a referee who judges dives; the biggest rules job and the most likely to frustrate |
| W86 | Dressed roster of 22 | Every roster has exactly the 20 players the lines need; spares only matter with injuries or ejections, and it needs 28 more real players |
