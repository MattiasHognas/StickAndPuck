using StickAndPuck.App.AI.Decisions;
using StickAndPuck.App.AI.Defence;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.AI.SpecialTeams;
using StickAndPuck.App.AI.Tactics;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Audio;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Fights;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Intermission;
using StickAndPuck.App.Lighting;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Momentum;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Checks;
using StickAndPuck.App.Players.Input;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Players.Switching;
using StickAndPuck.App.Pregame;
using StickAndPuck.App.Referee;
using StickAndPuck.App.Rules.Icing;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Offside;
using StickAndPuck.App.Rules.Overtime;
using StickAndPuck.App.Rules.Penalties;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season.Arcade;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Stats;

namespace StickAndPuck.App.Core
{
    /// <summary>The fixed list of systems, in update and draw order. Every task's system is already here, so
    /// a task fills in its own class and never edits this list or Game.cs.</summary>
    public static class Systems
    {
        /// <summary>Creates the systems a world runs: every system in order, or for an end-of-season scene
        /// (SAP_ENDING, <see cref="Season.Endings.EndingSystem"/>) only its own presentation, with no match to simulate.</summary>
        public static IReadOnlyList<ISystem> For(World world)
        {
            return world.Dev.Ending.HasValue
                ? [new Season.Endings.EndingSystem()]
                : Create(world.Seed);
        }

        /// <summary>Creates every system in order. Called after the window opens.</summary>
        /// <param name="seed">The match seed: each system with dice gets its own stream of it; 0 gives the fixed test streams.</param>
        public static IReadOnlyList<ISystem> Create(int seed)
        {
            return
            [
                new ScreenSystem(), // start screen, settings, pause, results
                new InputSystem(), // human input slots -> PlayerCommand
                new PositioningSystem(Seeds.Dice(seed, "positioning", PositioningSystem.Fallback)), // a AI: where each AI player skates
                new DefenceSystem(Seeds.Dice(seed, "defence", DefenceSystem.Fallback)), // b AI: contain, mark the man, cut lanes, stay home
                new SpecialTeamsSystem(
                    Seeds.Dice(seed, "specialteams", SpecialTeamsSystem.Fallback)
                ), // c AI: power play and penalty kill shapes, clears
                new TacticsSystem(Seeds.Dice(seed, "tactics", TacticsSystem.Fallback)), // d AI: club tactics: forecheck, trap, dump and chase
                new DecisionSystem(Seeds.Dice(seed, "decisions", DecisionSystem.Fallback)), // b AI: chase, pass, shoot
                new PregameSystem(), // warm-up before the opening faceoff
                new SwitchingSystem(), // which player each human controls
                new LinesSystem(), // fatigue, line changes, goalie swap
                new SkatingSystem(), // movement from commands and traits
                new CheckingSystem(Seeds.Dice(seed, "checking", CheckingSystem.Fallback)), // body checks
                new PuckHandlingSystem(
                    Seeds.Dice(seed, "puckhandling", PuckHandlingSystem.Fallback)
                ), // possession, stick handling, shots, passes
                new GoalieSystem { Dice = Seeds.Dice(seed, "goalie", GoalieSystem.Fallback) }, // crease positioning and saves
                new PuckPhysics(), // puck flight, boards, posts, nets, goal line
                new MatchRules(), // faceoffs, clock, periods, goals
                new OffsideRule(), // b
                new IcingRule(), // b
                new OvertimeRules(Seeds.Dice(seed, "overtime", OvertimeRules.Fallback)), // c 3 on 3, shootout
                new PenaltyRules(), // calls, box, power play
                new FightSystem(),
                new Season.Injuries.InjurySystem(
                    Seeds.Dice(seed, "injuries", Season.Injuries.InjurySystem.Fallback)
                ), // injuries from hard checks, blocked slap shots and punches
                new Season.Suspensions.SuspensionSystem(), // suspensions for a second fight or a match penalty
                new RefereeSystem(Seeds.Dice(seed, "referee", RefereeSystem.Fallback)), // skates, drops the puck, signals
                new StatsSystem(), // player stats, player of the game
                new ScheduleSystem(), // season schedule and standings
                new AutosaveSystem(), // autosave after each match
                new PlayoffSystem(), // b
                new ArcadeSystem(), // c
                new CameraSystem(), // gameplay and replay camera
                new IntermissionSystem(), // the Zamboni between periods
                new LightingSystem(), // time of day, light cues
                new AudioSystem(), // 8-bit effects
                new MomentumSystem(), // boost after a goal, a fight won or a big hit; crowd swell, HUD bar
                new ArenaSystem(), // ice, boards, glass, nets, ads, crowd
                new SkaterRenderer(), // skater models and clips
                new Benches.BenchRenderer(), // both benches' players behind the far boards
                new HudSystem(), // scorebug, clock, messages
                new Network.NetworkSystem(), // network match overlay: waiting, leaving
            ];
        }
    }
}
