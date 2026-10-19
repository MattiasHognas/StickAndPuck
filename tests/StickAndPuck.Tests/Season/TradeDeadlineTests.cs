using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Replay;
using StickAndPuck.App.Season.DressingRoom;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Trades;
using StickAndPuck.Tests.Support;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.Tests.Season
{
    /// <summary>
    /// The trade deadline (W135): the rule presets, when the deadline comes, the offers it makes (sellers in the
    /// bottom half, the same position, a value within range and a different kind of player, the same for
    /// a seed), accepting and declining, the traded rosters in the season's match setups, replays and save,
    /// the line slots kept, the numbers that would clash changed, and the dressing room news.
    /// </summary>
    public sealed class TradeDeadlineTests : CheckedTest
    {
        private static readonly RuleSet TwoOffers = RuleSet.Shl with
        {
            TradeOffers = TradeFrequency.Often,
        };

        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-tradetest-{Environment.ProcessId}-{Guid.NewGuid():N}"
            )
        );

        private static IReadOnlyList<ClubData> Shl => WorldBuilder.Clubs;

        private string SavePath => Path.Combine(folder, "season.json");

        [After(Test)]
        public void EndSession()
        {
            app.Season.End();
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        private static string S(FormattableString s)
        {
            return s.ToString(CultureInfo.InvariantCulture);
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }

        private static T Must<T>(Maybe<T> maybe)
            where T : notnull
        {
            return maybe.TryGet(out T value)
                ? value
                : throw new InvalidOperationException("expected a value");
        }

        private static ClubData Club(IReadOnlyList<ClubData> clubs, string id)
        {
            return clubs.First(c => Same(c.Id, id));
        }

        /// <summary>A roster as text: every slot's position, number and name, in order.</summary>
        private static string Roster(ClubData club)
        {
            return string.Join(
                ',',
                club.Roster.Select(p =>
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{p.Position}#{p.Number}{p.LastName}"
                    )
                )
            );
        }

        /// <summary>Gets a value indicating whether every player of a club wears his own number.</summary>
        private static bool UniqueNumbers(ClubData club)
        {
            return new HashSet<int>(club.Roster.Select(p => p.Number)).Count == club.Roster.Count;
        }

        private static int Played(SeasonState season)
        {
            return season.PlayedBy(season.UserClub).Count();
        }

        private static MatchSetup Next(SeasonSession session)
        {
            return session.NextSetup(InputDevice.None) is NextMatch.Play play
                ? play.Setup
                : throw new InvalidOperationException("expected a match to play");
        }

        /// <summary>A season settled with "sim this game" up to its trade deadline.</summary>
        private SeasonState ToDeadline(int seed, RuleSet rules)
        {
            SeasonState season = app.Season.StartNew(Shl, Shl[0].Id, seed, SavePath);
            season.Rules = rules;
            while (!season.Trades.Held && app.Season.SimCurrentGame().HasValue)
            {
                // one game at a time, as the season screens do
            }

            return season;
        }

        [Test]
        public void PresetsAndRows()
        {
            Check(
                RuleSet.Shl.TradeDeadline
                    && RuleSet.Shl.TradeOffers == TradeFrequency.Normal
                    && RuleSet.Nhl.TradeDeadline
                    && RuleSet.Nhl.TradeOffers == TradeFrequency.Often
                    && !RuleSet.Arcade.TradeDeadline,
                "SHL and NHL hold a trade deadline (the NHL a busier one), Arcade none"
            );
            foreach (
                string key in new[] { nameof(RuleSet.TradeDeadline), nameof(RuleSet.TradeOffers) }
            )
            {
                Check(
                    RuleRows.All.Any(r => Same(r.Key, key) && Same(r.Group, K.RuleGroupSeason)),
                    S($"{key} is a row in the SEASON group")
                );
            }
        }

        [Test]
        public void DeadlineIsTwoThirdsIn()
        {
            Check(
                TradeDesk.DeadlineGame(SeasonState.New(Shl, Shl[0].Id, 1)) == 35,
                "the SHL deadline is after game 35 of 52"
            );
            IReadOnlyList<ClubData> nhl = Clubs.LoadNhl();
            Check(
                TradeDesk.DeadlineGame(SeasonState.New(nhl, nhl[0].Id, 1)) == 55,
                "the NHL deadline is after game 55 of 82"
            );
        }

        [Test]
        public void OffersAreFairAndFromSellers()
        {
            int total = 0;
            foreach (int seed in new[] { 7, 135, 2026 })
            {
                SeasonState season = ToDeadline(seed, TwoOffers);
                int played = Played(season);
                Check(
                    season.Trades.Held && played == TradeDesk.DeadlineGame(season),
                    S($"seed {seed}: the deadline comes after game {played}")
                );
                IList<TradeOffer> offers = season.Trades.Offers;
                Check(
                    offers.Count is >= 1 and <= 2,
                    S($"seed {seed}: one or two offers ({offers.Count})")
                );
                Check(
                    new HashSet<string>(offers.Select(o => o.Club), StringComparer.Ordinal).Count
                        == offers.Count
                        && new HashSet<int>(offers.Select(o => o.Give)).Count == offers.Count,
                    S($"seed {seed}: each offer from another club, for another player")
                );
                foreach (TradeOffer o in offers)
                {
                    CheckOffer(season, o, seed);
                }

                total += offers.Count;
                app.Season.End();
            }

            Check(total >= 4, S($"most seasons get both offers with the busy setting ({total})"));
        }

        private void CheckOffer(SeasonState season, TradeOffer o, int seed)
        {
            List<string> bottom =
            [
                .. season.Table.Skip(season.Table.Count / 2).Select(r => r.ClubId),
            ];
            (PlayerData give, PlayerData get) = Must(TradeDesk.Players(season, Shl, o));
            int gap = TradeDesk.Value(get) - TradeDesk.Value(give);
            Check(
                bottom.Contains(o.Club, StringComparer.Ordinal) && !Same(o.Club, season.UserClub),
                S($"seed {seed}: {o.Club} is a seller in the bottom half")
            );
            Check(
                give.Position == get.Position
                    && give.Position == o.Position
                    && give.Position != PlayingPosition.G,
                S($"seed {seed}: {give.LastName} and {get.LastName} are skaters of one position")
            );
            Check(
                gap is >= TradeDesk.MinGap and <= TradeDesk.MaxGap,
                S(
                    $"seed {seed}: the values are close ({TradeDesk.Value(give)} for {TradeDesk.Value(get)})"
                )
            );
            Check(
                TradeDesk.Profile(give) != TradeDesk.Profile(get),
                S($"seed {seed}: a twist, {TradeDesk.Profile(get)} for {TradeDesk.Profile(give)}")
            );
            Check(
                !season.Stats.Absences.IsOut(season.UserClub, give.Number)
                    && !season.Stats.Absences.IsOut(o.Club, get.Number)
                    && o.IsOpen
                    && o.Game == Played(season),
                S($"seed {seed}: fit players, the offer open")
            );
            Check(
                Same(give.LastName, o.GiveName) && Same(get.LastName, o.GetName),
                S($"seed {seed}: the offer names its players")
            );
        }

        [Test]
        public void OffersRepeatForASeed()
        {
            SeasonState first = ToDeadline(99, RuleSet.Shl);
            List<string> a = [.. first.Trades.Offers.Select(o => S($"{o.Club}:{o.Give}>{o.Get}"))];
            app.Season.End();
            SeasonState second = ToDeadline(99, RuleSet.Shl);
            List<string> b = [.. second.Trades.Offers.Select(o => S($"{o.Club}:{o.Give}>{o.Get}"))];
            Check(
                a.Count > 0 && a.SequenceEqual(b, StringComparer.Ordinal),
                S($"the same seed makes the same offers ({string.Join(' ', a)})")
            );
            Check(
                TradeDesk
                    .Offers(second, Shl, RuleSet.Shl)
                    .Select(o => S($"{o.Club}:{o.Give}>{o.Get}"))
                    .SequenceEqual(b, StringComparer.Ordinal),
                "working them out again gives the same offers"
            );
        }

        [Test]
        public void NoOffersWithTheRuleOff()
        {
            SeasonState off = app.Season.StartNew(Shl, Shl[0].Id, Maybe.Some(5), SavePath);
            off.Rules = RuleSet.Shl with { TradeDeadline = false };
            int deadline = TradeDesk.DeadlineGame(off);
            while (Played(off) < deadline + 3 && app.Season.SimCurrentGame().HasValue)
            {
                // past the deadline
            }

            Check(
                !off.Trades.Held && off.Trades.Offers.Count == 0,
                "no deadline and no offers with the rule off"
            );
        }

        [Test]
        public void AcceptSwapsTheRosters()
        {
            SeasonState season = ToDeadline(135, TwoOffers);
            TradeOffer offer = season.Trades.Offers[0];
            ClubData userBefore = Club(app.Season.Clubs, season.UserClub);
            ClubData sellerBefore = Club(app.Season.Clubs, offer.Club);
            (PlayerData give, PlayerData get) = Must(TradeDesk.Players(season, Shl, offer));
            int slotGive = userBefore.Roster.ToList().IndexOf(give);
            int slotGet = sellerBefore.Roster.ToList().IndexOf(get);

            Trade trade = Must(app.Season.AnswerTrade(offer, accept: true));
            Check(
                offer.Answer == TradeAnswer.Accepted && season.Trades.Deals.Count == 1,
                "the offer is accepted and the trade kept"
            );

            ClubData user = Club(app.Season.Clubs, season.UserClub);
            ClubData seller = Club(app.Season.Clubs, offer.Club);
            Check(
                Same(user.Roster[slotGive].LastName, get.LastName)
                    && user.Roster[slotGive].Number == trade.OtherNewNumber
                    && user.Roster[slotGive].Traits == get.Traits
                    && Same(seller.Roster[slotGet].LastName, give.LastName)
                    && seller.Roster[slotGet].Number == trade.NewNumber,
                "each player takes the other's roster slot with his new number"
            );
            Check(
                user.Roster.Select(p => p.Position)
                    .SequenceEqual(userBefore.Roster.Select(p => p.Position))
                    && seller
                        .Roster.Select(p => p.Position)
                        .SequenceEqual(sellerBefore.Roster.Select(p => p.Position)),
                "every position slot stays as it was, so the lines keep their shape"
            );
            Check(
                UniqueNumbers(user) && UniqueNumbers(seller),
                "no two players of a club share a number"
            );
            Check(
                Same(Roster(Club(Shl, season.UserClub)), Roster(userBefore)),
                "the club data stays the base"
            );
            Check(
                Same(Roster(Club(season.Rosters(app.Season.Clubs), season.UserClub)), Roster(user)),
                "applying the trades again changes nothing"
            );

            MatchSetup setup = Next(app.Season);
            ClubData mine = setup.Club(
                Same(setup.Home.Id, season.UserClub) ? Side.Home : Side.Away
            );
            Check(
                mine.Roster.Any(p =>
                    Same(p.LastName, get.LastName) && p.Number == trade.OtherNewNumber
                )
                    && !mine.Roster.Any(p => Same(p.LastName, give.LastName))
                    && setup.Trades.SequenceEqual([trade]),
                "the next match plays the traded roster and carries the trade"
            );
        }

        [Test]
        public void TradeReachesReplaysAndTheSave()
        {
            SeasonState season = ToDeadline(135, TwoOffers);
            TradeOffer offer = season.Trades.Offers[0];
            Trade trade = Must(app.Season.AnswerTrade(offer, accept: true));
            string user = Roster(Club(app.Season.Clubs, season.UserClub));
            string seller = Roster(Club(app.Season.Clubs, offer.Club));

            MatchSetup setup = Next(app.Season);
            MatchSetup again = ReplaySetup.Of(setup).ToSetup(Shl);
            Check(
                Same(Roster(again.Home), Roster(setup.Home))
                    && Same(Roster(again.Away), Roster(setup.Away))
                    && again.Trades.SequenceEqual([trade]),
                "a replay builds the same traded rosters from the club data"
            );
            Check(
                Same(Roster(season.SetupFor(Must(season.NextGame), Shl).Home), Roster(setup.Home)),
                "a setup made from the club data files plays the trade too"
            );

            SeasonState loaded = Must(SeasonSave.Read(SavePath));
            Check(
                loaded.Trades.Deals.SequenceEqual([trade])
                    && loaded.Trades.Offers[0].Answer == TradeAnswer.Accepted
                    && loaded.Trades.Held,
                "the save keeps the trade and the answer"
            );
            app.Season.End();
            _ = Must(app.Season.Continue(Shl, SavePath));
            Check(
                Same(Roster(Club(app.Season.Clubs, season.UserClub)), user)
                    && Same(Roster(Club(app.Season.Clubs, offer.Club)), seller),
                "a continued season plays the traded rosters"
            );
            MatchSetup continued = Next(app.Season);
            Check(
                Same(Roster(continued.Home), Roster(setup.Home))
                    && Same(Roster(continued.Away), Roster(setup.Away)),
                "the continued season's next match is the same"
            );
        }

        [Test]
        public void DeclineAndLapse()
        {
            SeasonState season = ToDeadline(135, TwoOffers);
            Check(season.Trades.Offers.Count == 2, "two offers on the table");
            string before = string.Join('|', app.Season.Clubs.Select(Roster));
            Check(
                !app.Season.AnswerTrade(season.Trades.Offers[0], accept: false).HasValue
                    && season.Trades.Offers[0].Answer == TradeAnswer.Declined
                    && season.Trades.Deals.Count == 0,
                "a declined offer makes no trade"
            );
            Check(
                Same(string.Join('|', app.Season.Clubs.Select(Roster)), before),
                "the rosters stay as they were"
            );
            Check(
                !app.Season.AnswerTrade(season.Trades.Offers[0], accept: true).HasValue,
                "a declined offer cannot be accepted after"
            );
            Check(season.Trades.Offers[1].IsOpen, "the other offer still waits");
            _ = app.Season.SimCurrentGame();
            Check(
                season.Trades.Offers[1].Answer == TradeAnswer.Declined
                    && season.Trades.Deals.Count == 0
                    && season.Trades.Held
                    && season.Trades.Offers.Count == 2,
                "an offer left open lapses at the next game, and no new offers come"
            );
        }

        [Test]
        public void ClashingNumbersAreChanged()
        {
            // the seller's centre wears the number of the centre he is traded for
            ClubData user = Shl[0];
            PlayerData give = user.Roster.First(p => p.Position == PlayingPosition.C);
            ClubData seller = Shl[1];
            PlayerData get = seller.Roster.First(p => p.Position == PlayingPosition.C);
            int clash = give.Number;
            List<ClubData> clubs = [.. Shl];
            clubs[1] = seller with
            {
                Roster =
                [
                    .. seller.Roster.Select(p =>
                        p == get ? p with { Number = clash }
                        : p.Number == clash ? p with { Number = get.Number }
                        : p
                    ),
                ],
            };
            SeasonState season = SeasonState.New(clubs, user.Id, 3);
            TradeOffer offer = new()
            {
                Club = seller.Id,
                Position = PlayingPosition.C,
                Give = clash,
                Get = clash,
                GiveName = give.LastName,
                GetName = get.LastName,
            };
            Trade trade = Must(TradeDesk.Accept(season, clubs, offer));
            IReadOnlyList<ClubData> traded = season.Rosters(clubs);
            ClubData u = Club(traded, user.Id);
            ClubData s = Club(traded, seller.Id);
            Check(
                trade.NewNumber != clash && trade.OtherNewNumber != clash,
                S(
                    $"both arriving players change from #{clash} ({trade.NewNumber}, {trade.OtherNewNumber})"
                )
            );
            Check(UniqueNumbers(u) && UniqueNumbers(s), "no number is worn twice in either club");
            Check(
                !user.Roster.Any(p => p.Number == trade.OtherNewNumber)
                    && Math.Abs(trade.OtherNewNumber - clash) <= 3,
                S($"the new number is free and near his own ({trade.OtherNewNumber})")
            );

            int free = TradeDesk.FreeNumber(season.Trades, u, clash);
            Check(
                free != clash && !u.Roster.Any(p => p.Number == free),
                "a departed player's number is not handed out again this season"
            );
        }

        [Test]
        public void DressingRoomNews()
        {
            SeasonState season = ToDeadline(135, TwoOffers);
            DressingRoomReport open = DressingRoomSources.Build(
                DressingRoomContext.Of(season, Shl, TwoOffers)
            );
            string offers = Text.Format(K.TradeNewsOffers, 2);
            Check(
                open.News.Count > 0 && Same(open.News[0].Text, offers),
                S($"the open offers lead the news ({offers})")
            );

            TradeOffer offer = season.Trades.Offers[0];
            _ = app.Season.AnswerTrade(offer, accept: true);
            _ = app.Season.AnswerTrade(season.Trades.Offers[1], accept: false);
            DressingRoomReport after = DressingRoomSources.Build(
                DressingRoomContext.Of(season, Shl, TwoOffers)
            );
            string done = Text.Format(
                K.TradeNewsDone,
                offer.GetName.ToUpperInvariant(),
                Club(Shl, offer.Club).ShortName.ToUpperInvariant(),
                offer.GiveName.ToUpperInvariant()
            );
            Check(
                after.News.Any(e => Same(e.Text, done) && e.Highlight)
                    && !after.News.Any(e => Same(e.Text, offers)),
                S($"the trade is in the news ({done})")
            );
        }
    }
}
