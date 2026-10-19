using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Serialization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Schedule;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.Playoffs
{
    /// <summary>SHL playoffs, seeded from the final table: places 1 to 6 go straight to the quarter-finals,
    /// places 7 to 10 play a best of three play-in (7 against 10, 8 against 9), then best of seven
    /// quarter-finals, semi-finals and a final. Every round is reseeded, so the best seed left meets
    /// the worst one left. The human's club plays its games; every other game is settled by
    /// <see cref="QuickSim"/>, kept level with the human's series so the bracket moves together.
    /// Saved inside the <see cref="SeasonState"/> it follows. An NHL season plays the NHL's format instead
    /// (<see cref="NhlSeeding"/>): 16 clubs by division and wild card, four best of seven rounds on a
    /// fixed bracket, the better regular season record at home first in every series.</summary>
    public sealed class PlayoffState
    {
        /// <summary>Places that go straight to the quarter-finals.</summary>
        public const int Direct = 6;

        /// <summary>The last place that reaches the play-in.</summary>
        public const int LastPlayIn = 10;

        private static readonly ImmutableArray<(string Name, int BestOf)> Format =
        [
            ("PLAY-IN", 3),
            ("QUARTER-FINALS", 7),
            ("SEMI-FINALS", 7),
            ("FINAL", 7),
        ];

        private static readonly ImmutableArray<(string Name, int BestOf)> NhlFormat =
        [
            ("FIRST ROUND", 7),
            ("SECOND ROUND", 7),
            ("CONFERENCE FINALS", 7),
            ("FINAL", 7),
        ];

        /// <summary>Gets or sets the league whose format the playoffs follow; SHL in an older save.</summary>
        public League League { get; set; } = League.Shl;

        /// <summary>Gets or sets each NHL qualifier's tag by club id (A1, M3, WC2); empty in the SHL, whose
        /// bracket shows the table place.</summary>
        public IDictionary<string, string> Tags { get; set; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Gets or sets the NHL conferences in bracket order (the first one's series come first in
        /// every round but the final); empty in the SHL.</summary>
        public IList<string> Conferences { get; set; } = [];

        /// <summary>Gets or sets the seed the simulated games come from.</summary>
        public int Seed { get; set; }

        /// <summary>Gets or sets the id of the human's club.</summary>
        public string UserClub { get; set; } = "";

        /// <summary>Gets or sets every club in final table order: the seeds, best first.</summary>
        public IReadOnlyList<string> Seeds { get; set; } = [];

        /// <summary>Gets or sets which games of a best of seven the higher seed hosts, from the season's rules when the playoffs start.</summary>
        public PlayoffHomeIce HomeIce { get; set; } = PlayoffHomeIce.Alternating;

        /// <summary>Gets or sets the rounds started so far; the last one is being played.</summary>
        public IList<PlayoffRound> Rounds { get; set; } = [];

        /// <summary>Gets a round's name in the language shown: play-in, quarter-finals, semi-finals, final.</summary>
        public static string RoundTitle(int round)
        {
            return Text.Get(
                round switch
                {
                    <= 0 => K.RoundPlayIn,
                    1 => K.RoundQuarterFinals,
                    2 => K.RoundSemiFinals,
                    _ => K.RoundFinal,
                }
            );
        }

        /// <summary>Gets an NHL round's name in the language shown: first round, second round, conference finals, final.</summary>
        public static string NhlRoundTitle(int round)
        {
            return Text.Get(
                round switch
                {
                    <= 0 => K.RoundFirst,
                    1 => K.RoundSecond,
                    2 => K.RoundConferenceFinals,
                    _ => K.RoundFinal,
                }
            );
        }

        /// <summary>Gets a round's name in the language shown, in this playoff's format.</summary>
        public string Title(int round)
        {
            return League == League.Nhl ? NhlRoundTitle(round) : RoundTitle(round);
        }

        /// <summary>Gets the number of rounds in the playoffs.</summary>
        [JsonIgnore]
        public static int RoundCount => Format.Length;

        /// <summary>Gets the name of the round being played, in the language shown (<see cref="PlayoffRound.Name"/> is the save's).</summary>
        [JsonIgnore]
        public string CurrentTitle => Title(Rounds.Count - 1);

        /// <summary>Gets the round being played.</summary>
        [JsonIgnore]
        public PlayoffRound Current => Rounds[^1];

        /// <summary>Gets the champion's club id, or None until the final is won.</summary>
        [JsonIgnore]
        public Maybe<string> Champion =>
            Rounds.Count == Format.Length && Current.IsOver
                ? Current.Series[0].Winner
                : Maybe.None<string>();

        /// <summary>Gets a value indicating whether the playoffs have a champion.</summary>
        [JsonIgnore]
        public bool IsOver => Champion.HasValue;

        /// <summary>Gets the human's open series, or None when the human's club is out, waiting for a round, or done.</summary>
        [JsonIgnore]
        public Maybe<PlayoffSeries> UserSeries =>
            Rounds.Count == 0
                ? Maybe.None<PlayoffSeries>()
                : Current.Series.FirstOrNone(s => !s.IsOver && s.Involves(UserClub));

        /// <summary>Gets a value indicating whether the human's club made the playoffs.</summary>
        [JsonIgnore]
        public bool UserQualified =>
            League == League.Nhl
                ? Tags.ContainsKey(UserClub)
                : SeedOf(UserClub) is > 0 and <= LastPlayIn;

        /// <summary>Gets a value indicating whether the human's club can still win the title.</summary>
        [JsonIgnore]
        public bool UserAlive =>
            UserQualified && (IsOver ? Same(Champion.Or(""), UserClub) : Alive());

        /// <summary>Seeds the playoffs from a finished season and starts the play-in (the first round in the NHL).</summary>
        public static PlayoffState New(SeasonState season)
        {
            IReadOnlyList<StandingRow> table = season.Table;
            PlayoffState state = new()
            {
                Seed = unchecked((season.Seed * 31) + 2_027),
                UserClub = season.UserClub,
                Seeds = [.. table.Select(r => r.ClubId)],
                HomeIce = season.Rules.Or(RuleSet.Shl).PlayoffHomeIce,
                League = season.League,
            };
            if (season.League != League.Nhl)
            {
                state.Rounds.Add(state.Pair(0, Enumerable.Range(Direct + 1, LastPlayIn - Direct)));
                return state;
            }

            NhlField field = NhlSeeding.Field(season, table);
            foreach ((string club, string tag) in field.Tags)
            {
                state.Tags[club] = tag;
            }

            state.Conferences = [.. field.Conferences];
            state.Rounds.Add(
                state.Fixed(
                    0,
                    field.Pairs.Select(p => (state.SeedOf(p.First), state.SeedOf(p.Second)))
                )
            );
            return state;
        }

        /// <summary>Gets a club's tag in the bracket: its NHL qualifying spot (A1, WC2), or its table place in the SHL.</summary>
        public string TagOf(string clubId)
        {
            return Tags.TryGetValue(clubId, out string tag)
                ? tag
                : SeedOf(clubId).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Gets a club's seed: its place in the final table, from 1, or 0 when it is not in the season.</summary>
        public int SeedOf(string clubId)
        {
            for (int i = 0; i < Seeds.Count; i++)
            {
                if (Same(Seeds[i], clubId))
                {
                    return i + 1;
                }
            }
            return 0;
        }

        /// <summary>Adds a game to a series of the round being played.</summary>
        public bool Record(PlayoffSeries series, FixtureResult result)
        {
            if (
                series.IsOver
                || result.HomeGoals == result.AwayGoals
                || !Current.Series.Contains(series)
            )
            {
                return false;
            }

            series.Games.Add(new PlayoffGame(series.NextHome, series.NextAway, result));
            return true;
        }

        /// <summary>Settles every game up to the human's next one: the other series of the round are played to
        /// as many games as the human's series has, and when the human has no open series the round is
        /// finished and the next one drawn, until the human plays again or there is a champion. The games follow the mode's overtime rules.</summary>
        public int SimulateUntilUser(IReadOnlyList<ClubData> clubs, PlayoffRules rules)
        {
            Dictionary<string, ClubData> byId = clubs.ToDictionary(
                c => c.Id,
                StringComparer.Ordinal
            );
            int count = 0;
            while (!IsOver)
            {
                Maybe<PlayoffSeries> user = UserSeries;
                int upTo = user.TryGet(out PlayoffSeries mine) ? mine.Games.Count : int.MaxValue;
                for (int i = 0; i < Current.Series.Count; i++)
                {
                    PlayoffSeries s = Current.Series[i];
                    while (!user.Is(u => u == s) && !s.IsOver && s.Games.Count < upTo)
                    {
                        _ = Record(s, Simulate(byId, s, i, rules));
                        count++;
                    }
                }
                if (user.HasValue || !NextRound())
                {
                    break;
                }
            }
            return count;
        }

        /// <summary>Draws the next round once the current one is over.</summary>
        public bool NextRound()
        {
            if (!Current.IsOver || Rounds.Count >= Format.Length)
            {
                return false;
            }

            if (League == League.Nhl)
            {
                // a fixed bracket: the winners of neighbouring series meet
                List<int> won = [.. Current.Series.Select(s => SeedOf(s.Winner.Or("")))];
                Rounds.Add(
                    Fixed(
                        Rounds.Count,
                        Enumerable
                            .Range(0, won.Count / 2)
                            .Select(i => (won[2 * i], won[(2 * i) + 1]))
                    )
                );
                return true;
            }

            IEnumerable<int> winners = Current.Series.Select(s => SeedOf(s.Winner.Or("")));
            IEnumerable<int> seeds =
                Rounds.Count == 1 ? Enumerable.Range(1, Direct).Concat(winners) : winners;
            Rounds.Add(Pair(Rounds.Count, seeds));
            return true;
        }

        /// <summary>Gets the seed for one simulated game: fixed per playoffs, round, series and game.</summary>
        public int GameSeed(int round, int series, int game)
        {
            return unchecked((Seed * 1_000_003) + (((round * 64) + (series * 8) + game) * 7_919));
        }

        private FixtureResult Simulate(
            Dictionary<string, ClubData> byId,
            PlayoffSeries s,
            int index,
            PlayoffRules rules
        )
        {
            ClubData home = byId.TryGetValue(s.NextHome, out ClubData h)
                ? h
                : throw new InvalidOperationException(
                    $"club {s.NextHome} from the save has no data file"
                );
            ClubData away = byId.TryGetValue(s.NextAway, out ClubData a)
                ? a
                : throw new InvalidOperationException(
                    $"club {s.NextAway} from the save has no data file"
                );
            FixtureResult r = QuickSim.Play(
                home,
                away,
                GameSeed(Rounds.Count - 1, index, s.Games.Count)
            );

            // a simulated game follows the mode's overtime rules like a played one: with no shootout in
            // the format, a tie is settled in sudden death overtime instead
            return r.Kind == ResultKind.Shootout && !rules.Overtime.HasShootout
                ? r with
                {
                    Kind = ResultKind.Overtime,
                }
                : r;
        }

        private PlayoffRound Pair(int round, IEnumerable<int> seeds)
        {
            List<int> order = [.. seeds.Order()];
            (string name, int bestOf) = Format[round];
            PlayoffRound result = new() { Name = name, BestOf = bestOf };
            for (int i = 0; i < order.Count / 2; i++)
            {
                int high = order[i],
                    low = order[order.Count - 1 - i];
                result.Series.Add(NewSeries(high, low, bestOf));
            }
            return result;
        }

        /// <summary>An NHL round on the fixed bracket: the series in bracket order, the better seed of each
        /// (the better regular season record) as its higher seed.</summary>
        private PlayoffRound Fixed(int round, IEnumerable<(int A, int B)> pairs)
        {
            (string name, int bestOf) = NhlFormat[round];
            PlayoffRound result = new() { Name = name, BestOf = bestOf };
            foreach ((int a, int b) in pairs)
            {
                result.Series.Add(NewSeries(Math.Min(a, b), Math.Max(a, b), bestOf));
            }
            return result;
        }

        private PlayoffSeries NewSeries(int high, int low, int bestOf)
        {
            return new PlayoffSeries
            {
                HighSeed = high,
                LowSeed = low,
                High = Seeds[high - 1],
                Low = Seeds[low - 1],
                BestOf = bestOf,
                HomeIce = HomeIce,
            };
        }

        private bool Alive()
        {
            // the human's club has not lost a series of the current round, or waits out the play-in as a top six seed
            return Current.Series.FirstOrDefault(s => s.Involves(UserClub)) is { } series
                ? !series.IsOver || Same(series.Winner.Or(""), UserClub)
                : League != League.Nhl && Rounds.Count == 1 && SeedOf(UserClub) <= Direct;
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }
    }
}
