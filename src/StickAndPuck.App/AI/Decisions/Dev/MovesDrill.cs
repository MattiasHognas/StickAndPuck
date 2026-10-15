using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.AI.Dev;

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
        var world = new World(Scenes.DefaultSetup(clubs) with { Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] }, settings);
        MovesDrill.Setup(world, Env.Is("SAP_MOVE", "onetimer"));
        return world;
    }
}

/// <summary>The script behind <see cref="AiMovesScene"/>, driven from <see cref="DecisionSystem"/>.</summary>
internal sealed class MovesDrill
{
    /// <summary>Seconds each round of the drill runs before it starts over.</summary>
    const float Round = 4f;

    /// <summary>Seconds into a one-timer round that the pass goes.</summary>
    const float PassAt = 0.6f;

    readonly bool oneTimer;
    readonly Player carrier, mate, defender;
    float start;
    bool passed;

    MovesDrill(bool oneTimer, Player carrier, Player mate, Player defender)
    {
        this.oneTimer = oneTimer;
        this.carrier = carrier;
        this.mate = mate;
        this.defender = defender;
    }

    /// <summary>Adds the drill's skaters and goalies and starts the first round.</summary>
    public static void Setup(World world, bool oneTimer)
    {
        var home = Skaters(world.Setup.Home);
        var away = Skaters(world.Setup.Away);
        var star = new Traits { HockeyIq = 95, PuckControl = 95, ShotAccuracy = 90, TopSpeed = 80, Acceleration = 80 };
        var carrier = world.AddPlayer(Side.Home, home[0] with { Traits = star }, Vector3.Zero);
        var mate = world.AddPlayer(Side.Home, home[1 % home.Count] with { Traits = star }, Vector3.Zero);
        var defender = world.AddPlayer(Side.Away, away[3 % away.Count], Vector3.Zero);
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            if (world.Setup.Club(side).Roster.FirstOrDefault(p => p.Position == PlayingPosition.G) is not { } g) continue;
            float end = side == Side.Home ? -1 : 1;
            world.AddPlayer(side, g, new Vector3(end * (RinkSpec.GoalLineX - 1f), 0, 0)).Heading = end < 0 ? MathF.PI / 2 : -MathF.PI / 2;
        }
        MatchFlow.Begin(world, log: false);
        var drill = new MovesDrill(oneTimer, carrier, mate, defender);
        Decisions.Board(world).Drill = drill;
        drill.Reset(world);
    }

    /// <summary>Starts a round over when it has run its time; before decisions.</summary>
    public static void Restart(World world, DecisionBoard board)
    {
        if (board.Drill.TryGet(out var drill) && world.Time - drill.start >= Round) drill.Reset(world);
    }

    /// <summary>In a one-timer round, passes from the point to the slot once; after decisions, so it wins over the carrier's own read.</summary>
    public static void Pass(World world, DecisionBoard board)
    {
        if (board.Drill.TryGet(out var drill)) drill.PassAcross(world, board);
    }

    void PassAcross(World world, DecisionBoard board)
    {
        if (!oneTimer || passed || world.Time - start < PassAt || world.Puck.Carrier != carrier.Id || carrier.Action != PlayerAction.None) return;
        passed = true;
        var to = Vector2.Normalize(new Vector2(mate.Position.X - carrier.Position.X, mate.Position.Z - carrier.Position.Z));
        board.For(carrier.Id).PassTo = mate.Id;
        AiCommand.Act(world, carrier, new PlayerCommand { Move = to, Pass = true }, "drill: pass");
    }

    void Reset(World world)
    {
        int end = PuckHandling.AttackEnd(world, Side.Home);
        float face = end > 0 ? MathF.PI / 2 : -MathF.PI / 2;
        if (oneTimer)
        {
            Place(carrier, new Vector3(end * 18f, 0, -8f), face, Vector3.Zero);
            Place(mate, new Vector3(end * 21f, 0, 3.5f), MathF.PI, Vector3.Zero);
            Place(defender, new Vector3(end * 4f, 0, 12f), -face, Vector3.Zero);
        }
        else
        {
            Place(carrier, new Vector3(end * -8f, 0, 0), face, new Vector3(end * 6f, 0, 0));
            Place(mate, new Vector3(end * -12f, 0, 9f), face, new Vector3(end * 4f, 0, 0));
            Place(defender, new Vector3(end * 6f, 0, 0.3f), -face, new Vector3(end * -3f, 0, 0));
        }
        foreach (var g in world.Players.Where(p => p.IsGoalie))
        {
            float side = PuckHandling.AttackEnd(world, g.Side);
            Place(g, new Vector3(-side * (RinkSpec.GoalLineX - 1f), 0, 0), side > 0 ? MathF.PI / 2 : -MathF.PI / 2, Vector3.Zero);
        }
        var pk = world.Puck;
        pk.Carrier = carrier.Id;
        pk.LastTouch = carrier.Id;
        pk.Position = PuckHandling.Blade(carrier) with { Y = 0 };
        pk.Velocity = Vector3.Zero;
        pk.Live = true;
        world.Match.Phase = MatchPhase.Play;
        var brain = Decisions.Board(world).For(carrier.Id);
        brain.GotAt = world.Time;
        brain.DekeRolled = float.NegativeInfinity;
        start = world.Time;
        passed = false;
    }

    static void Place(Player p, Vector3 at, float heading, Vector3 velocity)
    {
        p.Position = at;
        p.Velocity = velocity;
        p.Heading = heading;
        p.Command = default;
        p.Begin(PlayerAction.None);
    }

    static List<PlayerData> Skaters(ClubData club)
    {
        var list = club.Roster.Where(p => p.Position != PlayingPosition.G).ToList();
        return list.Count > 0 ? list : [new PlayerData { Position = PlayingPosition.C }];
    }
}
