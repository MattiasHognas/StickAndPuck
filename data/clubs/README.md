# Club files

One JSON file per club under `data/clubs/{league}/`: [SHL](shl/README.md) and [NHL](nhl/README.md). The game loads only the selected league; `SAP_HOME` and `SAP_AWAY` pick clubs by `id`, otherwise the first two by id play.

- `id`: file-safe name, also used for kit atlases (`art/kits/{league}/{id}_{home|away}_{skater|goalie}_64.png`).
- `name`, `shortName`: full name and the club's code for the scorebug (three letters, four for HV71 and SAIK).
- `home`, `away`: kit colours as hex (`primary` jersey, `secondary` stripes and numbers, `accent` trim, `helmet`). The visiting club wears `away`. These are the club's real colours; the kit script snaps them to the palette.
- `roster`: the match squad in line order: two goalies (starter first), then four forward lines and three defence pairs as C, LW, RW, D, D per line (line 4 has no D). The first player of each position starts.
- `traits`: 0 to 100, 50 is an average SHL player: `acceleration`, `topSpeed`, `shotAccuracy`, `shotPower`, `checking`, `puckControl`, `hockeyIq`, `goalieReflexes`, and for goalies `goalieGlove`, `goalieBlocker`, `goalieReboundControl`, `goaliePuckHandling`. `hockeyIq` is how well the player reads the play and feeds every AI read (vision, passing lanes, re-think interval, positioning, getting open); it sets the odds, and the AI's skill-scaled randomness rolls against them. For a goalie `hockeyIq` is positioning: how far out and how square to the puck it stands. `goalieGlove` and `goalieBlocker` are the reach and sureness of each hand; the weaker one is the weak side AI shooters pick, and a goalie's `shoots` is its catching hand (Left puts the glove on the left). `goalieReboundControl` is how often a shot is held and how soft and wide the rebounds are; `goaliePuckHandling` is whether the goalie passes a held puck to a teammate and leaves the crease to move a loose puck on from behind the net. A trait name the game does not know fails the load.

- `tactics` (W22d): how the club plays at even strength. `forecheck` is `balanced` (one forward on the puck, the rest in shape), `aggressive` (2-1-2: two forwards in on the puck and its outlet, one high, the defence pinching at the blue line) or `trap` (1-3-1: one forward angling at their blue line, three across the red line, one back). `entry` is `carry` or `dumpAndChase` (with defenders ahead, take it wide, fire it into the emptier corner from over the red line and send two forwards after it). `powerPlay` is `umbrella`, `oneThreeOne` or `mixed` (either, at random). `style` (W140) is `balanced` (none of its own), `physical` (the skater nearest the carrier skates at him and lines up hits three times as often), `shootFromAnywhere` (shoots from anywhere in the attacking zone with a lane, slap shots from distance) or `defensiveShell` (collapses into a box in front of its own net while the other side has the puck in its zone; with a balanced forecheck it sends nobody in on the breakout and waits in its own half). A missing field plays balanced, carry, mixed, balanced; an unknown key or value fails the load.

## League folders

Use the same stable league id across league-specific assets:

- `data/clubs/{league}/{id}.json`: club colours, roster, traits and tactics.
- `art/kits/{league}/{id}_{home|away}_{skater|goalie}_64.png`: club kits.
- `art/kits/{league}/numbers/{id}_{home|away}_{skater|goalie}_{number}_64.png`: player kits.

`Leagues.Id` defines folder ids, and `Assets.ClubFolder` / `Assets.KitFolder` resolve them. Add league-specific data under its asset category using this convention. Keep shared translations, news cards, UI atlases and model templates shared. Club ids must remain globally unique while replay and UI references identify clubs by id.

Run `python3 art/scripts/make_kits.py --league shl` (or `nhl`) to build that league's kits. Add `check` to inspect colours without writing assets. Missing kits use the shared generic player textures.

## Rivals (W131)

`data/season/{league}/rivals.json` (`shl/`, `nhl/`) lists each league's rival pairs by club id. A game between two of them is a derby (SHL) or rivalry night (NHL): a banner on the opening card, more checking and fights, and a bigger team morale swing in a season; the RIVALRIES and RIVALRY STRENGTH rules (season group) turn it off or scale it. Whether a match is a rivalry game is decided when it is set up and stored in its replay. The pairs are a judgement from well-known rivalries, edit them freely: SHL Luleå–Skellefteå (the Norrland derby, [SVT/TV4 coverage](https://www.tv4.se/artikel/tt-250130-shltorsdag-0803dad1/vann-hett-derby-har-inte-skamt-bort-publiken)), Skellefteå–Björklöven ([TV4's hottest rivalries](https://www.tv4.se/artikel/5HwINa00S7U2BogrcpKPOL/experten-listar-de-hetaste-rivalmoetena-ogillar-varandra-vaeldigt-mycket)), Malmö–Rögle (the Skåne derby, [SVT](https://www.svt.se/sport/ishockey/malmo-korde-over-rogle-tog-tredje-raka-derbysegern)), HV71–Växjö (the Småland derby, [Sveriges Radio](https://www.sverigesradio.se/artikel/5962534)), Frölunda–Färjestad and Frölunda–Skellefteå (final rivals) and Brynäs–Djurgården; NHL Bruins–Canadiens, Bruins–Maple Leafs, Maple Leafs–Canadiens, Maple Leafs–Senators, Rangers–Islanders, Rangers–Devils, Penguins–Flyers, Penguins–Capitals, Oilers–Flames, Kings–Ducks, Blackhawks–Red Wings, Blackhawks–Blues, Avalanche–Red Wings and Lightning–Panthers.
