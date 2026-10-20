using System.Text.Json.Serialization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Board;
using StickAndPuck.App.Season.Challenges;
using StickAndPuck.App.Season.Injuries;
using StickAndPuck.App.Season.Morale;
using StickAndPuck.App.Season.News;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Stats;
using StickAndPuck.App.Season.Trades;

namespace StickAndPuck.App.Season.Schedule
{
    /// <summary>A whole regular season: the clubs, the human's club, the schedule (52 games a club in the SHL,
    /// 82 in the NHL, see <see cref="NhlSchedule"/>) and every result so far. This is exactly what the save file holds; the table is worked out from the results. The
    /// human's club plays its game of each round; the other games of that round are settled by
    /// <see cref="QuickSim"/> when the human's game ends. The playoffs read <see cref="IsOver"/> and
    /// <see cref="Table"/> to seed them; the screens show <see cref="Table"/> and <see cref="NextGame"/>.</summary>
    public sealed class SeasonState
    {
        /// <summary>The save format version; bump it when the shape changes.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Gets or sets the save format version.</summary>
        public int Version { get; set; } = CurrentVersion;

        /// <summary>Gets or sets the seed the schedule and the simulated games come from.</summary>
        public int Seed { get; set; }

        /// <summary>Gets or sets the id of the club the human plays.</summary>
        public string UserClub { get; set; } = "";

        /// <summary>Gets or sets the league the season is played in, which sets its schedule, its standings
        /// groups and its playoff format; SHL in an older save.</summary>
        public League League { get; set; } = League.Shl;

        /// <summary>Gets or sets each club's conference, by club id; empty in a league without conferences.</summary>
        public IDictionary<string, string> Conferences { get; set; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Gets or sets each club's division, by club id; empty in a league without divisions.</summary>
        public IDictionary<string, string> Divisions { get; set; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Gets or sets every club id in the season.</summary>
        public ICollection<string> Clubs { get; set; } = [];

        /// <summary>Gets or sets every game, in round order.</summary>
        public IList<Fixture> Games { get; set; } = [];

        /// <summary>Gets or sets when the season was started (UTC).</summary>
        public DateTime Started { get; set; }

        /// <summary>Gets or sets when the season was last saved (UTC).</summary>
        public DateTime Saved { get; set; }

        /// <summary>Gets or sets the playoffs, None until the regular season is over and they start.</summary>
        public Maybe<PlayoffState> Playoffs { get; set; }

        /// <summary>Gets the club relegated in the SHL playout (W76), or None while it is open or without one (NHL, rule off).</summary>
        [JsonIgnore]
        public Maybe<string> Relegated =>
            Playoffs.TryGet(out PlayoffState p) ? p.Relegated : Maybe.None<string>();

        /// <summary>Gets or sets the rules every game of this season plays, stored when its first game starts so a later settings change never alters a running season; None in an older save until then.</summary>
        public Maybe<RuleSet> Rules { get; set; }

        /// <summary>Gets or sets the player stats and records, filled as games are played or settled.</summary>
        public SeasonStats Stats { get; set; } = new();

        /// <summary>Gets or sets each club's team morale by club id, 0 to 100; a club missing here is at the neutral 50 (see <see cref="TeamMorale"/>).</summary>
        public IDictionary<string, int> Morale { get; set; } =
            new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Gets or sets the news events in play (see <see cref="NewsDesk"/>); empty in an older save.</summary>
        public IList<NewsEvent> News { get; set; } = [];

        /// <summary>Gets or sets the trade deadline: its offers and the trades made (see <see cref="TradeDesk"/>); empty in an older save.</summary>
        public SeasonTrades Trades { get; set; } = new();

        /// <summary>Gets or sets the coach's challenges, the open one and those settled (see <see cref="ChallengeDesk"/>); empty in an older save.</summary>
        public IList<SeasonChallenge> Challenges { get; set; } = [];

        /// <summary>Gets or sets the board's goal for the human's club (W133, <see cref="BoardExpectation"/>), set when the season
        /// starts; an older save gets it when it is read.</summary>
        public Maybe<BoardGoal> Goal { get; set; }

        /// <summary>Gets or sets a value indicating whether the board fired the human (<see cref="BoardExpectation.CheckHotSeat"/>):
        /// the season is over for them, with no more games and no playoffs.</summary>
        public bool Fired { get; set; }

        /// <summary>Gets the number of rounds.</summary>
        public int Rounds => Games.Count == 0 ? 0 : Games.Max(g => g.Round) + 1;

        /// <summary>Gets a value indicating whether every game has been played, so the playoffs can start.</summary>
        public bool IsOver => Games.All(g => g.Played);

        /// <summary>Gets the human's next unplayed game, or None when the human's club has played them all.</summary>
        public Maybe<Fixture> NextGame => Games.FirstOrNone(g => !g.Played && g.Involves(UserClub));

        /// <summary>Gets the round being played: the first round with an unplayed game, or <see cref="Rounds"/> when the season is over.</summary>
        public int CurrentRound =>
            Games.FirstOrNone(g => !g.Played).TryGet(out Fixture open) ? open.Round : Rounds;

        /// <summary>Gets the current table, best first, under the season's rules (SHL until they are stored).</summary>
        public IReadOnlyList<StandingRow> Table =>
            Standings.Compute(Clubs, Games, Rules.Or(RuleSet.Shl));

        /// <summary>Starts a new season for every club, with the human on one of them. The clubs' league sets the
        /// format: the SHL's four meetings of every pair (<see cref="SeasonSchedule"/>), or the NHL's 82
        /// games by division and conference (<see cref="NhlSchedule"/>), with NHL rules and the 16-club
        /// division playoff (<see cref="NhlSeeding"/>).</summary>
        public static SeasonState New(IReadOnlyList<ClubData> clubs, string userClub, int seed)
        {
            List<string> ids = [.. clubs.Select(c => c.Id)];
            if (!ids.Contains(userClub, StringComparer.Ordinal))
            {
                throw new ArgumentException($"{userClub} is not a club", nameof(userClub));
            }

            League league = clubs[0].League;
            if (clubs.Any(c => c.League != league))
            {
                throw new ArgumentException("a season's clubs play in one league", nameof(clubs));
            }

            bool nhl = league == League.Nhl;
            return new SeasonState
            {
                Seed = seed,
                UserClub = userClub,
                League = league,
                Conferences = Groups(clubs, c => c.Conference),
                Divisions = Groups(clubs, c => c.Division),
                Clubs = ids,
                Games = [.. nhl ? NhlSchedule.Build(clubs, seed) : SeasonSchedule.Build(ids, seed)],
                Rules = nhl ? RuleSet.Nhl : Maybe.None<RuleSet>(),
                Goal = BoardExpectation.Pick(clubs, userClub),
                Started = DateTime.UtcNow,
            };
        }

        /// <summary>Starts a new season of a league with every club in its data folder.</summary>
        public static SeasonState New(League league, string userClub, int seed)
        {
            return New(Core.Clubs.Load(league), userClub, seed);
        }

        private static Dictionary<string, string> Groups(
            IReadOnlyList<ClubData> clubs,
            Func<ClubData, Maybe<string>> group
        )
        {
            Dictionary<string, string> groups = [with(StringComparer.Ordinal)];
            foreach (ClubData c in clubs)
            {
                if (group(c).TryGet(out string name))
                {
                    groups[c.Id] = name;
                }
            }
            return groups;
        }

        /// <summary>The clubs as this season has them: the club data files with the season's trades made
        /// (<see cref="TradeDeals.Apply"/>). Clubs that already have them are returned unchanged.</summary>
        public IReadOnlyList<ClubData> Rosters(IReadOnlyList<ClubData> clubs)
        {
            return Trades.Deals.Count == 0 ? clubs : TradeDeals.Apply(clubs, Trades.Deals);
        }

        /// <summary>Gets the games of one round.</summary>
        public IEnumerable<Fixture> Round(int round)
        {
            return Games.Where(g => g.Round == round);
        }

        /// <summary>Gets the games one club has played, oldest first.</summary>
        public IEnumerable<Fixture> PlayedBy(string clubId)
        {
            return Games.Where(g => g.Played && g.Involves(clubId));
        }

        /// <summary>Records a game's result and moves both clubs' morale.</summary>
        public void Record(int number, FixtureResult result)
        {
            Games[number] = Games[number] with { Result = result };
            TeamMorale.AfterResult(this, Games[number]);
        }

        /// <summary>Settles every unplayed game before and in the given round that the human does not play.
        /// Called when the human's game of that round ends.</summary>
        public int SimulateUpTo(int round, IReadOnlyList<ClubData> clubs)
        {
            Dictionary<string, ClubData> byId = Rosters(clubs)
                .ToDictionary(c => c.Id, StringComparer.Ordinal);
            int count = 0;
            foreach (
                Fixture g in Games
                    .Where(g => g.Round <= round && !g.Played && !g.Involves(UserClub))
                    .ToList()
            )
            {
                if (
                    !byId.TryGetValue(g.Home, out ClubData home)
                    || !byId.TryGetValue(g.Away, out ClubData away)
                )
                {
                    continue;
                }

                Record(
                    g.Number,
                    TeamMorale.Sim(this, InForm(home), InForm(away), GameSeed(g.Number))
                );
                count++;
            }
            return count;
        }

        /// <summary>The club as this season's games play it: without its injured and suspended players, and its
        /// players' traits moved by their carry-over form and their development (W132), under the season's
        /// stored rules (SHL until its first game stores them).</summary>
        public ClubData InForm(ClubData club)
        {
            ClubData playing = Stats.Absences.Without(club);
            return FormTable.Apply(playing, Nudges(playing, Rules.Or(RuleSet.Shl)));
        }

        /// <summary>Gets the trait points a player's development has moved this season, for the "+2" by the name.</summary>
        public int DevelopmentPoints(string club, int number)
        {
            return Stats.Development.Points(club, number, Rules.Or(RuleSet.Shl));
        }

        /// <summary>A club's players' season nudges by <see cref="FormTable.Key"/>: carry-over form plus development.</summary>
        private Dictionary<string, int> Nudges(ClubData club, RuleSet rules)
        {
            Dictionary<string, int> nudges = new(
                Stats.Form.Nudges(club, rules),
                StringComparer.Ordinal
            );
            Stats.Development.AddNudges(club, rules, nudges);
            return nudges;
        }

        /// <summary>Gets the trait points a player's carry-over form moves in this season, for the arrow by the name.</summary>
        public int FormNudge(string club, int number)
        {
            return Stats.Form.Nudge(club, number, Rules.Or(RuleSet.Shl));
        }

        /// <summary>Gets the seed for one simulated game: fixed per season and game, the same on every machine and run.</summary>
        public int GameSeed(int number)
        {
            return unchecked((Seed * 1_000_003) + (number * 7_919));
        }

        /// <summary>The match setup for a game, so the season starts a match the same way exhibition, arcade and
        /// versus do: SHL rules, keyboard and mouse on the human's club, both clubs' morale, their form and
        /// their rosters as traded (<see cref="Rosters"/>) without the injured and suspended players (<see cref="AbsenceTable"/>).</summary>
        public MatchSetup SetupFor(
            Fixture game,
            IReadOnlyList<ClubData> clubs,
            InputDevice device = InputDevice.KeyboardMouse
        )
        {
            IReadOnlyList<ClubData> rosters = Rosters(clubs);
            ClubData Find(string id)
            {
                return rosters
                    .FirstOrNone(c => string.Equals(c.Id, id, StringComparison.Ordinal))
                    .TryGet(out ClubData club)
                    ? club
                    : throw new InvalidOperationException(
                        $"club {id} from the save has no data file"
                    );
            }

            Side side = string.Equals(game.Home, UserClub, StringComparison.Ordinal)
                ? Side.Home
                : Side.Away;
            Side other = side == Side.Home ? Side.Away : Side.Home;
            RuleSet rules = Rules.Or(RuleSet.Shl);
            List<string> absent = [.. Stats.Absences.Out(Find(game.Home))];
            absent.AddRange(Stats.Absences.Out(Find(game.Away)));
            ClubData home = AbsenceTable.Without(Find(game.Home), absent),
                away = AbsenceTable.Without(Find(game.Away), absent);
            Dictionary<string, int> form = Nudges(home, rules);
            foreach ((string key, int nudge) in Nudges(away, rules))
            {
                form[key] = nudge;
            }

            NewsDesk.AddNudges(this, form);

            return new MatchSetup
            {
                Home = FormTable.Apply(home, form),
                Away = FormTable.Apply(away, form),
                Inputs = [new InputSlot(device, side), new InputSlot(InputDevice.None, other)],
                Overtime = OvertimeRule.Shl,
                Morale = TeamMorale.ForGame(this, game.Home, game.Away),
                Rivalry = Rivals.Are(game.Home, game.Away),
                Form = form,
                StartEnergy = NewsDesk.StartEnergy(this, game),
                Absent = absent,
                Trades = [.. Trades.Deals],
            };
        }
    }
}
