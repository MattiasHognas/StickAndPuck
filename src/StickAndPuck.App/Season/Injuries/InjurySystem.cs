using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Checks;

namespace StickAndPuck.App.Season.Injuries
{
    /// <summary>Draws injuries from the step's events, with its own dice stream of the match seed, so a replay
    /// draws the same ones: a hard check may hurt the player hit, a blocked slap shot the blocker and a
    /// landed punch the fighter it hits (<see cref="InjuryModel"/>, scaled by <see cref="RuleSet.InjuryScale"/>).
    /// Each injury publishes one <see cref="PlayerInjured"/> with the games he misses; a player is hurt
    /// at most once a match and plays the match out. The stats tracker writes the injury on the match
    /// sheet, and a season game's sheet puts him on the season's absence list
    /// (<see cref="AbsenceTable"/>), so the lines of his next games are built without him. Goalies are
    /// never hurt. Runs after the checking, puck handling and fight systems publish their events, and
    /// before the stats system reads them.</summary>
    /// <remarks>Initializes the system with its own randomness, so tests repeat.</remarks>
    internal sealed class InjurySystem(Random rng) : ISystem
    {
        /// <summary>The dice seed without a match seed.</summary>
        internal const int Fallback = 10;

        /// <summary>How far from a check's spot, in metres, the player hit is looked for.</summary>
        private const float VictimReach = 1f;

        private readonly Random rng = rng;
        private readonly HashSet<int> hurt = [];

        public InjurySystem()
            : this(Seeds.Dice(Seeds.Environment, "injuries", Fallback)) { }

        public void Start(World world)
        {
            if (world.Setup.Absent.Count > 0)
            {
                Console.WriteLine(
                    $"SAP: injuries: left out of this match: {string.Join(", ", world.Setup.Absent)}"
                );
            }
        }

        public void Update(World world, float dt)
        {
            float scale = world.Setup.Rules.InjuryScale;
            if (scale <= 0 || world.Events.Current.Count == 0)
            {
                return;
            }

            foreach (MatchEvent e in world.Events.Current.ToList())
            {
                switch (e)
                {
                    case CheckLanded c:
                        Checked(world, c, scale);
                        break;
                    case ShotBlocked b when b.Slap && Valid(world, b.BlockerId):
                        Try(
                            world,
                            world.Players[b.BlockerId],
                            InjuryModel.Block(b.Slap, b.Speed, scale),
                            InjuryCause.BlockedShot,
                            b.Position
                        );
                        break;
                    default:
                        break;
                }
            }
        }

        private void Checked(World world, CheckLanded c, float scale)
        {
            if (!Valid(world, c.CheckerId) || !Victim(world, c).TryGet(out Player victim))
            {
                return;
            }

            bool punch = world.Players[c.CheckerId].Action == PlayerAction.Fight;
            if (!punch && c.Strength < CheckingModel.HardHit)
            {
                return;
            }

            Try(
                world,
                victim,
                punch ? InjuryModel.Punch(c.Strength, scale) : InjuryModel.Check(c.Strength, scale),
                punch ? InjuryCause.Fight : InjuryCause.Check,
                c.Position
            );
        }

        /// <summary>Rolls one chance on a player and publishes the injury when it comes up.</summary>
        private void Try(World world, Player p, float chance, InjuryCause cause, Vector3 at)
        {
            if (p.IsGoalie || hurt.Contains(p.Id) || !InjuryModel.Roll(rng, chance))
            {
                return;
            }

            _ = hurt.Add(p.Id);
            int games = InjuryModel.Games(rng);
            world.Events.Publish(new PlayerInjured(world.Time, p.Side, p.Id, at, games, cause));
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: injury: {world.Setup.Club(p.Side).ShortName} #{p.Data.Number} {p.Data.LastName} ({cause}), out {games} game(s)"
                )
            );
        }

        /// <summary>The player a check (or a punch) hit: the nearest opponent skater to where it landed, as the fight and penalty systems find him.</summary>
        private static Maybe<Player> Victim(World world, CheckLanded c)
        {
            Player victim = world
                .Players.Where(p => p.Side != c.Side && p.OnIce && !p.IsGoalie)
                .MinBy(p => Vector3.DistanceSquared(p.Position, c.Position));
            return
                victim is { }
                && Vector3.DistanceSquared(victim.Position, c.Position) <= VictimReach * VictimReach
                ? victim
                : Maybe.None<Player>();
        }

        private static bool Valid(World world, int id)
        {
            return id >= 0 && id < world.Players.Count;
        }
    }
}
