# Club files

One JSON file per club. The game loads every `*.json` here; `SAP_HOME` and `SAP_AWAY` pick clubs by `id`, otherwise the first two by id play.

- `id`: file-safe name, also used for kit atlases (`art/kits/{id}_{home|away}_{skater|goalie}_64.png`).
- `name`, `shortName`: full name and three letters for the scorebug.
- `home`, `away`: kit colours as hex (`primary` jersey, `secondary` stripes and numbers, `accent` trim, `helmet`). The visiting club wears `away`. These are the club's real colours; the kit script snaps them to the palette.
- `roster`: the match squad in line order: two goalies (starter first), then four forward lines and three defence pairs as C, LW, RW, D, D per line (line 4 has no D). The first player of each position starts.
- `traits`: 0 to 100, 50 is an average SHL player: `acceleration`, `topSpeed`, `shotAccuracy`, `shotPower`, `checking`, `puckControl`, `smartPasses`, `smartPositioning`, `goalieReflexes`.

## Sources

Rosters, numbers, handedness and lines come from SHL's official team pages (shl.se) as of 2026-10-09, using each club's latest SHL lineup. Players outside the 20-man squad (injured or extra) are left out.

Colours come from each club's crest: Frölunda green `#00573f`, red `#c8102e`, white; Djurgården blue `#0568af`, red `#e21f26`, yellow `#ffec0d`. Both wear white away.

Traits are seeded, not official: line (1st line higher), position, age, size and 2026-27 per-game goals, assists, shots and hits so far, clamped to 25..92. Goalies are seeded from save percentage. Edit any value by hand; nothing in code overrides these files.
