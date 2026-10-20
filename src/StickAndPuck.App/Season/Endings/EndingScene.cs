using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Endings
{
    /// <summary>SAP_ENDING=champions|runnersup|semifinal|seasonover|relegated|fired: jumps straight into that
    /// end-of-season scene (SAP_SCENE can be left unset). SAP_CLUB (or SAP_HOME) is the user's club, an
    /// SHL or NHL id, and the league follows it; SAP_AWAY is the other club in the final, the semi-final or
    /// on the ice, by default the club's rival or the next club of its league. Presentation only.</summary>
    public sealed class EndingScene : IScene
    {
        /// <summary>The scene's SAP_SCENE name.</summary>
        public const string SceneName = "ending";

        /// <inheritdoc/>
        public string Name => SceneName;

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            string value = Env.Get("SAP_ENDING");
            if (!EndingStage.Parse(value).TryGet(out EndingKind kind))
            {
                throw new InvalidOperationException(
                    $"SAP_ENDING={value}: use {string.Join(", ", Enum.GetNames<EndingKind>().Select(n => n.ToLowerInvariant()))}"
                );
            }

            ClubData home = Club(
                    clubs,
                    Env.Get("SAP_CLUB") is { Length: > 0 } c ? c : Env.Get("SAP_HOME")
                )
                .Or(clubs[0]);
            IReadOnlyList<ClubData> league =
                home.League == League.Nhl ? Clubs.LoadNhl() : Clubs.Load(League.Shl);
            ClubData away = Club(clubs, Env.Get("SAP_AWAY")).Or(Opponent(home, league));
            World world = new(
                new MatchSetup
                {
                    Home = home,
                    Away = away,
                    Rivalry = Rivals.Are(home.Id, away.Id),
                },
                settings
            );
            world.Dev.Ending.Set(new EndingStage(kind));
            Console.WriteLine(
                $"SAP: ending {kind.ToString().ToLowerInvariant()}: {home.Name} (v {away.Name})"
            );
            return world;
        }

        /// <summary>A club by id from the run's clubs, else from either league's files; none for an empty id.</summary>
        private static Maybe<ClubData> Club(IReadOnlyList<ClubData> clubs, string id)
        {
            if (id.Length == 0)
            {
                return Maybe.None<ClubData>();
            }

            bool Match(ClubData c)
            {
                return string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase);
            }

            return clubs.FirstOrNone(Match).TryGet(out ClubData found) ? found
                : Clubs.LoadNhl().FirstOrNone(Match).TryGet(out found) ? found
                : Clubs.Load(League.Shl).FirstOrNone(Match).TryGet(out found) ? found
                : throw new InvalidOperationException($"{id} is not a club id");
        }

        /// <summary>The club's rival in its league, else the next club after it in the league's list.</summary>
        private static ClubData Opponent(ClubData home, IReadOnlyList<ClubData> league)
        {
            foreach ((string a, string b) in Rivals.Pairs)
            {
                string other =
                    string.Equals(a, home.Id, StringComparison.Ordinal) ? b
                    : string.Equals(b, home.Id, StringComparison.Ordinal) ? a
                    : "";
                if (
                    league
                        .FirstOrNone(c => string.Equals(c.Id, other, StringComparison.Ordinal))
                        .TryGet(out ClubData rival)
                )
                {
                    return rival;
                }
            }

            int at = Math.Max(
                0,
                league
                    .ToList()
                    .FindIndex(c => string.Equals(c.Id, home.Id, StringComparison.Ordinal))
            );
            return league.Count > 1 ? league[(at + 1) % league.Count] : home;
        }
    }
}
