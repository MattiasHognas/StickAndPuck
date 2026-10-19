using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Match.Dev;

namespace StickAndPuck.App.Intermission.Dev
{
    /// <summary>SAP_SCENE=talk: an exhibition with the keyboard on the home centre that opens in the first
    /// intermission a goal down (SAP_TALK_DOWN=n goals; two or more makes the coach angry), so the trailing
    /// talk opens over the Zamboni at once. SAP_TALK=push or patient answers it after a moment with a
    /// scripted press the replay records; SAP_ZAMBONI_AT works as in the zamboni scene.</summary>
    public sealed class TalkScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "talk";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = MatchScenes.Exhibition(clubs, settings, 60f);
            if (world.OnIce(Side.Home).FirstOrDefault(p => !p.IsGoalie) is { } centre)
            {
                centre.Slot = 0;
            }

            MatchState m = world.Match;
            m.Phase = MatchPhase.Intermission;
            m.Clock = 0;
            m.Score[1] =
                Env.GetNumber("SAP_TALK_DOWN").TryGet(out float down) && down >= 1 ? (int)down : 1;
            world.Puck.Live = false;
            MatchFlow.State(world).Timer = 4f;
            world.Switches.TalkPick = Env.Get("SAP_TALK").ToUpperInvariant() switch
            {
                "PUSH" => TalkChoice.Push,
                "PATIENT" => TalkChoice.Patient,
                _ => TalkChoice.None,
            };
            ZamboniScene.StartAt(world);
            return world;
        }
    }
}
