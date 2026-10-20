# TODO

Tasks not yet done in the build plan. Each Milestone 6 rule becomes a rule setting with SHL, Arcade and NHL defaults; its penalty chain shares Rules/Penalties, so those four run one after another. Each Milestone 10 task is a setting (on/off and strength) with league presets, and adds its English and Swedish strings.

| ID | Task | Wave | Milestone | Context |
| --- | --- | --- | --- | --- |
| W79 | Goal review | 3 | M6 rules | One task for W79, W81 and W80: a held-goal state before a goal counts, goalie interference disallowing a goal, the review banner, and the NHL coach's challenge as an optional last step. Size L |
| W143 | Season ending scenes | 1 | M12 season endings and polish | A short in-engine scene for how the season ended, played before the season recap (W136): champions (trophy at centre ice, confetti, tifo), runners-up, semi-final handshakes, season over (empty arena, ice machine, lights going down), relegated (night outside the arena, a player in street clothes and a club-colour beanie walking away with his bag) and fired (the coach leaving the dressing room with a box). Celebrating players use staggered timing and varied poses. Prototyped on a local branch with SAP_ENDING; merged only after the owner approves the videos |
| W144 | Intro chant swell | 1 | M12 season endings and polish | The crowd chants (Audio/CrowdChants) swell during the title intro while the crowd cheers and the tifo is up, and settle when the menu appears |
| W145 | Clock check after the opening drop | 1 | M12 season endings and polish | The game clock was seen still showing 20:00 1.5 s into play (in the penalty-shot dev scene). Check whether the clock starts on the drop as intended or a display or start bug, and fix it if it is a bug |

## Parked

| ID | Task | Reason |
| --- | --- | --- |
| W86 | Dressed roster of 22 | Every roster has exactly the 20 players the lines need; spares only matter with injuries or ejections, and it needs 28 more real players |
