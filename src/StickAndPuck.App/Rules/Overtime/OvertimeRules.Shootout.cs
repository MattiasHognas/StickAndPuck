using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Rules.Overtime
{
    internal sealed partial class OvertimeRules
    {
        private void ShootoutStep(World world, OvertimeState s, float dt)
        {
            if (!s.Shootout.HasValue)
            {
                Begin(world, s);
            }

            switch (s.Stage)
            {
                case ShootoutStage.Break:
                    UpdateBreak(world, s, dt);
                    break;
                case ShootoutStage.Ready:
                    UpdateReady(world, s, dt);
                    break;
                case ShootoutStage.Attempt:
                    Attempt(world, s, dt);
                    break;
                case ShootoutStage.Result:
                    UpdateResult(world, s, dt);
                    break;
                case ShootoutStage.Done:
                    break;
                default:
                    break;
            }
        }

        private static void UpdateBreak(World world, OvertimeState s, float dt)
        {
            if (!s.First.IsSet())
            {
                // The human home club picks; nothing else runs until it does (or the time runs out)
                s.Timer -= dt;
                SideOrNone pick = ShootoutOrder.Pick(world);
                SideOrNone first = pick.IsSet() ? pick : SideOrNone.None;
                if (!first.IsSet() && s.Timer <= -ShootoutOrder.ChooseSeconds)
                {
                    first = SideOrNone.Home;
                }

                s.First = first;
                if (s.First.TryGetSide(out Side picked))
                {
                    s.Timer = 0.5f;
                    Log(
                        world,
                        $"{world.Setup.Home.ShortName} choose to shoot {(picked == Side.Home ? "first" : "second")}"
                    );
                }
                return;
            }

            if (Wait(s, dt))
            {
                NextShooter(world, s);
            }
        }

        private void UpdateReady(World world, OvertimeState s, float dt)
        {
            Hold(world, s);
            if (Wait(s, dt))
            {
                Go(world, s);
            }
        }

        private static void UpdateResult(World world, OvertimeState s, float dt)
        {
            ShootoutState so = s.Live;
            if (!Wait(s, dt))
            {
                return;
            }

            if (so.Decided(so.Rounds).TryGetSide(out Side winner))
            {
                End(world, s, winner);
            }
            else
            {
                NextShooter(world, s);
            }
        }

        /// <summary>Clears the ice for the shootout and picks each club's shooting order: best hands first.</summary>
        private static void Begin(World world, OvertimeState s)
        {
            s.Shootout = new ShootoutState { Rounds = ShootoutOrder.Shooters(world.Setup) };
            s.Stage = ShootoutStage.Break;
            s.Timer = OvertimeTuning.BreakSeconds;
            s.InBreak = false;
            s.First = ShootoutOrder.First(world.Setup);
            if (!s.First.IsSet())
            {
                s.Timer = 0;
            }

            world.Puck.Live = false;
            foreach (Player p in world.Players.Where(p => !p.IsGoalie))
            {
                Bench(p);
            }

            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                s.Order[(int)side] =
                [
                    .. world
                        .Players.Where(p => p.Side == side && !p.IsGoalie)
                        .OrderByDescending(p =>
                            (2 * p.Data.Traits.ShotAccuracy)
                            + p.Data.Traits.PuckControl
                            + p.Data.Traits.ShotPower
                        )
                        .ThenBy(p => p.Id),
                ];
            }

            Log(
                world,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"shootout, {ShootoutOrder.Shooters(world.Setup)} shooters each, {(s.First.TryGetSide(out Side f) ? $"{world.Setup.Club(f).ShortName} first" : $"{world.Setup.Home.ShortName} to choose")}"
                )
            );
        }

        /// <summary>Brings out the next shooter: the clubs in turn, the first club opening every round, round after round through the order.</summary>
        private static void NextShooter(World world, OvertimeState s)
        {
            ShootoutState so = s.Live;
            if (so.ShooterId >= 0)
            {
                Bench(world.Players[so.ShooterId]);
            }

            Side side = ShootoutOrder.Next(
                so,
                s.First.TryGetSide(out Side first) ? first : Side.Home
            );
            List<Player> order = s.Order[(int)side];
            if (order.Count == 0)
            {
                // a club with no skaters (should not happen) misses its turn
                so.Add(new ShootoutShot(side, -1, Scored: false));
                s.Stage = ShootoutStage.Result;
                s.Timer = 0;
                return;
            }

            Player shooter = order[
                ShootoutOrder.Place(world.Setup.Rules, s.Next[(int)side]++, order.Count)
            ];
            so.ShooterId = shooter.Id;
            so.Shooting = side;
            shooter.OnIce = true;
            shooter.Begin(PlayerAction.None);

            // a human slot playing for this club shoots
            for (int slot = 0; slot < world.Setup.Inputs.Count; slot++)
            {
                InputSlot input = world.Setup.Inputs[slot];
                if (input.Device != InputDevice.None && input.Side == side)
                {
                    shooter.Slot = slot;
                    break;
                }
            }

            s.Stage = ShootoutStage.Ready;
            s.Timer = OvertimeTuning.ReadySeconds;
            Hold(world, s);
        }

        /// <summary>Keeps the shooter and the dead puck at centre until the attempt starts.</summary>
        private static void Hold(World world, OvertimeState s)
        {
            ShootoutAttempt.Hold(world, world.Players[s.Live.ShooterId]);
        }

        /// <summary>Starts the attempt: the puck is live on the shooter's stick; an AI shooter picks a lane and a distance to shoot from.</summary>
        private void Go(World world, OvertimeState s)
        {
            s.Stage = ShootoutStage.Attempt;
            s.Attempt.Go(world, world.Players[s.Live.ShooterId], rng);
        }

        /// <summary>Watches one attempt to its end; skates and shoots for an AI shooter.</summary>
        private void Attempt(World world, OvertimeState s, float dt)
        {
            Player shooter = world.Players[s.Live.ShooterId];
            if (s.Attempt.Step(world, shooter, rng, dt).TryGet(out bool scored))
            {
                Finish(world, s, scored);
            }
        }

        private static void Finish(World world, OvertimeState s, bool scored)
        {
            ShootoutState so = s.Live;
            Player shooter = world.Players[so.ShooterId];
            so.Add(new ShootoutShot(shooter.Side, shooter.Id, scored));
            world.Puck.Live = false;
            s.Stage = ShootoutStage.Result;
            s.Timer = OvertimeTuning.ResultSeconds;
            Log(
                world,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"shootout #{shooter.Data.Number} {world.Setup.Club(shooter.Side).ShortName} {(scored ? "scores" : "misses")}, {so.Goals(Side.Home)}-{so.Goals(Side.Away)}"
                )
            );
        }

        /// <summary>The shootout is decided: the winner gets one goal on the final score and the match ends.</summary>
        private static void End(World world, OvertimeState s, Side winner)
        {
            ShootoutState so = s.Live;
            so.Winner = winner.OrNone();
            so.ShooterId = -1;
            s.Stage = ShootoutStage.Done;
            MatchState m = world.Match;
            m.Score[(int)winner]++;
            MatchRules.EndMatch(world, MatchFlow.State(world));
            Log(world, $"final after the shootout, {world.Setup.Club(winner).ShortName} win it");
        }
    }
}
