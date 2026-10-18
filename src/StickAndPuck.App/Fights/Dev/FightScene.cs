using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Fights.Dev
{
    /// <summary>SAP_SCENE=fight: a full AI exhibition that skips the intro and opens with the puck dropped. A
    /// quarter second in, the two centres drop the gloves and fight; the loser goes to the box for
    /// fighting and play restarts in his end. SAP_AI_HUMAN=1 puts the keyboard on the home centre: accept
    /// with F (Y on a pad), punch with space (A) when the marker is in the green, block with left ctrl (RB)
    /// just as the other fighter winds up. SAP_FIGHT_CHANCE sets the chance a later hard check drops the
    /// gloves (0.4 is normal); SAP_PERIOD=seconds shortens the periods.</summary>
    public sealed class FightScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "fight";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            float period = Env.GetNumber("SAP_PERIOD").TryGet(out float p) && p > 0 ? p : 20 * 60;
            World world = new(
                Scenes.DefaultSetup(clubs) with
                {
                    PeriodSeconds = period,
                },
                settings with
                {
                    Fights = true,
                }
            );
            Scenes.AddStarters(world);
            if (
                Env.Is("SAP_AI_HUMAN", "1")
                && world.OnIce(Side.Home).FirstOrDefault(x => x.Data.Position == PlayingPosition.C)
                    is { } centre
            )
            {
                centre.Slot = 0;
            }

            MatchFlow.Begin(world);
            MatchFlow.SetUpFaceoff(world, RinkSpec.CentreDot);
            _ = MatchFlow.Drop(world);
            FightState s = Fighting.State(world);
            s.ScriptAfterDrop = 0.25f;
            if (Env.GetNumber("SAP_FIGHT_CHANCE").TryGet(out float chance) && chance >= 0)
            {
                s.OfferChance = chance;
            }

            return world;
        }
    }
}
