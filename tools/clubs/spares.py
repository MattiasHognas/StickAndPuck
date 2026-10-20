#!/usr/bin/env python3
"""Adds two spare skaters to every club file in data/clubs/{league}/ (W86): a forward and a seventh defenceman.

    python3 -I tools/clubs/spares.py shl <cache dir> [<club dir>]
    python3 -I tools/clubs/spares.py nhl <cache dir> [<club dir>]

Downloads go to the cache dir (kept between runs, delete it to fetch again) and are read as data only.
The spares are real players of the club's 2026-27 squad who are not among its 20 in `roster`: the
forward and the defenceman with the most games this season (then points, then career games). SHL
squads, birth dates, weights, handedness and stats come from shl.se (shl.se lists forwards only as F,
so a spare forward plays the wing of his stick hand); NHL squads from the NHL API's current roster and
its stats. A spare's traits are seeded the way the club's own were (see data/clubs/shl/README.md and
tools/clubs/nhl_clubs.py), on the fourth line's or the third pair's averages a little lower, and his
`age` is set like the rest (W132). A letter the HUD font cannot draw loses its accent (Milošević is written Miloševic). The script writes each club's `spares` list after its `roster`,
replacing the one already there; `nhl_clubs.py` rebuilds the NHL files without them, so run this after it.
"""

import hashlib
import json
import os
import sys
import time
import unicodedata
import urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nhl_clubs  # noqa: E402  (the shared trait table and NHL fetching)

SHL_API = "https://www.shl.se/api/"
SHL_SEASON = "ndcf81nlb3"  # 2026/2027
SHL_SERIES = "qQ9-bb0bzEWUk"  # SHL
SHL_GAME_TYPE = "qQ9-af37Ti40B"  # regular season

SPARE_DROP = 2  # a spare is a little below the fourth line or the third pair he stands in for
SKATER_KEYS = nhl_clubs.SKATER_KEYS
LIMITS = {"shl": (25, 92), "nhl": (nhl_clubs.LOW, nhl_clubs.HIGH)}
LIFT = {"shl": 0, "nhl": nhl_clubs.NHL_LIFT}


def fetch(cache, url):
    name = "".join(ch if ch.isalnum() else "_" for ch in url.split("//", 1)[1])[:180] + ".json"
    path = os.path.join(cache, name)
    if not os.path.exists(path):
        request = urllib.request.Request(url, headers={"User-Agent": "StickAndPuck data tool"})
        with urllib.request.urlopen(request, timeout=60) as response:
            body = response.read()
        with open(path, "wb") as f:
            f.write(body)
        time.sleep(0.2)
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def font_glyphs():
    """The characters the HUD font (art/ui/font.fnt) can draw."""
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "art", "ui", "font.fnt")
    with open(path, encoding="utf-8") as f:
        return {chr(int(line[len("char id="):].split(" ")[0])) for line in f if line.startswith("char id=")}


GLYPHS = font_glyphs()


def drawable(name):
    """The name as the HUD draws it: a letter whose capital the font lacks (Ć) loses its accent (C)."""
    out = []
    for ch in name:
        if ch.upper() in GLYPHS:
            out.append(ch)
        else:
            out.append("".join(c for c in unicodedata.normalize("NFKD", ch) if not unicodedata.combining(c)))
    return "".join(out)


def plain(name):
    text = unicodedata.normalize("NFKD", name.lower())
    return "".join(ch for ch in text if "a" <= ch <= "z")


def jitter(name, salt, spread):
    digest = hashlib.sha256(f"{name}|{salt}".encode("utf-8")).digest()
    return (digest[0] % (2 * spread + 1)) - spread


def age(birth):
    year, month, _ = (int(x) for x in birth.split("-"))
    return 2026 - year - (1 if month > 10 else 0)


def traits(league, full, position, years, pounds, games, goals, assists, shots, hits):
    """Seeded like nhl_clubs.skater_traits, on the fourth line (forwards) or third pair (defence) less SPARE_DROP.
    Per-game rates from fewer than five games count as the league mean."""
    low, high = LIMITS[league]
    line = 2 if position == "D" else 3
    base = [b + LIFT[league] - SPARE_DROP for b in nhl_clubs.BASE[position][line]]
    d = position == "D"
    goal_mean, assist_mean, shot_mean = (0.08, 0.28, 1.6) if d else (0.22, 0.28, 2.0)
    if games >= 5:
        g, a = goals / games, assists / games
        s = shots / games if shots >= 0 else shot_mean
        h = hits / games if hits >= 0 else 1.3
    else:
        g, a, s, h = goal_mean, assist_mean, shot_mean, 1.3
    years = years or 27
    pounds = pounds or 200
    speed = (27 - years) * 0.8 - (pounds - 200) * 0.08
    values = [
        base[0] + speed + jitter(full, "acc", 3),
        base[1] + speed + jitter(full, "top", 3),
        base[2] + (g - goal_mean) * (45 if d else 28) + jitter(full, "accuracy", 3),
        base[3] + (s - shot_mean) * 4 + (pounds - 200) * 0.12 + jitter(full, "power", 3),
        base[4] + (h - 1.3) * 6 + (pounds - 200) * 0.15 + jitter(full, "check", 3),
        base[5] + (a - assist_mean) * 16 + (g - goal_mean) * 8 + jitter(full, "control", 3),
        base[6] + (a - assist_mean) * 20 + (years - 27) * 0.4 + jitter(full, "iq", 3),
    ]
    out = {k: max(low, min(high, int(round(v)))) for k, v in zip(SKATER_KEYS, values)}
    out["goalieReflexes"] = 10
    return out


def entry(number, first, last, position, shoots, years, seeded):
    return {
        "number": number,
        "firstName": drawable(first),
        "lastName": drawable(last),
        "position": position,
        "shoots": shoots,
        **({"age": years} if years else {}),
        "traits": seeded,
    }


def taken(club):
    return {(plain(p["firstName"]), plain(p["lastName"])) for p in club["roster"]}


def shl_spares(cache, club, codes):
    code = club["shortName"]
    if code not in codes:
        raise SystemExit(f"{club['id']}: no SHL team {code} in {sorted(codes)}")
    squad = fetch(cache, f"{SHL_API}sports-v2/athletes/by-team-uuid/{codes[code]}")
    names = taken(club)
    numbers = {p["number"] for p in club["roster"]}
    picks = []
    for group, position_code in (("F", "F"), ("D", "D")):
        candidates = []
        for g in squad:
            if g["positionCode"] != position_code:
                continue
            for p in g["players"]:
                number = int(p.get("jerseyNumber") or 0)
                if (plain(p["firstName"]), plain(p["lastName"])) in names or number in numbers or number <= 0:
                    continue
                p = dict(p, jerseyNumber=number)
                profile = fetch(cache, f"{SHL_API}statistics-v2/athlete/profile-page?playerUuid={p['uuid']}")
                season = {s["field"]: s["value"] for s in profile.get("seasonStats", [])}
                career = {s["field"]: s["value"] for s in profile.get("careerStats", [])}
                candidates.append((p, profile, season, career))
        if not candidates:
            raise SystemExit(f"{club['id']}: no spare {group} on shl.se")
        p, profile, season, career = max(
            candidates,
            key=lambda c: (c[2].get("GP", 0), c[2].get("TP", 0), c[3].get("GP", 0), -c[0].get("jerseyNumber", 0)),
        )
        shoots = "Right" if profile.get("shoots") == "Right" else "Left"
        position = "D" if group == "D" else ("RW" if shoots == "Right" else "LW")
        birth = profile.get("birthDate") or ""
        years = age(birth) if birth else 0
        kilos = (profile.get("weight") or {}).get("value") or 0
        pounds = kilos * 2.2046 if kilos else 0
        full = f"{p['firstName']} {p['lastName']}"
        seeded = traits("shl", full, "D" if group == "D" else ("RW" if shoots == "Right" else "LW"), years, pounds,
                        season.get("GP", 0), season.get("G", 0), season.get("A", 0), -1, -1)
        picks.append(entry(p["jerseyNumber"], p["firstName"], p["lastName"], position, shoots, years, seeded))
    return picks


def shl_teams(cache):
    url = (f"{SHL_API}sports-v2/game-schedule?seasonUuid={SHL_SEASON}&seriesUuid={SHL_SERIES}"
           f"&gameTypeUuid={SHL_GAME_TYPE}&gamePlace=all&played=all")
    found = {}
    for game in fetch(cache, url)["gameInfo"]:
        for side in ("homeTeamInfo", "awayTeamInfo"):
            found[game[side]["code"]] = game[side]["uuid"]
    return found


def nhl_spares(cache, club, seasons):
    abbrev = club["shortName"]
    roster = fetch(cache, f"{nhl_clubs.WEB}roster/{abbrev}/current")
    club_stats = fetch(cache, f"{nhl_clubs.WEB}club-stats/{abbrev}/{nhl_clubs.SEASON}/2")
    games_here = {s["playerId"]: s.get("gamesPlayed", 0) for s in club_stats["skaters"]}
    names = taken(club)
    numbers = {p["number"] for p in club["roster"]}

    def rows(pid, report):
        return [seasons[(report, s)][pid] for s in (nhl_clubs.SEASON, nhl_clubs.LAST_SEASON) if pid in seasons[(report, s)]]

    picks = []
    for group, codes in (("forwards", ("C", "L", "R")), ("defensemen", ("D",))):
        candidates = [
            p for p in roster[group]
            if p["positionCode"] in codes
            and (plain(p["firstName"]["default"]), plain(p["lastName"]["default"])) not in names
            and p.get("sweaterNumber", 0) not in numbers
            and p.get("sweaterNumber", 0) > 0
        ]
        if not candidates:
            raise SystemExit(f"{club['id']}: no spare {group} on the NHL roster")

        def points(p):
            return sum(r.get("points", 0) for r in rows(p["id"], "skater/summary"))

        p = max(candidates, key=lambda p: (games_here.get(p["id"], 0), points(p), -p.get("sweaterNumber", 0)))
        position = {"C": "C", "L": "LW", "R": "RW", "D": "D"}[p["positionCode"]]
        summary = rows(p["id"], "skater/summary")
        realtime = rows(p["id"], "skater/realtime")
        games = sum(r.get("gamesPlayed", 0) for r in summary)
        hit_games = sum(r.get("gamesPlayed", 0) for r in realtime)
        birth = p.get("birthDate", "")
        years = age(birth) if birth else 0
        first, last = p["firstName"]["default"], p["lastName"]["default"]
        hits = sum(r.get("hits", 0) for r in realtime) * games / hit_games if hit_games else -1
        seeded = traits("nhl", f"{first} {last}", position, years, p.get("weightInPounds", 0), games,
                        sum(r.get("goals", 0) for r in summary), sum(r.get("assists", 0) for r in summary),
                        sum(r.get("shots", 0) for r in summary), hits)
        shoots = "Right" if p.get("shootsCatches") == "R" else "Left"
        picks.append(entry(p["sweaterNumber"], first, last, position, shoots, years, seeded))
    return picks


def write(path, club, spares):
    out = {}
    for k, v in club.items():
        if k == "spares":
            continue
        out[k] = v
        if k == "roster":
            out["spares"] = spares
    with open(path, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=2)
        f.write("\n")


def main():
    if len(sys.argv) < 3 or sys.argv[1] not in ("shl", "nhl"):
        raise SystemExit(__doc__)
    league, cache = sys.argv[1], sys.argv[2]
    out = sys.argv[3] if len(sys.argv) > 3 else os.path.join(os.path.dirname(__file__), "..", "..", "data", "clubs", league)
    os.makedirs(cache, exist_ok=True)
    if league == "shl":
        codes = shl_teams(cache)
    else:
        seasons = {
            (report, season): nhl_clubs.season_rows(cache, report, season)
            for report in ("skater/summary", "skater/realtime")
            for season in (nhl_clubs.SEASON, nhl_clubs.LAST_SEASON)
        }
    for name in sorted(os.listdir(out)):
        if not name.endswith(".json"):
            continue
        path = os.path.join(out, name)
        with open(path, encoding="utf-8") as f:
            club = json.load(f)
        spares = shl_spares(cache, club, codes) if league == "shl" else nhl_spares(cache, club, seasons)
        write(path, club, spares)
        listed = ", ".join(f"#{p['number']} {p['firstName']} {p['lastName']} {p['position']} ({p.get('age', '?')})" for p in spares)
        print(f"{club['shortName']} {club['id']}: {listed}")


if __name__ == "__main__":
    main()
