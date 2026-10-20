using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Schedule;

namespace StickAndPuck.App.Season.Trades
{
    /// <summary>The trade deadline (W135). Two thirds of the way through the regular season (game 35 of the SHL's
    /// 52, game 55 of the NHL's 82) the AI clubs in the bottom half of the table, sellers by then, may
    /// offer the human's club a player-for-player trade: one or two offers (<see cref="RuleSet.TradeOffers"/>),
    /// each for a skater of the same position and about the same value (<see cref="MinGap"/> to
    /// <see cref="MaxGap"/> overall points) but a different kind of player (<see cref="Profile"/>): a
    /// sniper for a grinder, a playmaker for a speedster. Neither player may be injured, suspended or
    /// already traded. The offers come from a stream fixed by the season seed, so a season gets the
    /// same offers every time. Unanswered offers lapse when the next game is played. An accepted offer
    /// becomes a <see cref="Trade"/> in the season: the players swap roster slots for the rest of the
    /// season, and a number the arriving player cannot keep is changed (<see cref="FreeNumber"/>).</summary>
    public static class TradeDesk
    {
        /// <summary>The fewest overall points the offered player may be below the one asked for.</summary>
        public const int MinGap = -2;

        /// <summary>The most overall points the offered player may be above the one asked for.</summary>
        public const int MaxGap = 4;

        /// <summary>How many games after the deadline game offers can still come, for a save from before it.</summary>
        private const int Window = 2;

        /// <summary>The stream name of the offers.</summary>
        private const string Stream = "trades";

        /// <summary>Gets the human club's game after which the offers come: two thirds of its regular season
        /// (35 of 52 in the SHL, 55 of 82 in the NHL).</summary>
        public static int DeadlineGame(SeasonState season)
        {
            int games = season.Games.Count(g => g.Involves(season.UserClub));
            return (int)Math.Round(games * 35.0 / 52.0, MidpointRounding.AwayFromZero);
        }

        /// <summary>Gets a skater's overall value, 0 to 100: the average of his seven skating traits.</summary>
        public static int Value(PlayerData player)
        {
            Traits t = player.Traits;
            double sum =
                t.Acceleration
                + t.TopSpeed
                + t.ShotAccuracy
                + t.ShotPower
                + t.Checking
                + t.PuckControl
                + t.HockeyIq;
            return (int)Math.Round(sum / 7.0, MidpointRounding.AwayFromZero);
        }

        /// <summary>Gets what a skater is best at: his shot, his speed, his checking or his hands and head
        /// (ties go to the first of those).</summary>
        public static TradeProfile Profile(PlayerData player)
        {
            Traits t = player.Traits;
            (TradeProfile Profile, double Score)[] scores =
            [
                (TradeProfile.Sniper, (t.ShotAccuracy + t.ShotPower) / 2.0),
                (TradeProfile.Speedster, (t.Acceleration + t.TopSpeed) / 2.0),
                (TradeProfile.Grinder, t.Checking),
                (TradeProfile.Playmaker, (t.PuckControl + t.HockeyIq) / 2.0),
            ];
            (TradeProfile best, double top) = scores[0];
            foreach ((TradeProfile p, double score) in scores.Skip(1))
            {
                if (score > top)
                {
                    (best, top) = (p, score);
                }
            }

            return best;
        }

        /// <summary>Gets a value indicating whether two players make a fair offer: the same skating position, the
        /// offered player within <see cref="MinGap"/> to <see cref="MaxGap"/> of the one asked for, and a
        /// different kind of player.</summary>
        public static bool Fits(PlayerData give, PlayerData get)
        {
            int gap = Value(get) - Value(give);
            return give.Position == get.Position
                && give.Position != PlayingPosition.G
                && gap >= MinGap
                && gap <= MaxGap
                && Profile(give) != Profile(get);
        }

        /// <summary>After each of the human's regular-season games (<see cref="SeasonSession.Finish"/>): open offers
        /// lapse once a game has been played since they came, and at the deadline game the offers are made.</summary>
        public static void AfterGame(SeasonState season, IReadOnlyList<ClubData> clubs)
        {
            int played = season.PlayedBy(season.UserClub).Count();
            foreach (
                TradeOffer lapsed in season.Trades.Offers.Where(o => o.IsOpen && played > o.Game)
            )
            {
                lapsed.Answer = TradeAnswer.Declined;
            }

            RuleSet rules = season.Rules.Or(RuleSet.Shl);
            int deadline = DeadlineGame(season);
            if (
                !rules.TradeDeadline
                || season.Trades.Held
                || played < deadline
                || played > deadline + Window
                || !season.NextGame.HasValue
            )
            {
                return;
            }

            season.Trades.Held = true;
            foreach (TradeOffer offer in Offers(season, clubs, rules))
            {
                season.Trades.Offers.Add(offer);
            }
        }

        /// <summary>Works out the deadline's offers to the human's club, the same for a season every time: from AI
        /// clubs in the bottom half of the table, at most one each, each for a different player.</summary>
        public static IReadOnlyList<TradeOffer> Offers(
            SeasonState season,
            IReadOnlyList<ClubData> clubs,
            RuleSet rules
        )
        {
            IReadOnlyList<ClubData> rosters = season.Rosters(clubs);
            if (!Find(rosters, season.UserClub).TryGet(out ClubData user))
            {
                return [];
            }

            int played = season.PlayedBy(season.UserClub).Count();
            Random dice = Seeds.Dice(Seeds.For(season.Seed, Stream, 135));
            int count = rules.TradeOffers switch
            {
                TradeFrequency.Rare => 1,
                TradeFrequency.Often => 2,
                TradeFrequency.Normal => 1 + dice.Next(2),
                _ => throw new NotSupportedException("no offer count for " + rules.TradeOffers),
            };

            IReadOnlyList<StandingRow> table = season.Table;
            List<string> sellers =
            [
                .. table
                    .Skip(table.Count / 2)
                    .Select(r => r.ClubId)
                    .Where(id => !string.Equals(id, season.UserClub, StringComparison.Ordinal)),
            ];
            for (int i = sellers.Count - 1; i > 0; i--)
            {
                int j = dice.Next(i + 1);
                (sellers[i], sellers[j]) = (sellers[j], sellers[i]);
            }

            List<TradeOffer> offers = [];
            HashSet<int> asked = [];
            foreach (string id in sellers)
            {
                if (offers.Count >= count)
                {
                    break;
                }

                if (!Find(rosters, id).TryGet(out ClubData seller))
                {
                    continue;
                }

                List<(PlayerData Give, PlayerData Get)> pairs =
                [
                    .. from give in user.Roster
                    where !asked.Contains(give.Number) && Available(season, user.Id, give)
                    from get in seller.Roster
                    where Available(season, seller.Id, get) && Fits(give, get)
                    orderby give.Number, get.Number
                    select (give, get),
                ];
                if (pairs.Count == 0)
                {
                    continue;
                }

                (PlayerData g, PlayerData r) = pairs[dice.Next(pairs.Count)];
                _ = asked.Add(g.Number);
                offers.Add(
                    new TradeOffer
                    {
                        Club = seller.Id,
                        Position = g.Position,
                        Give = g.Number,
                        GiveName = Name(g),
                        Get = r.Number,
                        GetName = Name(r),
                        Game = played,
                    }
                );
            }

            return offers;
        }

        /// <summary>Gets the two players of an offer in the rosters as they stand, or None when one has gone.</summary>
        public static Maybe<(PlayerData Give, PlayerData Get)> Players(
            SeasonState season,
            IReadOnlyList<ClubData> clubs,
            TradeOffer offer
        )
        {
            IReadOnlyList<ClubData> rosters = season.Rosters(clubs);
            return
                Find(rosters, season.UserClub).TryGet(out ClubData user)
                && Find(rosters, offer.Club).TryGet(out ClubData seller)
                && user.Roster.FirstOrNone(p => p.Number == offer.Give).TryGet(out PlayerData give)
                && seller.Roster.FirstOrNone(p => p.Number == offer.Get).TryGet(out PlayerData get)
                && give.Position == get.Position
                ? (give, get)
                : Maybe.None<(PlayerData, PlayerData)>();
        }

        /// <summary>Accepts an open offer: the trade is made, with a free number for each arriving player, and
        /// kept in the season. None (and the offer declined) when it is no longer open or a player has gone.</summary>
        public static Maybe<Trade> Accept(
            SeasonState season,
            IReadOnlyList<ClubData> clubs,
            TradeOffer offer
        )
        {
            if (!offer.IsOpen)
            {
                return Maybe.None<Trade>();
            }

            IReadOnlyList<ClubData> rosters = season.Rosters(clubs);
            if (
                !Players(season, clubs, offer).TryGet(out (PlayerData Give, PlayerData Get) pair)
                || !Find(rosters, season.UserClub).TryGet(out ClubData user)
                || !Find(rosters, offer.Club).TryGet(out ClubData seller)
                || !Available(season, user.Id, pair.Give)
                || !Available(season, seller.Id, pair.Get)
            )
            {
                offer.Answer = TradeAnswer.Declined;
                return Maybe.None<Trade>();
            }

            Trade trade = new(
                user.Id,
                pair.Give.Number,
                FreeNumber(season.Trades, seller, pair.Give.Number),
                seller.Id,
                pair.Get.Number,
                FreeNumber(season.Trades, user, pair.Get.Number)
            );
            season.Trades.Deals.Add(trade);
            offer.Answer = TradeAnswer.Accepted;
            return trade;
        }

        /// <summary>Declines an open offer.</summary>
        public static void Decline(TradeOffer offer)
        {
            if (offer.IsOpen)
            {
                offer.Answer = TradeAnswer.Declined;
            }
        }

        /// <summary>Gets the number a player arriving at a club wears: his own when nobody there wears it and no
        /// player who left the club this season wore it (so his line in the stats is his own), else the
        /// nearest free number to it, above before below, from 1 to 99.</summary>
        public static int FreeNumber(SeasonTrades trades, ClubData club, int wanted)
        {
            HashSet<int> taken = [.. club.Roster.Select(p => p.Number)];
            foreach (Trade t in trades.Deals)
            {
                if (string.Equals(t.Club, club.Id, StringComparison.Ordinal))
                {
                    _ = taken.Add(t.Number);
                }

                if (string.Equals(t.OtherClub, club.Id, StringComparison.Ordinal))
                {
                    _ = taken.Add(t.OtherNumber);
                }
            }

            bool Free(int n)
            {
                return n is >= 1 and <= 99 && !taken.Contains(n);
            }

            for (int d = 0; d < 99; d++)
            {
                if (Free(wanted + d))
                {
                    return wanted + d;
                }

                if (Free(wanted - d))
                {
                    return wanted - d;
                }
            }

            return wanted;
        }

        /// <summary>Gets a value indicating whether a player can be traded: a skater, not injured or suspended, and
        /// not one who already changed clubs this season.</summary>
        private static bool Available(SeasonState season, string club, PlayerData player)
        {
            return player.Position != PlayingPosition.G
                && !season.Stats.Absences.IsOut(club, player.Number)
                && !season.Trades.Deals.Any(t =>
                    (
                        string.Equals(t.Club, club, StringComparison.Ordinal)
                        && t.OtherNewNumber == player.Number
                    )
                    || (
                        string.Equals(t.OtherClub, club, StringComparison.Ordinal)
                        && t.NewNumber == player.Number
                    )
                );
        }

        private static string Name(PlayerData p)
        {
            return p.LastName;
        }

        private static Maybe<ClubData> Find(IReadOnlyList<ClubData> clubs, string id)
        {
            return clubs.FirstOrNone(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        }
    }
}
