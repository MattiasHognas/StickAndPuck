using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Core
{
    /// <summary>The string table keys a league's screens show (W118): the standings, playoff and leaders
    /// headings, the champions and their trophy (the Le Mat trophy or the Stanley Cup) and the playoff
    /// rounds, so no SHL text reaches an NHL screen and the other way round. Every screen that names
    /// the league picks its keys here by <see cref="Of"/>.</summary>
    /// <param name="FinalTable">The standings heading once the regular season is over.</param>
    /// <param name="Round">The standings heading during the season: round {0} of {1}.</param>
    /// <param name="Playoffs">The bracket heading.</param>
    /// <param name="ChampionsOf">The bracket line under a decided final: champions: {0}.</param>
    /// <param name="Champions">The champions, as the trophy screen heads them.</param>
    /// <param name="Trophy">The champion's trophy.</param>
    /// <param name="LeadersSeason">The season leaders heading: {0} is the list shown.</param>
    /// <param name="LeadersPlayoff">The playoff leaders heading: {0} is the list shown.</param>
    /// <param name="Rounds">The playoff rounds' names, first to last.</param>
    public sealed record LeagueText(
        string FinalTable,
        string Round,
        string Playoffs,
        string ChampionsOf,
        string Champions,
        string Trophy,
        string LeadersSeason,
        string LeadersPlayoff,
        IReadOnlyList<string> Rounds
    )
    {
        /// <summary>The SHL's keys: play-in, quarter-finals, semi-finals and the final for the Le Mat trophy.</summary>
        public static readonly LeagueText Shl = new(
            K.ShlFinalTable,
            K.ShlRound,
            K.ShlPlayoffs,
            K.ShlChampionsOf,
            K.ShlChampions,
            K.TrophyLeMat,
            K.LeadersHeadingSeason,
            K.LeadersHeadingPlayoff,
            [K.RoundPlayIn, K.RoundQuarterFinals, K.RoundSemiFinals, K.RoundFinal]
        );

        /// <summary>The NHL's keys: first and second round, conference finals and the Stanley Cup final.</summary>
        public static readonly LeagueText Nhl = new(
            K.NhlFinalTable,
            K.NhlRound,
            K.NhlPlayoffs,
            K.NhlChampionsOf,
            K.NhlChampions,
            K.TrophyStanleyCup,
            K.NhlLeadersHeadingSeason,
            K.NhlLeadersHeadingPlayoff,
            [K.RoundFirst, K.RoundSecond, K.RoundConferenceFinals, K.RoundStanleyCupFinal]
        );

        /// <summary>Gets a league's keys.</summary>
        public static LeagueText Of(League league)
        {
            return league == League.Nhl ? Nhl : Shl;
        }

        /// <summary>Gets the key of a playoff round's name, from 0; the last name past the end.</summary>
        public string RoundKey(int round)
        {
            return Rounds[Math.Clamp(round, 0, Rounds.Count - 1)];
        }

        /// <summary>Gets the standings heading in the language shown: the round being played, or the final table.</summary>
        public string TableHeading(bool over, int round, int rounds)
        {
            return over ? Text.Get(FinalTable) : Text.Format(Round, round, rounds);
        }

        /// <summary>Gets the trophy screen heading in the language shown: the champions and their trophy.</summary>
        public string TrophyHeading()
        {
            return Text.Format(K.TrophyHeading, Text.Get(Champions), Text.Get(Trophy));
        }
    }
}
