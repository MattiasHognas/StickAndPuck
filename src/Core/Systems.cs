using StickAndPuck.AI;
using StickAndPuck.Arena;
using StickAndPuck.Audio;
using StickAndPuck.Camera;
using StickAndPuck.Fights;
using StickAndPuck.Goalie;
using StickAndPuck.Hud;
using StickAndPuck.Lighting;
using StickAndPuck.Lines;
using StickAndPuck.Physics;
using StickAndPuck.Players;
using StickAndPuck.Referee;
using StickAndPuck.Rules;
using StickAndPuck.Screens;
using StickAndPuck.Season;

namespace StickAndPuck.Core;

/// <summary>
/// The fixed list of systems, in update and draw order. Every task's system is already here, so
/// a task fills in its own class and never edits this list or Game.cs.
/// </summary>
public static class Systems
{
    /// <summary>Creates every system in order. Called after the window opens.</summary>
    /// <returns>The systems.</returns>
    public static IReadOnlyList<ISystem> Create()
    {
        return
        [
            new ScreenSystem(),          // W11 start screen, settings, pause, results
            new InputSystem(),           // W4a human input slots -> PlayerCommand
            new PositioningSystem(),     // W6a AI: where each AI player skates
            new DecisionSystem(),        // W6b AI: chase, pass, shoot
            new SwitchingSystem(),       // W4c which player each human controls
            new LinesSystem(),           // W13 fatigue, line changes, goalie swap
            new SkatingSystem(),         // W4a movement from commands and traits
            new CheckingSystem(),        // W4c body checks
            new PuckHandlingSystem(),    // W4b possession, stick handling, shots, passes
            new GoalieSystem(),          // W5 crease positioning and saves
            new PuckPhysics(),           // W3 puck flight, boards, posts, nets, goal line
            new MatchRules(),            // W7a faceoffs, clock, periods, goals
            new OffsideRule(),           // W7b
            new IcingRule(),             // W7b
            new OvertimeRules(),         // W7c 3 on 3, shootout
            new PenaltyRules(),          // W14 calls, box, power play
            new FightSystem(),           // W15
            new RefereeSystem(),         // W8 skates, drops the puck, signals
            new StatsSystem(),           // W25 player stats, player of the game
            new ScheduleSystem(),        // W16a season schedule and standings
            new AutosaveSystem(),        // W16a autosave after each match
            new PlayoffSystem(),         // W16b
            new ArcadeSystem(),          // W16c
            new CameraSystem(),          // W9 gameplay and replay camera
            new LightingSystem(),        // W18 time of day, light cues
            new AudioSystem(),           // W12 8-bit effects
            new ArenaSystem(),           // W2 ice, boards, glass, nets, ads, crowd
            new SkaterRenderer(),        // W4a skater models and clips
            new HudSystem(),             // W10 scorebug, clock, messages
        ];
    }
}
