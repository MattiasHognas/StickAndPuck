#!/usr/bin/env python3
"""Builds data/clubs/nhl/*.json, one file per NHL club, from the NHL's public API.

    python3 -I tools/clubs/nhl_clubs.py <cache dir> [<output dir>]

Downloads go to the cache dir (kept between runs, delete it to fetch again) and are read as data only.
Each club's squad is the 20 players it dressed in its latest regular-season game, filled from its
current roster when that game was short of a position. Lines are ordered by average ice time this
season (last game's when a player has fewer than three games). Traits are seeded the same way as the
SHL clubs (see data/clubs/README.md), on an NHL scale a few points above the SHL's.
"""

import hashlib
import json
import os
import sys
import time
import urllib.request

WEB = "https://api-web.nhle.com/v1/"
STATS = "https://api.nhle.com/stats/rest/en/"
SEASON = 20262027
LAST_SEASON = 20252026

# Home kit (primary, secondary, accent, helmet) from each club's official colours. Away kits are
# white with the home colours on the trim. Kits and crests are art task A16; these are what it builds from.
COLOURS = {
    "ANA": ("#fc4c02", "#1a1a1a", "#b9975b"),
    "BOS": ("#1a1a1a", "#ffb81c", "#ffffff"),
    "BUF": ("#003087", "#ffb81c", "#ffffff"),
    "CAR": ("#ce1126", "#1a1a1a", "#ffffff"),
    "CBJ": ("#002654", "#ce1126", "#a4a9ad"),
    "CGY": ("#c8102e", "#f1be48", "#1a1a1a"),
    "CHI": ("#cf0a2c", "#1a1a1a", "#ffffff"),
    "COL": ("#6f263d", "#236192", "#a2aaad"),
    "DAL": ("#006847", "#8f8f8c", "#1a1a1a"),
    "DET": ("#ce1126", "#ffffff", "#ffffff"),
    "EDM": ("#041e42", "#ff4c00", "#ffffff"),
    "FLA": ("#c8102e", "#041e42", "#b9975b"),
    "LAK": ("#1a1a1a", "#a2aaad", "#ffffff"),
    "MIN": ("#154734", "#a6192e", "#eaaa00"),
    "MTL": ("#af1e2d", "#192168", "#ffffff"),
    "NJD": ("#ce1126", "#1a1a1a", "#ffffff"),
    "NSH": ("#ffb81c", "#041e42", "#ffffff"),
    "NYI": ("#00539b", "#f47d30", "#ffffff"),
    "NYR": ("#0038a8", "#ce1126", "#ffffff"),
    "OTT": ("#1a1a1a", "#c52032", "#c2912c"),
    "PHI": ("#f74902", "#1a1a1a", "#ffffff"),
    "PIT": ("#1a1a1a", "#fcb514", "#ffffff"),
    "SEA": ("#001628", "#99d9d9", "#e9072b"),
    "SJS": ("#006d75", "#1a1a1a", "#ea7200"),
    "STL": ("#002f87", "#fcb514", "#041e42"),
    "TBL": ("#002868", "#ffffff", "#ffffff"),
    "TOR": ("#00205b", "#ffffff", "#ffffff"),
    "UTA": ("#1a1a1a", "#69b3e7", "#ffffff"),
    "VAN": ("#00205b", "#00843d", "#ffffff"),
    "VGK": ("#b4975a", "#333f48", "#c8102e"),
    "WPG": ("#041e42", "#ffffff", "#004c97"),
    "WSH": ("#c8102e", "#041e42", "#ffffff"),
}

# How each club plays (a judgement, not official data): forecheck, entry, power play.
TACTICS = {
    "ANA": ("balanced", "carry", "mixed"),
    "BOS": ("aggressive", "dumpAndChase", "umbrella"),
    "BUF": ("balanced", "carry", "oneThreeOne"),
    "CAR": ("aggressive", "dumpAndChase", "umbrella"),
    "CBJ": ("aggressive", "carry", "mixed"),
    "CGY": ("balanced", "dumpAndChase", "umbrella"),
    "CHI": ("balanced", "carry", "mixed"),
    "COL": ("aggressive", "carry", "oneThreeOne"),
    "DAL": ("balanced", "carry", "oneThreeOne"),
    "DET": ("trap", "carry", "mixed"),
    "EDM": ("aggressive", "carry", "oneThreeOne"),
    "FLA": ("aggressive", "dumpAndChase", "oneThreeOne"),
    "LAK": ("trap", "dumpAndChase", "umbrella"),
    "MIN": ("trap", "dumpAndChase", "umbrella"),
    "MTL": ("balanced", "carry", "mixed"),
    "NJD": ("aggressive", "carry", "oneThreeOne"),
    "NSH": ("balanced", "dumpAndChase", "umbrella"),
    "NYI": ("trap", "dumpAndChase", "umbrella"),
    "NYR": ("balanced", "carry", "oneThreeOne"),
    "OTT": ("aggressive", "carry", "mixed"),
    "PHI": ("aggressive", "dumpAndChase", "mixed"),
    "PIT": ("balanced", "carry", "umbrella"),
    "SEA": ("balanced", "dumpAndChase", "mixed"),
    "SJS": ("balanced", "carry", "mixed"),
    "STL": ("trap", "dumpAndChase", "umbrella"),
    "TBL": ("trap", "carry", "oneThreeOne"),
    "TOR": ("balanced", "carry", "oneThreeOne"),
    "UTA": ("balanced", "carry", "mixed"),
    "VAN": ("balanced", "carry", "oneThreeOne"),
    "VGK": ("aggressive", "dumpAndChase", "umbrella"),
    "WPG": ("balanced", "carry", "oneThreeOne"),
    "WSH": ("balanced", "carry", "oneThreeOne"),
}

# Average traits per position and line for the SHL clubs (50 is an average SHL player), as of 2026-10-18.
BASE = {
    "C": [(63, 65, 72, 69, 58, 78, 78), (57, 59, 64, 60, 53, 67, 67), (54, 56, 56, 51, 52, 60, 58), (42, 44, 47, 44, 50, 52, 53)],
    "LW": [(66, 68, 73, 70, 58, 75, 72), (57, 59, 64, 62, 57, 65, 64), (52, 53, 56, 51, 50, 58, 56), (49, 50, 47, 44, 50, 50, 47)],
    "RW": [(66, 67, 74, 69, 58, 76, 72), (57, 58, 65, 61, 55, 65, 63), (51, 53, 56, 52, 54, 57, 55), (46, 48, 48, 45, 51, 49, 48)],
    "D": [(64, 66, 66, 76, 68, 69, 73), (58, 60, 56, 66, 64, 61, 65), (50, 52, 48, 58, 62, 53, 57)],
}
SKATER_KEYS = ("acceleration", "topSpeed", "shotAccuracy", "shotPower", "checking", "puckControl", "hockeyIq")
NHL_LIFT = 7  # an NHL line is this much better than the same SHL line
LOW, HIGH = 30, 95


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


def by_player(rows):
    return {row["playerId"]: row for row in rows}


def season_rows(cache, report, season):
    url = f"{STATS}{report}?limit=-1&cayenneExp=seasonId={season}%20and%20gameTypeId=2"
    return by_player(fetch(cache, url)["data"])


def jitter(name, salt, spread):
    digest = hashlib.sha256(f"{name}|{salt}".encode("utf-8")).digest()
    return (digest[0] % (2 * spread + 1)) - spread


def clamp(value):
    return max(LOW, min(HIGH, int(round(value))))


def minutes(toi):
    m, s = toi.split(":")
    return int(m) + int(s) / 60


def slug(text):
    out = []
    for ch in text.lower():
        if "a" <= ch <= "z" or "0" <= ch <= "9":
            out.append(ch)
        elif out and out[-1] != "_":
            out.append("_")
    return "".join(out).strip("_")


def age(birth):
    year, month, _ = (int(x) for x in birth.split("-"))
    return 2026 - year - (1 if month > 10 else 0)


def per_game(stats, key):
    """A per-game rate blended over this season and the last, weighted by games played."""
    games = sum(s.get("gamesPlayed", 0) for s in stats)
    return (sum(s.get(key, 0) for s in stats) / games) if games else 0.0


def skater_traits(player, position, line, stats, hits):
    full = f"{player['first']} {player['last']}"
    base = [b + NHL_LIFT for b in BASE[position][line]]
    goals, assists, shots = (per_game(stats, k) for k in ("goals", "assists", "shots"))
    hit_rate = per_game(hits, "hits")
    years = age(player["birth"]) if player["birth"] else 27
    pounds = player["weight"] or 200
    d = position == "D"
    goal_mean, assist_mean, shot_mean = (0.08, 0.28, 1.6) if d else (0.22, 0.28, 2.0)
    speed = (27 - years) * 0.8 - (pounds - 200) * 0.08
    acc = base[0] + speed + jitter(full, "acc", 3)
    top = base[1] + speed + jitter(full, "top", 3)
    accuracy = base[2] + (goals - goal_mean) * (45 if d else 28) + jitter(full, "accuracy", 3)
    power = base[3] + (shots - shot_mean) * 4 + (pounds - 200) * 0.12 + jitter(full, "power", 3)
    checking = base[4] + (hit_rate - 1.3) * 6 + (pounds - 200) * 0.15 + jitter(full, "check", 3)
    control = base[5] + (assists - assist_mean) * 16 + (goals - goal_mean) * 8 + jitter(full, "control", 3)
    iq = base[6] + (assists - assist_mean) * 20 + (years - 27) * 0.4 + jitter(full, "iq", 3)
    values = [acc, top, accuracy, power, checking, control, iq]
    traits = {k: clamp(v) for k, v in zip(SKATER_KEYS, values)}
    traits["goalieReflexes"] = 10
    return traits


def goalie_traits(player, starter, stats):
    full = f"{player['first']} {player['last']}"
    saves = sum(s.get("saves", 0) for s in stats)
    against = sum(s.get("shotsAgainst", 0) for s in stats)
    pct = saves / against if against >= 100 else 0.895
    reflexes = 70 + NHL_LIFT + (pct - 0.900) * 400 + (0 if starter else -5) + jitter(full, "reflexes", 2)
    positioning = reflexes + jitter(full, "iq", 3)
    weak = jitter(full, "hand", 1) >= 0  # True: the glove is the weaker hand
    gap = 6 + abs(jitter(full, "gap", 4))
    glove = reflexes - (gap if weak else -abs(jitter(full, "glove", 3)))
    blocker = reflexes - (-abs(jitter(full, "blocker", 3)) if weak else gap)
    rebounds = reflexes + jitter(full, "rebound", 4)
    handling = 58 + jitter(full, "handling", 10)
    return {
        "acceleration": 35,
        "topSpeed": 35,
        "shotAccuracy": 25,
        "shotPower": 30,
        "checking": 25,
        "puckControl": 40,
        "hockeyIq": clamp(positioning),
        "goalieReflexes": clamp(reflexes),
        "goalieGlove": clamp(glove),
        "goalieBlocker": clamp(blocker),
        "goalieReboundControl": clamp(rebounds),
        "goaliePuckHandling": clamp(handling),
    }


def last_game(cache, abbrev):
    games = fetch(cache, f"{WEB}club-schedule-season/{abbrev}/now")["games"]
    done = [g for g in games if g["gameType"] == 2 and g["gameState"] in ("OFF", "FINAL")]
    return done[-1]


def build(cache, row, seasons):
    abbrev = row["teamAbbrev"]["default"]
    roster = fetch(cache, f"{WEB}roster/{abbrev}/current")
    club_stats = fetch(cache, f"{WEB}club-stats/{abbrev}/{SEASON}/2")
    game = last_game(cache, abbrev)
    box = fetch(cache, f"{WEB}gamecenter/{game['id']}/boxscore")
    side = "homeTeam" if box["homeTeam"]["abbrev"] == abbrev else "awayTeam"
    dressed = box["playerByGameStats"][side]

    people = {}
    for group in ("forwards", "defensemen", "goalies"):
        for p in roster[group]:
            people[p["id"]] = {
                "id": p["id"],
                "first": p["firstName"]["default"],
                "last": p["lastName"]["default"],
                "number": p.get("sweaterNumber", 0),
                "pos": p["positionCode"],
                "shoots": p.get("shootsCatches", "L"),
                "birth": p.get("birthDate", ""),
                "weight": p.get("weightInPounds", 0),
            }
    avg_toi = {s["playerId"]: (s["gamesPlayed"], s["avgTimeOnIcePerGame"] / 60) for s in club_stats["skaters"]}
    starts = {g["playerId"]: g.get("gamesStarted", 0) for g in club_stats["goalies"]}

    def ice(pid, fallback):
        games, avg = avg_toi.get(pid, (0, 0.0))
        return avg if games >= 3 else fallback

    def stats_for(pid, report):
        return [seasons[(report, s)][pid] for s in (SEASON, LAST_SEASON) if pid in seasons[(report, s)]]

    def points(pid):
        return sum(s.get("points", 0) for s in stats_for(pid, "skater/summary"))

    picked = {"F": [], "D": [], "G": []}
    for group, key in (("forwards", "F"), ("defense", "D"), ("goalies", "G")):
        for p in dressed[group]:
            pid = p["playerId"]
            person = people.get(pid) or {
                "id": pid,
                "first": p["name"]["default"].split(". ", 1)[0],
                "last": p["name"]["default"].split(". ", 1)[-1],
                "number": p.get("sweaterNumber", 0),
                "pos": p["position"],
                "shoots": "L",
                "birth": "",
                "weight": 0,
            }
            if person["pos"] in ("C", "L", "R") and key == "D":
                person = dict(person, pos="D")
            if person["pos"] == "D" and key == "F":
                person = dict(person, pos="C")
            person["toi"] = ice(pid, minutes(p.get("toi", "0:00")))
            picked[key].append(person)

    # Fill a short position from the rest of the roster (most points first), drop the extras by ice time.
    need = {"F": 12, "D": 6, "G": 2}
    chosen = {pid for group in picked.values() for pid in (p["id"] for p in group)}
    for key, positions in (("F", ("C", "L", "R")), ("D", ("D",)), ("G", ("G",))):
        spare = sorted(
            (p for p in people.values() if p["pos"] in positions and p["id"] not in chosen),
            key=lambda p: (-starts.get(p["id"], 0), -points(p["id"])),
        )
        while len(picked[key]) < need[key] and spare:
            extra = dict(spare.pop(0), toi=0.0)
            picked[key].append(extra)
        picked[key] = sorted(picked[key], key=lambda p: -p["toi"])[: need[key]]
        if len(picked[key]) < need[key]:
            raise SystemExit(f"{abbrev}: only {len(picked[key])} {key}")

    # Forwards into centre, left and right wing, four each; extras move to the thinnest position.
    lists = {"C": [], "LW": [], "RW": []}
    for p in picked["F"]:
        lists[{"C": "C", "L": "LW", "R": "RW"}[p["pos"]]].append(p)
    for k in lists:
        lists[k].sort(key=lambda p: -p["toi"])
    while any(len(v) > 4 for v in lists.values()):
        over = max(lists, key=lambda k: len(lists[k]))
        under = min(lists, key=lambda k: len(lists[k]))
        lists[under].append(lists[over].pop())
        lists[under].sort(key=lambda p: -p["toi"])

    goalies = sorted(picked["G"], key=lambda p: (-starts.get(p["id"], 0), -p["toi"]))
    defence = picked["D"]

    def entry(p, position, traits):
        return {
            "number": p["number"],
            "firstName": p["first"],
            "lastName": p["last"],
            "position": position,
            "shoots": "Right" if p["shoots"] == "R" else "Left",
            "traits": traits,
        }

    squad = []
    for i, g in enumerate(goalies):
        squad.append(entry(g, "G", goalie_traits(g, i == 0, stats_for(g["id"], "goalie/summary"))))
    for line in range(4):
        for position in ("C", "LW", "RW"):
            p = lists[position][line]
            stats = stats_for(p["id"], "skater/summary")
            hits = stats_for(p["id"], "skater/realtime")
            squad.append(entry(p, position, skater_traits(p, position, line, stats, hits)))
        if line < 3:
            for p in defence[line * 2 : line * 2 + 2]:
                stats = stats_for(p["id"], "skater/summary")
                hits = stats_for(p["id"], "skater/realtime")
                squad.append(entry(p, "D", skater_traits(p, "D", line, stats, hits)))

    primary, secondary, accent = COLOURS[abbrev]
    away_secondary = primary if primary != "#ffffff" else secondary
    away_accent = secondary if secondary != "#ffffff" else accent
    forecheck, entry_style, power_play = TACTICS[abbrev]
    club = {
        "id": slug(row["teamCommonName"]["default"]),
        "name": row["teamName"]["default"],
        "shortName": abbrev,
        "league": "NHL",
        "conference": row["conferenceName"],
        "division": row["divisionName"],
        "home": {"primary": primary, "secondary": secondary, "accent": accent, "helmet": primary},
        "away": {"primary": "#ffffff", "secondary": away_secondary, "accent": away_accent, "helmet": primary},
        "tactics": {"forecheck": forecheck, "entry": entry_style, "powerPlay": power_play},
        "roster": squad,
    }
    return club, game["gameDate"]


def main():
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    cache = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(__file__), "..", "..", "data", "clubs", "nhl")
    os.makedirs(cache, exist_ok=True)
    os.makedirs(out, exist_ok=True)
    seasons = {
        (report, season): season_rows(cache, report, season)
        for report in ("skater/summary", "skater/realtime", "goalie/summary")
        for season in (SEASON, LAST_SEASON)
    }
    standings = fetch(cache, f"{WEB}standings/now")["standings"]
    for row in sorted(standings, key=lambda r: r["teamAbbrev"]["default"]):
        club, date = build(cache, row, seasons)
        with open(os.path.join(out, club["id"] + ".json"), "w", encoding="utf-8") as f:
            json.dump(club, f, ensure_ascii=False, indent=2)
            f.write("\n")
        print(f"{club['shortName']} {club['id']}: lineup of {date}, {club['conference']}/{club['division']}")


if __name__ == "__main__":
    main()
