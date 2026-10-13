using StickAndPuck.App.AI;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Audio;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Fights;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Hud;
using StickAndPuck.Intermission;
using StickAndPuck.App.Lighting;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Momentum;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.App.Pregame;
using StickAndPuck.App.Referee;
using StickAndPuck.App.Rules;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season;

namespace StickAndPuck.App.Core;

/// <summary>
/// The fixed list of systems, in update and draw order. Every task's system is already here, so
/// a task fills in its own class and never edits this list or Game.cs.
/// </summary>
public static class Systems
{
    /// <summary>Creates every system in order. Called after the window opens.</summary>
    /// <param name="seed">The match seed (W67): each system with dice gets its own stream of it; 0 gives the fixed test streams.</param>
    /// <returns>The systems.</returns>
    public static IReadOnlyList<ISystem> Create(int seed)
    {
        return
        [
            new ScreenSystem(),          // W11 start screen, settings, pause, results
            new InputSystem(),           // W4a human input slots -> PlayerCommand
            new PositioningSystem(Seeds.Dice(seed, "positioning", PositioningSystem.Fallback)),     // W6a AI: where each AI player skates
            new DefenceSystem(Seeds.Dice(seed, "defence", DefenceSystem.Fallback)),         // W22b AI: contain, mark the man, cut lanes, stay home
            new SpecialTeamsSystem(Seeds.Dice(seed, "specialteams", SpecialTeamsSystem.Fallback)),    // W22c AI: power play and penalty kill shapes, clears
            new TacticsSystem(Seeds.Dice(seed, "tactics", TacticsSystem.Fallback)),         // W22d AI: club tactics: forecheck, trap, dump and chase
            new DecisionSystem(Seeds.Dice(seed, "decisions", DecisionSystem.Fallback)),        // W6b AI: chase, pass, shoot
            new PregameSystem(),         // W28 warm-up before the opening faceoff
            new SwitchingSystem(),       // W4c which player each human controls
            new LinesSystem(),           // W13 fatigue, line changes, goalie swap
            new SkatingSystem(),         // W4a movement from commands and traits
            new CheckingSystem(Seeds.Dice(seed, "checking", CheckingSystem.Fallback)),        // W4c body checks
            new PuckHandlingSystem(Seeds.Dice(seed, "puckhandling", PuckHandlingSystem.Fallback)),    // W4b possession, stick handling, shots, passes
            new GoalieSystem { Dice = Seeds.Dice(seed, "goalie", GoalieSystem.Fallback) },          // W5 crease positioning and saves
            new PuckPhysics(),           // W3 puck flight, boards, posts, nets, goal line
            new MatchRules(),            // W7a faceoffs, clock, periods, goals
            new OffsideRule(),           // W7b
            new IcingRule(),             // W7b
            new OvertimeRules(Seeds.Dice(seed, "overtime", OvertimeRules.Fallback)),         // W7c 3 on 3, shootout
            new PenaltyRules(),          // W14 calls, box, power play
            new FightSystem(),           // W15
            new RefereeSystem(Seeds.Dice(seed, "referee", RefereeSystem.Fallback)),         // W8 skates, drops the puck, signals
            new StatsSystem(),           // W25 player stats, player of the game
            new ScheduleSystem(),        // W16a season schedule and standings
            new AutosaveSystem(),        // W16a autosave after each match
            new PlayoffSystem(),         // W16b
            new ArcadeSystem(),          // W16c
            new CameraSystem(),          // W9 gameplay and replay camera
            new IntermissionSystem(),    // W31 the Zamboni between periods
            new LightingSystem(),        // W18 time of day, light cues
            new AudioSystem(),           // W12 8-bit effects
            new MomentumSystem(),        // W29 boost after a goal, a fight won or a big hit; crowd swell, HUD bar
            new ArenaSystem(),           // W2 ice, boards, glass, nets, ads, crowd
            new SkaterRenderer(),        // W4a skater models and clips
            new HudSystem(),             // W10 scorebug, clock, messages
        ];
    }
}
