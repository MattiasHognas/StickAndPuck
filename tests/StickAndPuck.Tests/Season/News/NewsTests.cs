using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StickAndPuck.App.Core;
using StickAndPuck.App.Replay;
using StickAndPuck.App.Season.DressingRoom;
using StickAndPuck.App.Season.Morale;
using StickAndPuck.App.Season.News;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Stats;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season.News
{
    /// <summary>
    /// Season news events (W130): the deck in data/season/news.json and its weights, how often a card comes
    /// up under each rule setting, every effect (flu, rookie call-up, contract dispute, travel delay, fan
    /// night) reaching the match through its setup and replay, the same story from the same season seed,
    /// the events in the save (and an older save without them), and the lines in the dressing room.
    /// </summary>
    public sealed class NewsTests : CheckedTest
    {
        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-newstest-{Environment.ProcessId}-{Guid.NewGuid():N}"
            )
        );

        private static IReadOnlyList<ClubData> Clubs => WorldBuilder.Clubs;

        private static NewsDeck Deck => NewsDeck.Default;

        [After(Test)]
        public void EndSession()
        {
            app.Season.End();
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        private static SeasonState NewSeason(int seed, RuleSet rules)
        {
            SeasonState season = SeasonState.New(Clubs, Clubs[0].Id, seed);
            season.Rules = rules;
            return season;
        }

        private static ClubData User(SeasonState season)
        {
            return Clubs.First(c => string.Equals(c.Id, season.UserClub, StringComparison.Ordinal));
        }

        private static MatchSetup NextSetup(SeasonState season)
        {
            return season.SetupFor(
                season.NextGame.TryGet(out Fixture next)
                    ? next
                    : throw new InvalidOperationException("no next game"),
                Clubs
            );
        }

        private static ClubData UserSide(MatchSetup setup, SeasonState season)
        {
            return string.Equals(setup.Home.Id, season.UserClub, StringComparison.Ordinal)
                ? setup.Home
                : setup.Away;
        }

        private static int Trait(ClubData club, int number)
        {
            return club.Roster.First(p => p.Number == number).Traits.HockeyIq;
        }

        [Test]
        public void PresetsAndRows()
        {
            Check(
                RuleSet.Shl.News
                    && RuleSet.Shl.NewsFrequency == NewsFrequency.Normal
                    && RuleSet.Nhl.NewsFrequency == NewsFrequency.Often
                    && !RuleSet.Arcade.News,
                "SHL draws news at the normal rate, the NHL often, Arcade none"
            );
            Check(
                RuleSet.Shl.NewsEvery == 5
                    && RuleSet.Nhl.NewsEvery == 3
                    && (RuleSet.Shl with { NewsFrequency = NewsFrequency.Rare }).NewsEvery == 8
                    && RuleSet.Arcade.NewsEvery == 0,
                "the frequency sets the odds, and off draws nothing"
            );
            Check(
                RuleRows.All.Any(r =>
                    string.Equals(r.Key, nameof(RuleSet.News), StringComparison.Ordinal)
                    && string.Equals(r.Group, Text.Keys.RuleGroupSeason, StringComparison.Ordinal)
                )
                    && RuleRows.All.Any(r =>
                        string.Equals(
                            r.Key,
                            nameof(RuleSet.NewsFrequency),
                            StringComparison.Ordinal
                        )
                        && string.Equals(
                            r.Group,
                            Text.Keys.RuleGroupSeason,
                            StringComparison.Ordinal
                        )
                    ),
                "both news rules are on the rules page's season group"
            );
        }

        [Test]
        public void DeckAndStrings()
        {
            string[] ids = ["flu", "rookie", "contract", "travel", "fannight"];
            Check(
                ids.All(id => Deck.Card(id).HasValue) && Deck.Cards.All(c => c.Weight > 0),
                "the deck holds the five cards, each with a weight"
            );
            IReadOnlyDictionary<string, string> en = Text.Read(Text.FileOf(Language.English)),
                sv = Text.Read(Text.FileOf(Language.Swedish));
            string[] keys =
            [
                .. Deck.Cards.Select(c => c.Text),
                Text.Keys.RuleNews,
                Text.Keys.RuleNewsFrequency,
                Text.Keys.RuleValueRare,
                Text.Keys.RuleValueOften,
            ];
            Check(
                keys.All(k => en.ContainsKey(k) && sv.ContainsKey(k)),
                "every news string is in English and Swedish"
            );
        }

        [Test]
        public void PickFollowsTheWeights()
        {
            IReadOnlyList<NewsCard> cards = Deck.Cards;
            int total = cards.Sum(c => c.Weight);
            Dictionary<string, int> hits = [with(StringComparer.Ordinal)];
            for (int roll = 0; roll < total; roll++)
            {
                string id = NewsDesk.Pick(cards, roll).Id;
                hits[id] = hits.GetValueOrDefault(id) + 1;
            }

            Check(
                cards.All(c => hits.GetValueOrDefault(c.Id) == c.Weight),
                "each card takes as many rolls as its weight"
            );

            // over many seasons the drawn cards follow the weights, by venue
            Dictionary<string, int> drawn = [with(StringComparer.Ordinal)];
            int homeTravel = 0,
                awayFan = 0;
            for (int seed = 1; seed <= 3000; seed++)
            {
                SeasonState season = NewSeason(
                    seed,
                    RuleSet.Shl with
                    {
                        NewsFrequency = NewsFrequency.Often,
                    }
                );
                if (NewsDesk.Draw(season, Clubs, Deck).TryGet(out NewsEvent e))
                {
                    drawn[e.Id] = drawn.GetValueOrDefault(e.Id) + 1;
                    bool home = season.NextGame.Is(g =>
                        string.Equals(g.Home, season.UserClub, StringComparison.Ordinal)
                    );
                    homeTravel +=
                        home && string.Equals(e.Id, "travel", StringComparison.Ordinal) ? 1 : 0;
                    awayFan +=
                        !home && string.Equals(e.Id, "fannight", StringComparison.Ordinal) ? 1 : 0;
                }
            }

            Check(
                drawn.GetValueOrDefault("flu") > drawn.GetValueOrDefault("rookie")
                    && drawn.GetValueOrDefault("flu") > drawn.GetValueOrDefault("contract")
                    && drawn.Values.All(n => n > 0),
                "the heaviest card comes up most and every card comes up ("
                    + string.Join(
                        ", ",
                        drawn.Select(kv =>
                            kv.Key + " " + kv.Value.ToString(CultureInfo.InvariantCulture)
                        )
                    )
                    + ")"
            );
            Check(
                homeTravel == 0 && awayFan == 0,
                "a travel delay only comes before an away game, a fan night before a home game"
            );
        }

        [Test]
        public void FrequencyFollowsTheRule()
        {
            static double Rate(RuleSet rules)
            {
                int draws = 0;
                const int Seasons = 3000;
                for (int seed = 1; seed <= Seasons; seed++)
                {
                    draws += NewsDesk.Draw(NewSeason(seed, rules), Clubs, Deck).HasValue ? 1 : 0;
                }

                return draws / (double)Seasons;
            }

            double rare = Rate(RuleSet.Shl with { NewsFrequency = NewsFrequency.Rare }),
                normal = Rate(RuleSet.Shl),
                often = Rate(RuleSet.Shl with { NewsFrequency = NewsFrequency.Often }),
                off = Rate(RuleSet.Shl with { News = false });
            Check(
                Math.Abs(rare - (1 / 8.0)) < 0.03
                    && Math.Abs(normal - (1 / 5.0)) < 0.03
                    && Math.Abs(often - (1 / 3.0)) < 0.03
                    && off == 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"about one game in 8, 5 and 3, none when off ({rare:0.000}, {normal:0.000}, {often:0.000}, {off:0.000})"
                )
            );

            SeasonState full = NewSeason(
                3,
                RuleSet.Shl with
                {
                    NewsFrequency = NewsFrequency.Often,
                }
            );
            _ = NewsDesk.Force(full, Clubs, Deck, "flu");
            _ = NewsDesk.Force(full, Clubs, Deck, "rookie");
            Check(
                full.News.Count == NewsDesk.MaxActive
                    && Enumerable.Range(0, 50).All(_ => !NewsDesk.Draw(full, Clubs, Deck).HasValue),
                "no card is drawn while the most events run"
            );
        }

        [Test]
        public void FluWeakensSomeSkatersForAFewGames()
        {
            SeasonState season = NewSeason(11, RuleSet.Shl);
            MatchSetup before = NextSetup(season);
            NewsEvent flu = NewsDesk.Force(season, Clubs, Deck, "flu").Or(new NewsEvent());
            Check(
                flu.Players.Count is >= 2 and <= 3
                    && flu.GamesLeft is >= 2 and <= 3
                    && flu.Players.All(n =>
                        User(season)
                            .Roster.Any(p => p.Number == n && p.Position != PlayingPosition.G)
                    ),
                "the flu hits two or three skaters for two or three games"
            );
            MatchSetup after = NextSetup(season);
            ClubData was = UserSide(before, season),
                now = UserSide(after, season);
            Check(
                flu.Players.All(n => Trait(now, n) == Math.Clamp(Trait(was, n) - 8, 0, 100))
                    && now.Roster.Where(p => !flu.Players.Contains(p.Number))
                        .All(p => Trait(was, p.Number) == p.Traits.HockeyIq),
                "the setup plays the sick skaters 8 trait points weaker and the others as before"
            );
            Check(
                flu.Players.All(n =>
                    after.Form.GetValueOrDefault(FormTable.Key(season.UserClub, n))
                    == before.Form.GetValueOrDefault(FormTable.Key(season.UserClub, n)) - 8
                ),
                "the trait points go into the setup's nudges, which the replay stores"
            );

            int games = flu.GamesLeft;
            for (int i = 0; i < games; i++)
            {
                Check(season.News.Contains(flu), "the flu lasts its games");
                NewsDesk.Tick(season);
            }

            Check(season.News.Count == 0, "and then it is over");
        }

        [Test]
        public void RookieAndContractHitTheRightSkater()
        {
            SeasonState season = NewSeason(12, RuleSet.Shl);
            List<PlayerData> skaters =
            [
                .. User(season).Roster.Where(p => p.Position != PlayingPosition.G),
            ];
            int weakest = skaters.OrderBy(NewsDesk.Overall).ThenBy(p => p.Number).First().Number,
                best =
                    skaters
                        .OrderByDescending(NewsDesk.Overall)
                        .ThenBy(p => p.Number)
                        .First()
                        .Number;
            MatchSetup before = NextSetup(season);
            NewsEvent rookie = NewsDesk.Force(season, Clubs, Deck, "rookie").Or(new NewsEvent());
            NewsEvent contract = NewsDesk
                .Force(season, Clubs, Deck, "contract")
                .Or(new NewsEvent());
            Check(
                rookie.Players.SequenceEqual([weakest]) && contract.Players.SequenceEqual([best]),
                "the call-up lifts the weakest skater, the dispute hits the best"
            );
            ClubData was = UserSide(before, season),
                now = UserSide(NextSetup(season), season);
            Check(
                Trait(now, weakest) == Math.Clamp(Trait(was, weakest) + 6, 0, 100)
                    && Trait(now, best) == Math.Clamp(Trait(was, best) - 6, 0, 100),
                "the rookie plays 6 points up, the unhappy star 6 down"
            );
        }

        [Test]
        public void TravelDelayStartsTheClubTired()
        {
            SeasonState season = NewSeason(13, RuleSet.Shl);
            MatchSetup before = NextSetup(season);
            Check(before.StartEnergy.SequenceEqual([1f, 1f]), "a club starts fresh without news");
            NewsEvent travel = NewsDesk.Force(season, Clubs, Deck, "travel").Or(new NewsEvent());
            MatchSetup setup = NextSetup(season);
            Side user = string.Equals(setup.Home.Id, season.UserClub, StringComparison.Ordinal)
                ? Side.Home
                : Side.Away;
            Side other = user == Side.Home ? Side.Away : Side.Home;
            Check(
                travel.GamesLeft == 1
                    && Math.Abs(setup.StartEnergyOf(user) - 0.7f) < 1e-6f
                    && Math.Abs(setup.StartEnergyOf(other) - 1f) < 1e-6f,
                "a travel delay starts the club's next game at 70 percent energy"
            );

            World world = new(setup, new Settings());
            Player tired = world.AddPlayer(user, setup.Club(user).Roster[0], Vector3.Zero),
                fresh = world.AddPlayer(other, setup.Club(other).Roster[0], Vector3.Zero);
            Check(
                Math.Abs(tired.Energy - 0.7f) < 1e-6f && Math.Abs(fresh.Energy - 1f) < 1e-6f,
                "the match puts the tired club's players on the ice tired"
            );

            string json = JsonSerializer.Serialize(
                ReplaySetup.Of(setup),
                ReplayJsonContext.Default.ReplaySetup
            );
            ReplaySetup read =
                JsonSerializer.Deserialize(json, ReplayJsonContext.Default.ReplaySetup)
                ?? throw new InvalidOperationException("replay setup did not read");
            Check(
                read.ToSetup(Clubs).StartEnergy.SequenceEqual(setup.StartEnergy),
                "the replay keeps the tired start"
            );
            ReplaySetup old =
                JsonSerializer.Deserialize(
                    json.Replace("\"startEnergy\":", "\"unused\":", StringComparison.Ordinal),
                    ReplayJsonContext.Default.ReplaySetup
                ) ?? throw new InvalidOperationException("old replay setup did not read");
            Check(
                old.ToSetup(Clubs).StartEnergy.SequenceEqual([1f, 1f]),
                "an older replay starts everyone fresh"
            );
        }

        [Test]
        public void FanNightLiftsMorale()
        {
            SeasonState season = NewSeason(14, RuleSet.Shl);
            int before = TeamMorale.Of(season, season.UserClub);
            _ = NewsDesk.Force(season, Clubs, Deck, "fannight");
            Check(
                TeamMorale.Of(season, season.UserClub) == before + 8,
                "a fan night gives the club 8 morale"
            );
            MatchSetup setup = NextSetup(season);
            Side user = string.Equals(setup.Home.Id, season.UserClub, StringComparison.Ordinal)
                ? Side.Home
                : Side.Away;
            Check(
                setup.Morale.Is(m => m.Of(user) == before + 8),
                "and the setup's morale starts the momentum meter with it"
            );

            SeasonState off = NewSeason(14, RuleSet.Shl with { Morale = false });
            _ = NewsDesk.Force(off, Clubs, Deck, "fannight");
            Check(off.Morale.Count == 0, "with morale off the fan night moves nothing");
        }

        [Test]
        public void SameSeedSameStory()
        {
            List<string> Story(int seed, string name)
            {
                AppState run = new();
                string path = Path.Combine(folder, name);
                _ = run.Season.StartNew(Clubs, Clubs[0].Id, Maybe.Some(seed), path);
                SeasonState season = run.Season.Current.Or(new SeasonState());
                season.Rules = RuleSet.Shl with { NewsFrequency = NewsFrequency.Often };
                List<string> story = [];
                for (int i = 0; i < 15; i++)
                {
                    _ = run.Season.SimCurrentGame();
                    story.Add(
                        string.Join(
                            ';',
                            season.News.Select(e =>
                                string.Create(
                                    CultureInfo.InvariantCulture,
                                    $"{e.Id}:{string.Join(',', e.Players)}:{e.GamesLeft}:{e.Drawn}"
                                )
                            )
                        )
                    );
                }

                run.Season.End();
                return story;
            }

            _ = Directory.CreateDirectory(folder);
            List<string> a = Story(77, "a.json"),
                b = Story(77, "b.json"),
                c = Story(78, "c.json");
            Check(
                a.SequenceEqual(b, StringComparer.Ordinal),
                "the same season seed tells the same news story"
            );
            Check(a.Exists(s => s.Length > 0), "fifteen games with frequent news bring some");
            Check(!a.SequenceEqual(c, StringComparer.Ordinal), "another seed tells another");
        }

        [Test]
        public void EventsLiveInTheSave()
        {
            _ = Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "season.json");
            SeasonState season = app.Season.StartNew(Clubs, Clubs[0].Id, Maybe.Some(21), path);
            season.Rules = RuleSet.Shl;
            NewsEvent flu = NewsDesk.Force(season, Clubs, Deck, "flu").Or(new NewsEvent());
            SeasonSave.Write(season, path);
            app.Season.End();
            Check(
                app.Season.Continue(Clubs, path).TryGet(out SeasonState read)
                    && read.News.Count == 1
                    && string.Equals(read.News[0].Id, "flu", StringComparison.Ordinal)
                    && read.News[0].Effect == NewsEffect.Traits
                    && read.News[0].Players.SequenceEqual(flu.Players)
                    && read.News[0].GamesLeft == flu.GamesLeft
                    && read.News[0].Amount == flu.Amount,
                "the event in play reads back from the save"
            );

            JsonObject json = JsonNode.Parse(File.ReadAllText(path)).AsObject();
            Check(json.Remove("news"), "the save holds the news, removed to make an older save");
            File.WriteAllText(path, json.ToJsonString());
            app.Season.End();
            Check(
                SeasonSave.Read(path).Is(s => s.News.Count == 0 && s.Games.Count > 0),
                "a save from before news reads with none in play"
            );
        }

        [Test]
        public void DressingRoomShowsTheNews()
        {
            SeasonState season = NewSeason(31, RuleSet.Shl);
            NewsEvent contract = NewsDesk
                .Force(season, Clubs, Deck, "contract")
                .Or(new NewsEvent());
            string name = User(season).Roster.First(p => p.Number == contract.Players[0]).LastName;
            DressingRoomReport report = DressingRoomSources.Build(
                DressingRoomContext.Of(season, Clubs, RuleSet.Shl)
            );
            Check(
                report.News.Count > 0
                    && report.News[0].Highlight
                    && report.News[0].Priority > 0
                    && report.News[0].Text.Contains(name, StringComparison.Ordinal),
                "the fresh news event leads the news, in gold, naming the player"
            );
            DressingRoomReport off = DressingRoomSources.Build(
                DressingRoomContext.Of(season, Clubs, RuleSet.Shl with { News = false })
            );
            Check(off.News.All(n => n.Priority == 0), "with news off the dressing room shows none");
        }
    }
}
