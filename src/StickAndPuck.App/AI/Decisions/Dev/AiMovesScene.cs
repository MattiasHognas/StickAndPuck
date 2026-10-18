using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.Decisions.Dev
{
    /// <summary>SAP_SCENE=aimoves: the AI's dekes and one-timers as a drill, every skater AI, for recording.
    /// SAP_MOVE=deke (the default): a home carrier rushes up the neutral zone at an away defender skating
    /// back at him, over and over. SAP_MOVE=onetimer: a home skater at the point passes across to a
    /// teammate alone in the slot, over and over. Only the setup and, for one-timers, the pass are
    /// scripted; whether the carrier dekes and whether the receiver one-times it are the AI's own reads.</summary>
    public sealed class AiMovesScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "aimoves";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            // arcade rules: no offside whistle for a drill that starts in the attacking zone
            MatchSetup setup = Scenes.DefaultSetup(clubs).WithRules(RuleSet.Arcade);
            World world = new(
                setup with
                {
                    Inputs =
                    [
                        new InputSlot(InputDevice.None, Side.Home),
                        new InputSlot(InputDevice.None, Side.Away),
                    ],
                },
                settings
            );
            MovesDrill.Setup(world, Env.Is("SAP_MOVE", "onetimer"));
            return world;
        }
    }
}
