using System.Text.Json.Serialization;
using StickAndPuck.App.Core;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season;

/// <summary>
/// One player's totals over a season, a playoff run or an arcade run. A player is known by club
/// id and jersey number, so the line survives a restart and a roster edit that keeps the number.
/// </summary>
public sealed class PlayerStats
{
    /// <summary>Gets or sets the club id.</summary>
    public string Club { get; set; } = "";

    /// <summary>Gets or sets the jersey number.</summary>
    public int Number { get; set; }

    /// <summary>Gets or sets the last name, as the HUD shows it.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the playing position.</summary>
    public PlayingPosition Position { get; set; }

    /// <summary>Gets or sets the games played (on the ice at least once).</summary>
    public int Games { get; set; }

    /// <summary>Gets or sets the goals.</summary>
    public int Goals { get; set; }

    /// <summary>Gets or sets the assists.</summary>
    public int Assists { get; set; }

    /// <summary>Gets or sets the shots.</summary>
    public int Shots { get; set; }

    /// <summary>Gets or sets the body checks landed.</summary>
    public int Hits { get; set; }

    /// <summary>Gets or sets the saves, for goalies.</summary>
    public int Saves { get; set; }

    /// <summary>Gets or sets how often the player was player of the game.</summary>
    public int Stars { get; set; }

    /// <summary>Gets the points: goals plus assists.</summary>
    [JsonIgnore]
    public int Points => Goals + Assists;

    /// <summary>Gets a value indicating whether this is a goalie's line.</summary>
    [JsonIgnore]
    public bool IsGoalie => Position == PlayingPosition.G;

    /// <summary>Gets "#NR NAME".</summary>
    [JsonIgnore]
    public string Label => $"#{Number} {Name}";

    /// <summary>Gets a value indicating whether this line is for the given player.</summary>
    /// <param name="club">The club id.</param>
    /// <param name="number">The jersey number.</param>
    /// <returns>True when it is.</returns>
    public bool Is(string club, int number)
    {
        return Number == number && string.Equals(Club, club, StringComparison.Ordinal);
    }

    /// <summary>Adds a match's line to this one.</summary>
    /// <param name="line">The match line.</param>
    public void Add(MatchStatLine line)
    {
        if (line.Played) Games++;
        Goals += line.Goals;
        Assists += line.Assists;
        Shots += line.Shots;
        Hits += line.Hits;
        Saves += line.Saves;
    }
}

/// <summary>What one player did in one match.</summary>
public sealed class MatchStatLine
{
    /// <summary>Gets or sets the club id.</summary>
    public string Club { get; set; } = "";

    /// <summary>Gets or sets the club side in that match.</summary>
    public Side Side { get; set; }

    /// <summary>Gets or sets the jersey number.</summary>
    public int Number { get; set; }

    /// <summary>Gets or sets the last name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the playing position.</summary>
    public PlayingPosition Position { get; set; }

    /// <summary>Gets or sets a value indicating whether the player was on the ice at some point.</summary>
    public bool Played { get; set; }

    /// <summary>Gets or sets the goals.</summary>
    public int Goals { get; set; }

    /// <summary>Gets or sets the assists.</summary>
    public int Assists { get; set; }

    /// <summary>Gets or sets the shots.</summary>
    public int Shots { get; set; }

    /// <summary>Gets or sets the body checks landed.</summary>
    public int Hits { get; set; }

    /// <summary>Gets or sets the saves, for goalies.</summary>
    public int Saves { get; set; }

    /// <summary>Gets the points.</summary>
    public int Points => Goals + Assists;

    /// <summary>Gets a value indicating whether this is a goalie's line.</summary>
    public bool IsGoalie => Position == PlayingPosition.G;

    /// <summary>Gets "#NR NAME".</summary>
    public string Label => $"#{Number} {Name}";

    /// <summary>A line for a roster player, nothing done yet.</summary>
    /// <param name="club">The club id.</param>
    /// <param name="side">The club side in the match.</param>
    /// <param name="player">The roster entry.</param>
    /// <returns>The line.</returns>
    public static MatchStatLine For(string club, Side side, PlayerData player)
    {
        return new MatchStatLine { Club = club, Side = side, Number = player.Number, Name = player.LastName, Position = player.Position };
    }
}

/// <summary>The player of one game, as kept with the season or the arcade run.</summary>
/// <param name="Game">Which game: "g12" for season game 13, "p0.1.2" for playoff round 1 series 2 game 3, "a3" for arcade stage 4.</param>
/// <param name="Home">The home club id.</param>
/// <param name="Away">The away club id.</param>
/// <param name="HomeGoals">The home goals.</param>
/// <param name="AwayGoals">The away goals.</param>
/// <param name="Club">The player's club id.</param>
/// <param name="Number">The jersey number.</param>
/// <param name="Name">The last name.</param>
/// <param name="Goals">Goals in the game.</param>
/// <param name="Assists">Assists in the game.</param>
/// <param name="Saves">Saves in the game, for a goalie.</param>
public sealed record PlayerOfGameAward(string Game, string Home, string Away, int HomeGoals, int AwayGoals, string Club, int Number, string Name, int Goals, int Assists, int Saves)
{
    /// <summary>Gets "#NR NAME".</summary>
    [JsonIgnore]
    public string Label => $"#{Number} {Name}";

    /// <summary>Gets "2G 1A" or "31 SV", what earned the award.</summary>
    [JsonIgnore]
    public string Summary => Saves > 0 && Goals + Assists == 0 ? Text.Format(K.SummarySaves, Saves) : Text.Format(K.SummaryPoints, Goals, Assists);
}

/// <summary>
/// Every player's totals over a run of games, and the leaders. W33's leaders page reads
/// <see cref="Leaders"/> and <see cref="Goalies"/>; the season keeps one of these for the regular
/// season and one for the playoffs, the arcade run one per run.
/// </summary>
public sealed class StatTable
{
    /// <summary>Gets or sets every player with a line, in no particular order.</summary>
    public List<PlayerStats> Players { get; set; } = [];

    /// <summary>Gets or sets the games credited.</summary>
    public int GamesCredited { get; set; }

    /// <summary>Gets the line for a player, adding an empty one if needed.</summary>
    /// <param name="club">The club id.</param>
    /// <param name="number">The jersey number.</param>
    /// <param name="name">The last name, for a new line.</param>
    /// <param name="position">The position, for a new line.</param>
    /// <returns>The line.</returns>
    public PlayerStats Line(string club, int number, string name, PlayingPosition position)
    {
        var line = Players.Find(p => p.Is(club, number));
        if (line == null)
        {
            line = new PlayerStats { Club = club, Number = number, Name = name, Position = position };
            Players.Add(line);
        }
        return line;
    }

    /// <summary>Gets a player's line, or null when they have none yet.</summary>
    /// <param name="club">The club id.</param>
    /// <param name="number">The jersey number.</param>
    /// <returns>The line or null.</returns>
    public PlayerStats? Find(string club, int number)
    {
        return Players.Find(p => p.Is(club, number));
    }

    /// <summary>Adds a match's lines to the totals.</summary>
    /// <param name="sheet">The match.</param>
    public void Add(MatchStatSheet sheet)
    {
        foreach (var line in sheet.Lines) Line(line.Club, line.Number, line.Name, line.Position).Add(line);
        if (sheet.PlayerOfGame is { } star) Line(star.Club, star.Number, star.Name, star.Position).Stars++;
        GamesCredited++;
    }

    /// <summary>Gets the scoring leaders: by points, then goals, then fewer games, then name.</summary>
    /// <param name="count">How many.</param>
    /// <param name="club">Only this club's players, or null for every club.</param>
    /// <returns>The leaders, best first.</returns>
    public IReadOnlyList<PlayerStats> Leaders(int count = 10, string? club = null)
    {
        return Players.Where(p => !p.IsGoalie && (club == null || string.Equals(p.Club, club, StringComparison.Ordinal)) && p.Points > 0)
            .OrderByDescending(p => p.Points).ThenByDescending(p => p.Goals).ThenBy(p => p.Games).ThenBy(p => p.Name, StringComparer.Ordinal)
            .Take(count).ToList();
    }

    /// <summary>Gets the goal scorers, most goals first.</summary>
    /// <param name="count">How many.</param>
    /// <returns>The scorers.</returns>
    public IReadOnlyList<PlayerStats> Scorers(int count = 10)
    {
        return Players.Where(p => !p.IsGoalie && p.Goals > 0)
            .OrderByDescending(p => p.Goals).ThenByDescending(p => p.Points).ThenBy(p => p.Games).ThenBy(p => p.Name, StringComparer.Ordinal)
            .Take(count).ToList();
    }

    /// <summary>Gets the goalies, most saves first.</summary>
    /// <param name="count">How many.</param>
    /// <returns>The goalies.</returns>
    public IReadOnlyList<PlayerStats> Goalies(int count = 10)
    {
        return Players.Where(p => p.IsGoalie && p.Games > 0)
            .OrderByDescending(p => p.Saves).ThenBy(p => p.Games).ThenBy(p => p.Name, StringComparer.Ordinal)
            .Take(count).ToList();
    }

    /// <summary>Gets the players most often player of the game.</summary>
    /// <param name="count">How many.</param>
    /// <returns>The players, most awards first.</returns>
    public IReadOnlyList<PlayerStats> Stars(int count = 10)
    {
        return Players.Where(p => p.Stars > 0)
            .OrderByDescending(p => p.Stars).ThenByDescending(p => p.Points).ThenBy(p => p.Name, StringComparer.Ordinal)
            .Take(count).ToList();
    }
}
