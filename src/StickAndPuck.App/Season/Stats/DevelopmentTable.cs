using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Stats
{
    /// <summary>Every season player's development (W132), saved inside season.json with the stats (an older save
    /// reads with an empty table). Each credited game moves it by <see cref="Step"/>: a young player
    /// (<see cref="YoungAge"/> or under) who played gains, the more the younger he is, the more ice time he got
    /// next to the club's other players at his position and the better his carry-over form; a veteran
    /// (<see cref="VeteranAge"/> or over) slowly loses, the faster the older he is, slower while in form. The
    /// whole points, scaled by <see cref="RuleSet.DevelopmentScale"/>, move every trait in a season match the
    /// same way the form nudge does (<see cref="MatchSetup.Form"/>, so a replay keeps them) and show as "+2" by
    /// the name on the stats page. A player with no age in his club file never moves.</summary>
    public sealed class DevelopmentTable
    {
        /// <summary>The oldest age that still develops.</summary>
        public const int YoungAge = 23;

        /// <summary>The youngest age that declines.</summary>
        public const int VeteranAge = 31;

        /// <summary>The most trait points a season can add.</summary>
        public const int MaxGain = 5;

        /// <summary>The most trait points a season can take.</summary>
        public const int MaxLoss = 4;

        /// <summary>The unscaled points an 18-year-old with an ordinary share of the ice and level form gains a game:
        /// about four over a 52-game SHL season.</summary>
        public const double PerGame = 0.08;

        /// <summary>Gets or sets every player with a development, in no particular order.</summary>
        public ICollection<PlayerDevelopment> Players { get; set; } = [];

        /// <summary>Gets a player's unscaled development; 0 for a player with none.</summary>
        public double Of(string club, int number)
        {
            return Players.FirstOrNone(p => p.Is(club, number)).Select(p => p.Value).Or(0);
        }

        /// <summary>Gets the trait points a player's development moves under these rules: -4 to +5, 0 with development off.</summary>
        public int Points(string club, int number, RuleSet rules)
        {
            return Scaled(Of(club, number), rules);
        }

        /// <summary>Gets the whole trait points an unscaled development is worth under these rules, rounded towards 0.</summary>
        public static int Scaled(double value, RuleSet rules)
        {
            return Math.Clamp(
                (int)Math.Truncate(value * rules.DevelopmentScale),
                -MaxLoss,
                MaxGain
            );
        }

        /// <summary>Moves the development of everyone in a credited game, after <paramref name="form"/> took the game in.</summary>
        public void Add(string key, MatchStatSheet sheet, FormTable form)
        {
            foreach (MatchStatLine line in sheet.Lines)
            {
                double step = Step(line, sheet, form.Of(line.Club, line.Number));
                if (step == 0)
                {
                    continue;
                }

                if (
                    !Players
                        .FirstOrNone(p => p.Is(line.Club, line.Number))
                        .TryGet(out PlayerDevelopment d)
                )
                {
                    d = new PlayerDevelopment { Club = line.Club, Number = line.Number };
                    Players.Add(d);
                }

                d.Before = d.Value;
                d.Game = key;
                d.Value = Math.Clamp(d.Value + step, -MaxLoss, MaxGain);
            }
        }

        /// <summary>How far one game moves a player, in unscaled trait points: for a young player who played,
        /// <see cref="PerGame"/> times his youth (1 at 18 down to a quarter at 23), his ice time over the
        /// average of the club's others at his position (a quarter to double) and his form (half at -5, half
        /// again at +5); for a veteran, played or not, minus <see cref="PerGame"/> times an eighth a year over 30,
        /// less while in form; 0 for everyone else.</summary>
        public static double Step(MatchStatLine line, MatchStatSheet sheet, double form)
        {
            if (line.Age <= 0)
            {
                return 0;
            }

            if (line.Age <= YoungAge)
            {
                if (!line.Played)
                {
                    return 0;
                }

                double youth = Math.Clamp((YoungAge + 1 - line.Age) / 6.0, 0.25, 1);
                return PerGame * youth * Ice(line, sheet) * (1 + (Math.Clamp(form, -5, 5) / 10));
            }

            return line.Age >= VeteranAge
                ? -PerGame
                    * (line.Age - (VeteranAge - 1))
                    / 8
                    * Math.Clamp(1 - (form / 10), 0.5, 1.5)
                : 0;
        }

        /// <summary>A player's ice time over the average of the club's players at his position who played, a
        /// quarter to double; 1 when the sheet has no ice times.</summary>
        public static double Ice(MatchStatLine line, MatchStatSheet sheet)
        {
            List<float> mates =
            [
                .. sheet
                    .Lines.Where(l =>
                        l.Played
                        && l.Side == line.Side
                        && l.Position == line.Position
                        && l.IceTime > 0
                    )
                    .Select(l => l.IceTime),
            ];
            return line.IceTime > 0 && mates.Count > 0
                ? Math.Clamp(line.IceTime / mates.Average(), 0.25, 2)
                : 1;
        }

        /// <summary>Gets the development points of a club's players under these rules, by <see cref="FormTable.Key"/>, leaving out the zeros.</summary>
        public IReadOnlyDictionary<string, int> Nudges(ClubData club, RuleSet rules)
        {
            return club
                .Roster.Select(p => p.Number)
                .Distinct()
                .Select(n => (Key: FormTable.Key(club.Id, n), Points: Points(club.Id, n, rules)))
                .Where(x => x.Points != 0)
                .ToDictionary(x => x.Key, x => x.Points, StringComparer.Ordinal);
        }

        /// <summary>Adds the development points of a club's players to a match's nudges, by <see cref="FormTable.Key"/>.</summary>
        public void AddNudges(ClubData club, RuleSet rules, IDictionary<string, int> nudges)
        {
            foreach ((string key, int points) in Nudges(club, rules))
            {
                nudges[key] = nudges.TryGetValue(key, out int n) ? n + points : points;
            }
        }
    }
}
