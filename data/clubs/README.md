# Club files

One JSON file per club under `data/clubs/{league}/`: [SHL](shl/README.md) and [NHL](nhl/README.md). The game loads only the selected league; `SAP_HOME` and `SAP_AWAY` pick clubs by `id`, otherwise the first two by id play.

- `id`: file-safe name, also used for kit atlases (`art/kits/{league}/{id}_{home|away}_{skater|goalie}_64.png`).
- `name`, `shortName`: full name and the club's code for the scorebug (three letters, four for HV71 and SAIK).
- `home`, `away`: kit colours as hex (`primary` jersey, `secondary` stripes and numbers, `accent` trim, `helmet`). The visiting club wears `away`. These are the club's real colours; the kit script snaps them to the palette.
- `roster`: the match squad in line order: two goalies (starter first), then four forward lines and three defence pairs as C, LW, RW, D, D per line (line 4 has no D). The first player of each position starts.
- `age` (W132): the player's age early in the 2026-27 season, for development: 23 or younger gains trait points over a season from ice time and form, 31 or older slowly loses some. Optional: a player without one neither develops nor declines. SHL ages come from shl.se (`tools/clubs/shl_ages.py`), NHL ages from the NHL API (`tools/clubs/nhl_clubs.py ages`).
- `traits`: 0 to 100, 50 is an average SHL player: `acceleration`, `topSpeed`, `shotAccuracy`, `shotPower`, `checking`, `puckControl`, `hockeyIq`, `goalieReflexes`, and for goalies `goalieGlove`, `goalieBlocker`, `goalieReboundControl`, `goaliePuckHandling`. `hockeyIq` is how well the player reads the play and feeds every AI read (vision, passing lanes, re-think interval, positioning, getting open); it sets the odds, and the AI's skill-scaled randomness rolls against them. For a goalie `hockeyIq` is positioning: how far out and how square to the puck it stands. `goalieGlove` and `goalieBlocker` are the reach and sureness of each hand; the weaker one is the weak side AI shooters pick, and a goalie's `shoots` is its catching hand (Left puts the glove on the left). `goalieReboundControl` is how often a shot is held and how soft and wide the rebounds are; `goaliePuckHandling` is whether the goalie passes a held puck to a teammate and leaves the crease to move a loose puck on from behind the net. A trait name the game does not know fails the load.

- `tactics` (W22d): how the club plays at even strength. `forecheck` is `balanced` (one forward on the puck, the rest in shape), `aggressive` (2-1-2: two forwards in on the puck and its outlet, one high, the defence pinching at the blue line) or `trap` (1-3-1: one forward angling at their blue line, three across the red line, one back). `entry` is `carry` or `dumpAndChase` (with defenders ahead, take it wide, fire it into the emptier corner from over the red line and send two forwards after it). `powerPlay` is `umbrella`, `oneThreeOne` or `mixed` (either, at random). `style` (W140) is `balanced` (none of its own), `physical` (the skater nearest the carrier skates at him and lines up hits three times as often), `shootFromAnywhere` (shoots from anywhere in the attacking zone with a lane, slap shots from distance) or `defensiveShell` (collapses into a box in front of its own net while the other side has the puck in its zone; with a balanced forecheck it sends nobody in on the breakout and waits in its own half). A missing field plays balanced, carry, mixed, balanced; an unknown key or value fails the load.

- `supporters` (A19): what the club's fans write on their tifo, scarves and banners, in the club's own language (Swedish for the SHL, English for the NHL, French where it is natural in Montréal). `tifo` is one or two lines under the crest on the big cloth (a line of up to 12 or so capitals is drawn twice the size), `scarf` the text knitted into the scarves and `banner` the sign two fans hold up. Only the HUD font's capitals, digits, `Å Ä Ö É Ü`, the other accented capitals and `! ' - .` can be drawn; `make_supporters.py` stops on a slogan that does not fit or uses another character. Optional: a club without one has no supporter sheet. The slogans only paint the stands, so the replay club fingerprint leaves them out.

## League folders

Use the same stable league id across league-specific assets:

- `data/clubs/{league}/{id}.json`: club colours, roster, traits and tactics.
- `art/kits/{league}/{id}_{home|away}_{skater|goalie}_64.png`: club kits.
- `art/kits/{league}/numbers/{id}_{home|away}_{skater|goalie}_{number}_64.png`: player kits.

`Leagues.Id` defines folder ids, and `Assets.ClubFolder` / `Assets.KitFolder` resolve them. Add league-specific data under its asset category using this convention. Keep shared translations, news cards, UI atlases and model templates shared. Club ids must remain globally unique while replay and UI references identify clubs by id.

Run `python3 art/scripts/make_kits.py --league shl` (or `nhl`) to build that league's kits. Add `check` to inspect colours without writing assets. Missing kits use the shared generic player textures.

## Rivals (W131)

`data/season/{league}/rivals.json` (`shl/`, `nhl/`) lists each league's rival pairs by club id. A game between two of them is a derby (SHL) or rivalry night (NHL): a banner on the opening card, more checking and fights, and a bigger team morale swing in a season; the RIVALRIES and RIVALRY STRENGTH rules (season group) turn it off or scale it. Whether a match is a rivalry game is decided when it is set up and stored in its replay. The pairs are a judgement from well-known rivalries, edit them freely: SHL Luleå–Skellefteå (the Norrland derby, [SVT/TV4 coverage](https://www.tv4.se/artikel/tt-250130-shltorsdag-0803dad1/vann-hett-derby-har-inte-skamt-bort-publiken)), Skellefteå–Björklöven ([TV4's hottest rivalries](https://www.tv4.se/artikel/5HwINa00S7U2BogrcpKPOL/experten-listar-de-hetaste-rivalmoetena-ogillar-varandra-vaeldigt-mycket)), Malmö–Rögle (the Skåne derby, [SVT](https://www.svt.se/sport/ishockey/malmo-korde-over-rogle-tog-tredje-raka-derbysegern)), HV71–Växjö (the Småland derby, [Sveriges Radio](https://www.sverigesradio.se/artikel/5962534)), Frölunda–Färjestad and Frölunda–Skellefteå (final rivals) and Brynäs–Djurgården; NHL Bruins–Canadiens, Bruins–Maple Leafs, Maple Leafs–Canadiens, Maple Leafs–Senators, Rangers–Islanders, Rangers–Devils, Penguins–Flyers, Penguins–Capitals, Oilers–Flames, Kings–Ducks, Blackhawks–Red Wings, Blackhawks–Blues, Avalanche–Red Wings and Lightning–Panthers.

## Supporter slogans (A19)

The `supporters` slogans are chants, song titles, supporter group names and rallying cries the fans actually use, found online on 2026-10-20 (supporter group chant lists, club pages, Wikipedia, news coverage). Lines marked *generic* are common shouts with no single source. Long chants are cut to a line that fits; taunting or offensive chants are left out (Frölunda's dropped "Indians" name too). Edit them freely.

| SHL club | Tifo | Scarf | Banner | Sources |
|---|---|---|---|---|
| Björklöven | GRÖNGULT HJÄRTA (the hymn) | LÖVEN | GREEN DEVILS (supporter group) | [club](https://www.bjorkloven.com/artikel/25e2alfas-2e4ad/lyssna-pa-nya-bjorklovenhymnen-grongult-hjarta), [Söderlövare](https://soderlovare.se/ramsor/), [Wikipedia](https://sv.wikipedia.org/wiki/IF_Bj%C3%B6rkl%C3%B6ven) |
| Brynäs | BRYNÄS HEJ / VI ÄLSKAR BRYNÄS | SVART GUL RÖD | FOLKETS LAG | [Brynäs Stockholm songs](https://brynasstockholm.se/sangtexter/), [club](https://www.brynas.se/artikel/t9yyainrw-1ekad/sanger-trummor-flaggor-och-tifon), [Wikipedia](https://sv.wikipedia.org/wiki/Bryn%C3%A4s_IF) |
| Djurgården | SJUNG FÖR GAMLA DJURGÅR'N | JÄRNKAMINERNA | MESTA MÄSTARNA | [Järnkaminerna](https://www.jarnkaminerna.se/sanger), [Wikipedia](https://sv.wikipedia.org/wiki/Djurg%C3%A5rdens_IF_Hockey) |
| Färjestad | FÄRJESTAD | WOLFPACK (supporter group) | FÄRJESTAD! | [Bryggeriet](https://www.bryggerietkarlstad.se/ramsor), [Wikipedia](https://sv.wikipedia.org/wiki/F%C3%A4rjestad_BK), [chant](https://www.youtube.com/watch?v=j-196ayWJnY) |
| Frölunda | GOA GUBBAR / KOM IGEN NU FRÖLUNDA | FRÖLUNDA | RÖTT GRÖNT VITT | [club music](https://www.frolundahockey.com/musik), [Goa gubbar](https://goagubbar.org/ramsor) |
| HV71 | HÅVE / JÖNKÖPING VÅRAN STAD | HV-BLÅ | VITA GULA BLÅ | [North Bank](https://northbanks.se/sanger/), [fan forum](https://forum.hv71fans.se/t/ramsor/5840) |
| Linköping | CLUBEN / IFRÅN STORA STOLTA | WHITE LIONS (supporter group) | HEJ HÅ LHC | [White Lions](https://www.whitelions.se/supporterkultur/), [chants](http://wl.mikaelsvelander.se/fanculture-chants.php) |
| Luleå | VI ÄR LULEÅ / INGET HALVVÄGS | KÄMPA LULEÅ | GULDET SKA HEM | [LuleåFans](https://www.luleafans.se/ramsor) |
| Malmö | REDHAWKS / REDHAWKS SUPPORT | MALMÖ | REDHAWKS | [club hymn](https://www.malmoredhawks.com/artikel/8kkcainrx-33nad/lar-dig-malmo-redhawks-nya-hymn), [Wikipedia](https://sv.wikipedia.org/wiki/Malm%C3%B6_Redhawks) |
| Örebro | FJORTONTRE / 14-3 ÖREBRO (supporter group) | ÖHK | HEJA ÖREBRO (*generic*) | [14-3](https://www.fjortontre.se/), [Wikipedia](https://sv.wikipedia.org/wiki/%C3%96rebro_HK) |
| Rögle | ANGEL ISLAND ARMY (supporter group) | RÖGLE BK | GRÖNVIT FEST | [Wikipedia](https://sv.wikipedia.org/wiki/R%C3%B6gle_BK), [TV4](https://www.tv4.se/artikel/tt-240425-shlfinalfeber-25356fa2/gronvit-fest-infor-historisk-final) |
| Skellefteå | VISA HJÄRTA / PLATSEN ÄR HÄR | NORTH POWER (supporter group) | HEJ HÄR ÄR VI | [North Power](https://www.northpower.nu/ramsor/ramsor), [club](https://www.skellefteaaik.se/article/u5vat2f-5n5bi1/view) |
| Timrå | TIMRÅ HEJ | TIMRÅ IK | RÄTTA VIRKET (stand group) | [supporter club](https://www.dubbeltallen.se/minnen/supporterklubben/), [chants](https://tikare.se/ramsor/) |
| Växjö | SJÖARNAS STAD (the hymn) | LAKERS | LAKERS LAKEJER (supporter group) | [Lakers Lakejer](https://lakerslakejer.se/ramsor/) |

| NHL club | Tifo | Scarf | Banner | Sources |
|---|---|---|---|---|
| Avalanche | GO AVS GO | AVALANCHE | LET'S GO AVS | [NHL](https://nhl.com/news/avalanche-hold-next-generation-night-330355938), [Axios](https://www.axios.com/local/denver/2022/06/15/colorado-avalanche-bandwagon-stanley-cup) |
| Blackhawks | ONE GOAL (club campaign) | BLACKHAWKS | LET'S GO HAWKS (*generic*) | [campaign](https://www.lovethework.com/work-awards/campaigns/chicago-blackhawks-hockey-286759) |
| Blue Jackets | C-B-J / FIRE THE CANNON | COLUMBUS | FIRE THE CANNON | [NHL](https://nhl.com/bluejackets/news/blue-jackets-thank-you-fans-5th-line), [the cannon](https://cwcolumbus.com/news/local/columbus-blue-jackets-cannon-makes-grand-entrance-to-shoe-ahead-of-stadium-series) |
| Blues | PLAY GLORIA | ST. LOUIS | LET'S GO BLUES | [NBC Sports](https://nhl.nbcsports.com/2019/05/08/blues-embrace-80s-hit-gloria-as-post-win-anthem) |
| Bruins | BIG BAD BRUINS | BRUINS | LET'S GO BRUINS | [Boston Globe](https://www.bostonglobe.com/2021/05/29/sports/with-packed-house-td-garden-bruins-islanders-buzz-was-back-city), [Spectrum News](https://spectrumlocalnews.com/me/maine/sports/2023/11/20/bruins--1970-and-1972-stanley-cup-winners-finally-raise-banner-to-rafters) |
| Canadiens | GO HABS GO / NOS BRAS MEURTRIS (the dressing-room motto) | LES GLORIEUX | GO HABS GO | [The Suburban](https://www.thesuburban.com/opinion/editorials/go-habs-go-a-rallying-cry-of-a-common-heart/article_925498ef-f979-4064-b0b5-2a30d67453e1.html), [Noovo](https://www.noovo.info/nouvelle/il-ny-aura-pas-de-go-habs-go-sur-les-bus-pour-debuter-la-saison-des-canadiens-annonce-la-stm.html), [Encyclopédie canadienne](https://encyclopediecanadienne.ca/article/montreal-canadiens) |
| Canucks | TOWEL POWER | CANUCKS | GO CANUCKS GO (*generic*) | [Wikipedia](https://en.wikipedia.org/wiki/Towel_Power) |
| Capitals | ROCK THE RED | CAPITALS | LET'S GO CAPS (*generic*) | [DCist](https://dcist.com/story/16/04/14/washington-capitals-rock-the-red-ra) |
| Devils | JERSEY PRIDE (*generic*) | DEVILS | LET'S GO DEVILS | [Inklings](https://inklingsnews.com/e/2012/06/05/why-playoff-hockey-games-at-the-rock-are-surreal) |
| Ducks | QUACK ATTACK | ANAHEIM | LET'S GO DUCKS | [NHL](https://www.nhl.com/news/playoff-arenas-have-unique-features-for-fans), [Visit Anaheim](https://www.visitanaheim.org/events/sports-events/anaheim-ducks/) |
| Flames | C OF RED | FLAMES | GO FLAMES GO | [Red Deer Advocate](https://www.reddeeradvocate.com/sports/the-spectacle-that-is-the-c-of-red/) |
| Flyers | BROAD STREET BULLIES | FLYERS | LET'S GO FLYERS | [Inquirer](https://www.inquirer.com/flyers/broad-street-bullies-nickname-history-philadelphia-bulletin-20240515.html), [Philadelphia Encyclopedia](https://philadelphiaencyclopedia.org/essays/broad-street-bullies/) |
| Golden Knights | VEGAS BORN | KNIGHT UP | GO KNIGHTS GO | [LV Sports Biz](https://lvsportsbiz.com/2018/09/13/exclusive-golden-knights-unveil-updated-vegas-born-marketing-campaign-for-season-2/), [Review-Journal](https://www.reviewjournal.com/?p=1381102) |
| Hurricanes | STORM SURGE / BUNCH OF JERKS | CANIACS | BUNCH OF JERKS | [WFMY](https://wfmynews2.com/article/news/bunch-of-jerks-why-the-carolina-hurricanes-are-called-just-that-and-what-non-hockey-fans-should-know/83-35f1f7e8-bbea-491b-991a-c73c44c9031d), [TribLive](https://triblive.com/sports/hurricanes-respond-to-don-cherrys-bunch-of-jerks-comment-with-new-t-shirt) |
| Islanders | YES YES YES | ISLANDERS | LET'S GO ISLES | [NHL](https://nhl.com/news/wwe-star-behind-islanders-chant-attends-home-opener-282735768) |
| Jets | WHITEOUT / TRUE NORTH | WINNIPEG | GO JETS GO | [The Hockey News](https://thehockeynews.com/nhl/winnipeg-jets/latest-news/revisiting-the-creation-of-the-winnipeg-jets-whiteout-tradition), [CBC](https://www.cbc.ca/lite/story/1.7180537) |
| Kings | GO KINGS GO | LA KINGS | LET'S GO KINGS | [Kings](https://www.nhl.com/kings/news/2026-playoff-fan-rally) |
| Kraken | RELEASE THE KRAKEN | KRAKEN | LET'S GET KRAKEN | [NHLPA](https://www.nhlpa.com/news/2-22807/release-the-kraken-seattle-unveils-name-for-nhl-franchise) |
| Lightning | BE THE THUNDER (playoff campaign) | BOLTS | GO BOLTS GO (*generic*) | [WTSP](https://wtsp.com/article/sports/nhl/lightning/tampa-bay-lightning-2024-nhl-playoffs-fan-events/67-70d75e19-deec-41ca-9fb3-35ce57107b5d) |
| Mammoth | TUSKS UP | MAMMOTH | TUSKS UP UTAH | [Utah Sports Commission](https://utahsportscommission.com/tusks-up/), [KUER](https://www.kuer.org/sports-recreation/2026-04-28/with-a-taste-of-the-playoffs-utah-mammoth-fans-are-now-all-in-on-their-team) |
| Maple Leafs | GO LEAFS GO / LEAFS FOREVER | LEAFS NATION | GO LEAFS GO | [Daily Hive](https://dailyhive.com/toronto/the-rock-lets-go-leafs-chant) |
| Oilers | OIL COUNTRY / THIS IS OIL COUNTRY | OILERS | LET'S GO OILERS | [Oilersnation](https://oilersnation.com/news/the-this-is-oil-country-sign-is-making-a-comeback-at-rogers-place), [Global News](https://globalnews.ca/news/8865969/edmonton-oilers-community-rally/amp) |
| Panthers | TIME TO HUNT | PANTHERS | LET'S GO CATS (*generic*) | [Panthers vault](https://www.flapanthersvault.com/item/time-to-hunt/) |
| Penguins | LET'S GO PENS | PENGUINS | LET'S GO PENS | [Pittsburgh Hockey Now](https://pittsburghhockeynow.com/pittsburgh-penguins-fans-rocked-ppg-tristan-jarry-game-2-sidney-crosby/) |
| Predators | SMASHVILLE | PREDATORS | LET'S GO PREDS (*generic*) | [Wikipedia](https://en.wikipedia.org/wiki/Smashville) |
| Rangers | BLUESHIRTS | RANGERS | LET'S GO RANGERS | [The Hockey News](https://thehockeynews.com/nhl/new-jersey-devils/game-day/devils-dominate-rangers-in-7-1-win-as-nico-daws-earns-first-victory-of-the-season), [Blueshirt Banter](https://blueshirtbanter.com/the-story-behind-the-famous-potvin-chant-new-york-rangers-ulf-nilsson-denis-potvin) |
| Red Wings | HOCKEYTOWN | RED WINGS | GO WINGS GO (*generic*) | [Wikipedia](https://en.wikipedia.org/wiki/Hockeytown) |
| Sabres | LET'S GO BUFFALO | SABRES | ONE BUFFALO | [Sabres](https://www.nhl.com/sabres/news/buffalo-sabres-opening-night-fans) |
| Senators | GO SENS GO / SENS ARMY | SENS ARMY | GO SENS GO | [The Hockey News](https://thehockeynews.com/news/latest-news/it-was-f-g-unbelievable-senators-reflect-on-the-support-after-first-playoff-appearance-since-2017), [CTV](https://www.ctvnews.ca/ottawa/photos/2026/04/20/ottawa-senators-fans-show-off-their-pride-during-the-playoffs/) |
| Sharks | SHARK TANK | SHARKS | LET'S GO SHARKS (*generic*) | [USC folklore archive](https://folklore.usc.edu/?p=87225) |
| Stars | VICTORY GREEN | STARS | LET'S GO STARS | [HHOF shop](https://shop.hhof.com/products/dallas-stars-puck-lets-go), [Icethetics](https://www.icethetics.co/blog/2014/11/30/dallas-stars-reveal-road-to-rebrand-with-loads-of-concepts) |
| Wild | LET'S PLAY HOCKEY (the pregame call) | MINNESOTA | STATE OF HOCKEY | [Star Tribune](https://m.startribune.com/wild-s-marketing-brilliant-before-a-game-played/251716051/), [Barrett Media](https://barrettmedia.com/2023/12/28/cory-cove-kfan-the-power-trip-can-promote-zach-halverson-leading-lets-play-hockey-call-at-wild-game/) |

`python3 art/scripts/make_supporters.py [club ...]` paints every club's (or the named clubs') supporter sheet from these slogans, its crest and its home colours into `art/crowd/supporters/{league}/{id}.png`; the layout is `art/crowd/supporters.json`. See [Development](../../docs/DEVELOPMENT.md#art-and-data).
