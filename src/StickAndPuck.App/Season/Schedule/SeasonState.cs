using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Stats;

namespace StickAndPuck.App.Season.Schedule
{
    /// <summary>A whole regular season: the clubs, the human's club, the 52-round schedule and every result so
    /// far. This is exactly what the save file holds; the table is worked out from the results. The
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

        /// <summary>Gets or sets the rules every game of this season plays, stored when its first game starts so a later settings change never alters a running season; None in an older save until then.</summary>
        public Maybe<RuleSet> Rules { get; set; }

        /// <summary>Gets or sets the player stats and records, filled as games are played or settled.</summary>
        public SeasonStats Stats { get; set; } = new();

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

        /// <summary>Starts a new season for every club, with the human on one of them.</summary>
        public static SeasonState New(IReadOnlyList<ClubData> clubs, string userClub, int seed)
        {
            List<string> ids = [.. clubs.Select(c => c.Id)];
            return !ids.Contains(userClub, StringComparer.Ordinal)
                ? throw new ArgumentException($"{userClub} is not a club", nameof(userClub))
                : new SeasonState
                {
                    Seed = seed,
                    UserClub = userClub,
                    Clubs = ids,
                    Games = [.. SeasonSchedule.Build(ids, seed)],
                    Started = DateTime.UtcNow,
                };
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

        /// <summary>Records a game's result.</summary>
        public void Record(int number, FixtureResult result)
        {
            Games[number] = Games[number] with { Result = result };
        }

        /// <summary>Settles every unplayed game before and in the given round that the human does not play.
        /// Called when the human's game of that round ends.</summary>
        public int SimulateUpTo(int round, IReadOnlyList<ClubData> clubs)
        {
            Dictionary<string, ClubData> byId = clubs.ToDictionary(
                c => c.Id,
                StringComparer.Ordinal
            );
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

                Record(g.Number, QuickSim.Play(home, away, GameSeed(g.Number)));
                count++;
            }
            return count;
        }

        /// <summary>Gets the seed for one simulated game: fixed per season and game, the same on every machine and run.</summary>
        public int GameSeed(int number)
        {
            return unchecked((Seed * 1_000_003) + (number * 7_919));
        }

        /// <summary>The match setup for a game, so the season starts a match the same way exhibition, arcade and
        /// versus do: SHL rules, keyboard and mouse on the human's club.</summary>
        public MatchSetup SetupFor(
            Fixture game,
            IReadOnlyList<ClubData> clubs,
            InputDevice device = InputDevice.KeyboardMouse
        )
        {
            ClubData Find(string id)
            {
                return clubs
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
            return new MatchSetup
            {
                Home = Find(game.Home),
                Away = Find(game.Away),
                Inputs = [new InputSlot(device, side), new InputSlot(InputDevice.None, other)],
                Overtime = OvertimeRule.Shl,
            };
        }
    }
}
