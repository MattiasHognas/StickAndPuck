# TODO

Tasks not yet done in the build plan. Each Milestone 6 rule becomes a rule setting with SHL, Arcade and NHL defaults; its penalty chain shares Rules/Penalties, so those four run one after another. Each Milestone 10 task is a setting (on/off and strength) with league presets, and adds its English and Swedish strings.

| ID | Task | Wave | Milestone | Context |
| --- | --- | --- | --- | --- |
| W73 | Puck over the glass | 2 (penalty chain, 1st) | M6 rules | The puck can leave the rink over the glass (today netting catches everything), with an out-of-play stoppage and a minor from the defending zone. Size M |
| W74 | Too many men | 2 (penalty chain, 2nd) | M6 rules | Bench minor when a rushed human change steps on too early; lenient, and off in Arcade. Size M |
| W72 | Trapezoid and goalie limits | 2 (penalty chain, 3rd) | M6 rules | Trapezoid markings (needs arena art) and a delay-of-game minor for the human goalie playing the puck outside it; the AI goalie never breaks it. Size M |
| W71 | Penalty shot | 2 (penalty chain, 4th) | M6 rules | A foul from behind on a clean breakaway gives a penalty shot, reusing the shootout attempt. Size M |
| W79 | Goal review | 3 | M6 rules | One task for W79, W81 and W80: a held-goal state before a goal counts, goalie interference disallowing a goal, the review banner, and the NHL coach's challenge as an optional last step. Size L |
| A16 | NHL kits and crests | 1 | M8 NHL league | Home and away kits and crest art for all 32 clubs, built through make_kits.py; the 64-colour palette has too few free slots for 32 more clubs, so it has to grow |
| W117 | League choice | 2 | M8 NHL league | Pick SHL or NHL when starting a season, arcade or versus game, with saves kept per league (needs W115) |
| A17 | NHL arena graphics | 2 | M8 NHL league | NHL centre ice and arena dressing (board ads are A21), swapped in when an NHL match is played (after A16, art runs one at a time) |
| W118 | NHL presentation | 3 | M8 NHL league | Scorebug, lineup and standings screens showing NHL crests and names, with English and Swedish strings (needs W116, A16) |
| A21 | League ad sets | 3 | M8 NHL league | Two sets of board ad strips from art/scripts/ads_pixel.py (today one atlas, art/ads/ads_53x80.png, with ten made-up sponsors in a 3x5 pixel font): Swedish-style products for SHL and Canadian and American-style products for NHL. Made-up brands in each region's style, not real logos (after A17, art runs one at a time) |
| W137 | League-specific arena ads | 4 | M8 NHL league | The arena picks the ad atlas by league instead of the one fixed Assets.AdStrip, so SHL matches show the Swedish set and NHL matches the North American set (needs W117, A21) |
| A19 | Banners and tifo art | 2 | M9 crowd and atmosphere | Banners, scarves and a tifo per club, made from the crests and club colours (after A18, art runs one at a time) |
| W120 | Home banners and tifo | 3 | M9 crowd and atmosphere | Home sections wave the home club's banners and scarves, and a tifo goes up at the start of the match and after home goals; builds on the Arena stands and the club colours and crests in the data (needs W119, A19) |
| W142 | Tifo in the title intro | 4 | M9 crowd and atmosphere | The title intro (black, letters drop in, camera comes down from the rafters over a cheering crowd, random home and away clubs each launch) raises the home club's tifo through the hook it leaves for it, so the home stands show their tifo as the camera arrives (needs W120) |
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
| A22 | Intro card art | 1 | M11 match intros and team playstyles | Art for the pre-game stats card: a versus panel with both crests, frames and stat icons in the HUD style |
| W139 | Team stats card | 2 | M11 match intros and team playstyles | A card over the flyover comparing both clubs: crests, record and table place, the attack/defence/goalie/overall ratings from Screens/ClubStrengths.cs, how they play, top scorers and recent form (needs W138, A22) |
| W141 | Coach scouting tip | 3 | M11 match intros and team playstyles | Before the game the coach says something useful about the opponent, such as their forecheck, entry style or best line, read from their real tactics and strengths so the tip is always true; fits into the W125 pre-game talk (needs W139, W140, W125) |

## Parked

| ID | Task | Reason |
| --- | --- | --- |
| W85 | Diving | Needs AI divers, a human dive input and a referee who judges dives; the biggest rules job and the most likely to frustrate |
| W86 | Dressed roster of 22 | Every roster has exactly the 20 players the lines need; spares only matter with injuries or ejections, and it needs 28 more real players |
