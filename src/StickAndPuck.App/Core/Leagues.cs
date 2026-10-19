namespace StickAndPuck.App.Core
{
    /// <summary>League choice (W117): what a <see cref="League"/> brings with it. Its clubs come from
    /// <see cref="Clubs.Load(League)"/>, its rules default to <see cref="Preset"/>, and its season and arcade
    /// saves sit beside the SHL ones under <see cref="SavePath(League, string)"/>, so an SHL save from before
    /// league choice keeps working as the SHL save.</summary>
    public static class Leagues
    {
        /// <summary>Gets both leagues, SHL first.</summary>
        public static IReadOnlyList<League> All { get; } = [League.Shl, League.Nhl];

        /// <summary>Gets the league's name as the menus show it.</summary>
        public static string Name(League league)
        {
            return Text.Get(league == League.Nhl ? Text.Keys.LeagueNhl : Text.Keys.LeagueShl);
        }

        /// <summary>Gets the rule preset a league plays by default.</summary>
        public static RulePreset Preset(League league)
        {
            return league == League.Nhl ? RulePreset.Nhl : RulePreset.Shl;
        }

        /// <summary>Gets the other league, one step along with left or right.</summary>
        public static League Next(League league, int dir)
        {
            int i = (((All.ToList().IndexOf(league) + dir) % All.Count) + All.Count) % All.Count;
            return All[i];
        }

        /// <summary>Gets the league a list of clubs plays in: the first club's, SHL for no clubs.</summary>
        public static League Of(IReadOnlyList<ClubData> clubs)
        {
            return clubs.Count > 0 ? clubs[0].League : League.Shl;
        }

        /// <summary>Gets a league's save file beside the SHL one: the SHL save itself, or for the NHL the same
        /// name with "_nhl" before the extension (season.json gives season_nhl.json).</summary>
        public static string SavePath(League league, string shlPath)
        {
            if (league == League.Shl)
            {
                return shlPath;
            }

            string name =
                Path.GetFileNameWithoutExtension(shlPath) + "_nhl" + Path.GetExtension(shlPath);
            string folder = Path.GetDirectoryName(shlPath) ?? "";
            return folder.Length > 0 ? Path.Combine(folder, name) : name;
        }

        /// <summary>Parses "shl" or "nhl", in any case, or None.</summary>
        public static Maybe<League> Parse(string name)
        {
            return name.Trim().ToLowerInvariant() switch
            {
                "shl" => League.Shl,
                "nhl" => League.Nhl,
                _ => Maybe.None<League>(),
            };
        }

        /// <summary>Gets the league SAP_LEAGUE names, the SHL when it is unset: the league a headless run loads
        /// its clubs from (SAP_LEAGUE=nhl plays the scenes and the screens with the 32 NHL clubs).</summary>
        public static League FromEnvironment()
        {
            string named = Env.Get("SAP_LEAGUE");
            return named.Trim().Length == 0 ? League.Shl
                : Parse(named).TryGet(out League league) ? league
                : throw new InvalidOperationException($"SAP_LEAGUE={named}: use shl or nhl");
        }

        /// <summary>Gets the league the season, playoff and arcade scenes play: SAP_LEAGUE=shl|nhl, else the
        /// league of an NHL club named by SAP_CLUB or SAP_HOME, else <paramref name="fallback"/>.</summary>
        public static League ForScenes(League fallback)
        {
            if (Env.Get("SAP_LEAGUE").Trim().Length > 0)
            {
                return FromEnvironment();
            }

            foreach (string variable in (string[])["SAP_CLUB", "SAP_HOME"])
            {
                string id = Env.Get(variable);
                if (
                    id.Length > 0
                    && Clubs
                        .LoadNhl()
                        .Any(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
                )
                {
                    return League.Nhl;
                }
            }
            return fallback;
        }

        /// <summary>Gets the clubs a dev scene plays with: <paramref name="shl"/> (the clubs the game loaded),
        /// or the other league's clubs when <see cref="ForScenes"/> picks that league.</summary>
        public static IReadOnlyList<ClubData> ClubsFromEnvironment(IReadOnlyList<ClubData> shl)
        {
            League league = ForScenes(Of(shl));
            return league == Of(shl) ? shl : Clubs.Load(league);
        }
    }
}
