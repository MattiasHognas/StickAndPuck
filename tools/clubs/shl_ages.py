#!/usr/bin/env python3
"""Sets each player's `age` in data/clubs/shl/*.json from the birth dates on shl.se (W132).

    python3 -I tools/clubs/shl_ages.py <cache dir> [<club dir>]

Downloads go to the cache dir (kept between runs, delete it to fetch again) and are read as data only.
Each club's squad on shl.se (the 2026-27 season's teams) is matched to the club file by jersey number
and last name, then by last name alone; the player's shl.se record gives the birth date. The age is the
player's age on 2026-10-31, early in the 2026-27 season, the same date the NHL tool uses. A player shl.se
no longer lists keeps the file as it is and is printed, for a hand-set age.
"""

import json
import os
import sys
import time
import unicodedata
import urllib.request

API = "https://www.shl.se/api/sports-v2/"
SEASON = "ndcf81nlb3"  # 2026/2027
SERIES = "qQ9-bb0bzEWUk"  # SHL
GAME_TYPE = "qQ9-af37Ti40B"  # regular season


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


def age(birth):
    year, month, _ = (int(x) for x in birth.split("-"))
    return 2026 - year - (1 if month > 10 else 0)


def plain(name):
    text = unicodedata.normalize("NFKD", name.lower())
    return "".join(ch for ch in text if "a" <= ch <= "z")


def teams(cache):
    """The SHL teams of the season by code (FHC, LHF, ...), from its schedule."""
    url = f"{API}game-schedule?seasonUuid={SEASON}&seriesUuid={SERIES}&gameTypeUuid={GAME_TYPE}&gamePlace=all&played=all"
    found = {}
    for game in fetch(cache, url)["gameInfo"]:
        for side in ("homeTeamInfo", "awayTeamInfo"):
            info = game[side]
            found[info["code"]] = info["uuid"]
    return found


def main():
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    cache = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(__file__), "..", "..", "data", "clubs", "shl")
    os.makedirs(cache, exist_ok=True)
    codes = teams(cache)
    for name in sorted(os.listdir(out)):
        if not name.endswith(".json"):
            continue
        path = os.path.join(out, name)
        with open(path, encoding="utf-8") as f:
            club = json.load(f)
        code = club["shortName"]
        if code not in codes:
            raise SystemExit(f"{club['id']}: no SHL team {code} in {sorted(codes)}")
        squad = [p for group in fetch(cache, f"{API}athletes/by-team-uuid/{codes[code]}") for p in group["players"]]
        missing = []
        for i, player in enumerate(club["roster"]):
            last = plain(player["lastName"])
            match = [p for p in squad if p.get("jerseyNumber") == player["number"] and plain(p["lastName"]) == last]
            if not match:
                match = [p for p in squad if plain(p["lastName"]) == last and plain(p["firstName"]) == plain(player["firstName"])]
            if not match:
                missing.append(f"#{player['number']} {player['firstName']} {player['lastName']}")
                continue
            details = fetch(cache, f"{API}athlete-details/{match[0]['uuid']}")["athleteData"]
            birth = details.get("dateOfBirth") or ""
            if not birth:
                missing.append(f"#{player['number']} {player['firstName']} {player['lastName']} (no birth date)")
                continue
            entry = {k: v for k, v in player.items() if k not in ("age", "traits")}
            entry["age"] = age(birth)
            entry["traits"] = player["traits"]
            club["roster"][i] = entry
        with open(path, "w", encoding="utf-8") as f:
            json.dump(club, f, ensure_ascii=False, indent=2)
            f.write("\n")
        print(f"{code} {club['id']}: ages set" + (f", not on shl.se: {', '.join(missing)}" if missing else ""))


if __name__ == "__main__":
    main()
