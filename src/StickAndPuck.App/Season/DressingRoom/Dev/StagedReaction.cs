using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Stats;

namespace StickAndPuck.App.Season.DressingRoom.Dev
{
    /// <summary>Stages the human's last season game as one the coach reacts to, for the dressing room capture
    /// (SAP_SCREEN=dressingroom SAP_REACTION=hattrick, shutout, blownlead or lostfight): the game's
    /// result is rewritten and a played sheet saved for it, as if the human had just played it.</summary>
    internal static class StagedReaction
    {
        /// <summary>The situations a capture can stage.</summary>
        public const string Kinds = "hattrick, shutout, blownlead, lostfight";

        /// <summary>Rewrites the human's last regular-season game; false for an unknown kind or no game.</summary>
        public static bool Stage(SeasonState season, IReadOnlyList<ClubData> clubs, string kind)
        {
            string user = season.UserClub;
            Maybe<Fixture> last = season
                .PlayedBy(user)
                .OrderByDescending(f => f.Number)
                .FirstOrNone(_ => true);
            if (!last.TryGet(out Fixture game))
            {
                return false;
            }

            (int own, int other, ResultKind result) = kind switch
            {
                "hattrick" => (4, 2, ResultKind.Regulation),
                "shutout" => (3, 0, ResultKind.Regulation),
                "blownlead" => (2, 3, ResultKind.Regulation),
                "lostfight" => (1, 2, ResultKind.Regulation),
                _ => (-1, -1, ResultKind.Regulation),
            };
            Maybe<ClubData> home = clubs.FirstOrNone(c =>
                string.Equals(c.Id, game.Home, StringComparison.Ordinal)
            );
            Maybe<ClubData> away = clubs.FirstOrNone(c =>
                string.Equals(c.Id, game.Away, StringComparison.Ordinal)
            );
            if (own < 0 || !home.TryGet(out ClubData h) || !away.TryGet(out ClubData a))
            {
                return false;
            }

            bool atHome = string.Equals(game.Home, user, StringComparison.Ordinal);
            FixtureResult score = atHome
                ? new FixtureResult(own, other, result)
                : new FixtureResult(other, own, result);
            season.Games[season.Games.IndexOf(game)] = game with { Result = score };
            MatchStatSheet sheet = SyntheticStats.Sheet(h, a, score, season.GameSeed(game.Number));
            Side side = atHome ? Side.Home : Side.Away;
            List<MatchStatLine> ours = [.. sheet.Lines.Where(l => l.Side == side && !l.IsGoalie)];
            switch (kind)
            {
                case "hattrick":
                    MatchStatLine star = ours.OrderByDescending(l => l.Goals).First();
                    foreach (MatchStatLine l in ours)
                    {
                        l.Goals = 0;
                    }

                    star.Goals = 3;
                    ours.First(l => l != star).Goals = 1;
                    break;
                case "blownlead":
                    if (atHome)
                    {
                        sheet.HomeMaxLead = 2;
                    }
                    else
                    {
                        sheet.AwayMaxLead = 2;
                    }

                    break;
                case "lostfight":
                    ours.OrderByDescending(l => l.Hits).First().FightsLost = 1;
                    break;
                default:
                    break;
            }

            season.Stats.LastPlayed = sheet;
            season.Stats.LastPlayedKey = SeasonStats.GameKey(game.Number);
            return true;
        }
    }
}
