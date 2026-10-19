using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Fights;
using StickAndPuck.App.Rules.Penalties;

namespace StickAndPuck.App.Season.Suspensions
{
    /// <summary>Hands out suspensions from the step's events: a skater's second fight in the match
    /// (<see cref="FightStarted"/>, both fighters counted) and a match penalty (a <see cref="PenaltyCalled"/> that
    /// ejected him: a game misconduct, or a major and game misconduct, which SHL rules give for every fight)
    /// each are an offence, and <see cref="SuspensionModel"/> turns his offences into 1 to 3 games. Publishes
    /// <see cref="PlayerSuspended"/>, again when a later offence lengthens it. The stats tracker writes it on
    /// the match sheet and a season game's sheet puts him on the season's absence list
    /// (<see cref="Injuries.AbsenceTable"/>), so the lines of his club's next games are built without him.
    /// No dice, so a replay suspends the same players. Runs after the penalty and fight systems.</summary>
    internal sealed class SuspensionSystem : ISystem
    {
        private readonly Dictionary<int, int> fights = [];
        private readonly Dictionary<int, List<int>> offences = [];
        private readonly HashSet<int> ejected = [];
        private readonly Dictionary<int, int> published = [];

        public void Start(World world)
        {
            fights.Clear();
            offences.Clear();
            ejected.Clear();
            published.Clear();
        }

        public void Update(World world, float dt)
        {
            if (!world.Setup.Rules.Suspensions || world.Events.Current.Count == 0)
            {
                return;
            }

            foreach (MatchEvent e in world.Events.Current.ToList())
            {
                switch (e)
                {
                    case FightStarted:
                        Fought(world);
                        break;
                    case PenaltyCalled p:
                        Called(world, p);
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>A fight started: both fighters count one; a second fight is an offence.</summary>
        private void Fought(World world)
        {
            if (!Fighting.State(world).Fight.TryGet(out Fight fight))
            {
                return;
            }

            foreach (int id in fight.Both.Select(f => f.PlayerId))
            {
                int count = fights.GetValueOrDefault(id) + 1;
                fights[id] = count;
                if (count == 2)
                {
                    Offend(
                        world,
                        id,
                        SuspensionCause.SecondFight,
                        SuspensionModel.Offence(SuspensionCause.SecondFight, majorForAHit: false)
                    );
                }
            }
        }

        /// <summary>A penalty: when it threw him out of the match it is a match penalty, worth more for a hit's major
        /// and game misconduct than for a fight's or a plain game misconduct.</summary>
        private void Called(World world, PenaltyCalled p)
        {
            if (!Valid(world, p.PlayerId) || ejected.Contains(p.PlayerId))
            {
                return;
            }

            if (!PenaltyCalls.Ejected(world, p.PlayerId))
            {
                return;
            }

            _ = ejected.Add(p.PlayerId);
            FightState fs = Fighting.State(world);
            bool fight =
                fs.LastEnded >= world.Time
                && fs.Last.Is(f => f.Both.Any(x => x.PlayerId == p.PlayerId));
            bool major =
                !fight
                && PenaltyCalls.SeverityOf(world, p.PlayerId) == Severity.MajorAndGameMisconduct;
            Offend(
                world,
                p.PlayerId,
                SuspensionCause.MatchPenalty,
                SuspensionModel.Offence(SuspensionCause.MatchPenalty, major)
            );
        }

        private void Offend(World world, int playerId, SuspensionCause cause, int worth)
        {
            if (!Valid(world, playerId))
            {
                return;
            }

            if (!offences.TryGetValue(playerId, out List<int> list))
            {
                list = [];
                offences[playerId] = list;
            }

            list.Add(worth);
            int games = SuspensionModel.Games(list, world.Setup.Rules);
            if (games <= published.GetValueOrDefault(playerId))
            {
                return;
            }

            published[playerId] = games;
            Player p = world.Players[playerId];
            world.Events.Publish(
                new PlayerSuspended(world.Time, p.Side, p.Id, p.Position, games, cause)
            );
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: suspension: {world.Setup.Club(p.Side).ShortName} #{p.Data.Number} {p.Data.LastName} ({cause}), out {games} game(s)"
                )
            );
        }

        private static bool Valid(World world, int id)
        {
            return id >= 0 && id < world.Players.Count;
        }
    }
}
