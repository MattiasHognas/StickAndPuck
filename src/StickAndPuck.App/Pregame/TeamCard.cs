using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season.Schedule;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Pregame
{
    /// <summary>What the pre-game stats card (W139) shows for both clubs: the ratings and playstyle of each
    /// (from <see cref="ClubStrengths"/>, for the clubs as they take the ice), and in a season or playoff
    /// game also the regular season's record, table place, last five results and top goal scorer. Outside
    /// a season those rows are None and the card shows a dash. Render only: nothing here touches the
    /// simulation. <see cref="Shown"/> says how far the card is in: it slides in as the arena flyover
    /// starts and out when the close-up ends, the flyover is skipped or the game starts.</summary>
    /// <param name="Home">The home club's side of the card.</param>
    /// <param name="Away">The away club's side of the card.</param>
    /// <param name="Season">Whether the match is a season or playoff game, so the season rows are filled.</param>
    internal sealed record TeamCard(TeamCardSide Home, TeamCardSide Away, bool Season)
    {
        /// <summary>The card waits this long into the flyover before it slides in, in seconds.</summary>
        public const float InAt = 0.3f;

        /// <summary>How long the card takes to slide in and to slide out, in seconds.</summary>
        public const float SlideSeconds = 0.4f;

        /// <summary>Without a skip, the card slides out when the flyover's close-up ends, as the camera settles.</summary>
        public const float OutAt = ArenaFlyover.AerialSeconds + ArenaFlyover.CloseUpSeconds;

        /// <summary>How many recent results the form row shows.</summary>
        public const int FormGames = 5;

        /// <summary>The longest last name the scorer row shows before it is cut.</summary>
        private const int NameLength = 10;

        /// <summary>Gets one side of the card.</summary>
        public TeamCardSide Of(Side side)
        {
            return side == Side.Home ? Home : Away;
        }

        /// <summary>Gathers the card for a world's match.</summary>
        public static TeamCard For(World world)
        {
            Maybe<SeasonState> season = SeasonOf(world);
            return new TeamCard(
                SideOf(world.Setup.Home, season),
                SideOf(world.Setup.Away, season),
                season.HasValue
            );
        }

        /// <summary>Gets how far the card is in, 0 (gone) to 1 (fully shown): it slides in once the flyover is
        /// under way and out when the close-up ends or the flyover is skipped; it is gone with the flyover
        /// off and once the game starts. SAP_SCENE=introcard holds it in through the warm-up.</summary>
        public static float Shown(World world)
        {
            if (!PregameFlow.IsWarmingUp(world))
            {
                return 0;
            }

            if (world.Switches.IntroCardPreview)
            {
                return 1;
            }

            if (ArenaFlyover.Disabled)
            {
                return 0;
            }

            WarmupState s = PregameFlow.State(world);
            float t = s.Elapsed;
            return Smooth((t - InAt) / SlideSeconds) * (1 - Smooth((t - Until(s)) / SlideSeconds));
        }

        /// <summary>Gets a value indicating whether the card has the screen, from the flyover's start until it has slid out, so the HUD's intro panel keeps clear of it.</summary>
        public static bool Covering(World world)
        {
            if (!PregameFlow.IsWarmingUp(world))
            {
                return false;
            }

            WarmupState s = PregameFlow.State(world);
            return world.Switches.IntroCardPreview
                || (!ArenaFlyover.Disabled && s.Elapsed < Until(s) + SlideSeconds);
        }

        /// <summary>When the card starts to slide out: at the end of the close-up, or when the flyover was skipped.</summary>
        private static float Until(WarmupState s)
        {
            return s.FlyoverSkipped ? Math.Min(s.SkippedAt, OutAt) : OutAt;
        }

        /// <summary>The season a match belongs to: the season being played when this is its game or one of its playoff games, with both clubs in it.</summary>
        private static Maybe<SeasonState> SeasonOf(World world)
        {
            SeasonSession session = world.App.Season;
            return
                !session.Current.TryGet(out SeasonState season)
                || !(session.Playing.HasValue || session.Playoffs.Playing.HasValue)
                || !season.Clubs.Contains(world.Setup.Home.Id, StringComparer.Ordinal)
                || !season.Clubs.Contains(world.Setup.Away.Id, StringComparer.Ordinal)
                ? Maybe.None<SeasonState>()
                : season;
        }

        private static TeamCardSide SideOf(ClubData club, Maybe<SeasonState> season)
        {
            ClubStrengths st = ClubStrengths.Of(club);
            TeamCardSide side = new(
                club.Id,
                club.Name,
                st,
                Maybe.None<string>(),
                Maybe.None<string>(),
                [],
                Maybe.None<string>()
            );
            if (!season.TryGet(out SeasonState s))
            {
                return side;
            }

            IReadOnlyList<StandingRow> table = s.Table;
            int place = 0;
            for (int i = 0; i < table.Count; i++)
            {
                if (string.Equals(table[i].ClubId, club.Id, StringComparison.Ordinal))
                {
                    place = i + 1;
                }
            }

            Maybe<string> record = Maybe.None<string>();
            if (
                table
                    .FirstOrNone(r => string.Equals(r.ClubId, club.Id, StringComparison.Ordinal))
                    .TryGet(out StandingRow row)
            )
            {
                record = Text.Format(
                    K.TeamCardRecord,
                    row.Wins + row.OvertimeWins,
                    row.Losses,
                    row.OvertimeLosses,
                    row.Points
                );
            }

            Maybe<string> scorer = s
                .Stats.Regular.Players.Where(p =>
                    !p.IsGoalie
                    && p.Goals > 0
                    && string.Equals(p.Club, club.Id, StringComparison.Ordinal)
                )
                .OrderByDescending(p => p.Goals)
                .ThenByDescending(p => p.Points)
                .ThenBy(p => p.Games)
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .FirstOrNone()
                .Select(p =>
                    Text.Format(
                        K.TeamCardScorer,
                        p.Number,
                        p.Name.Length > NameLength ? p.Name[..NameLength] : p.Name,
                        p.Goals
                    )
                );

            return side with
            {
                Record = record,
                Place =
                    place > 0
                        ? Text.Format(K.TeamCardPlace, place, table.Count)
                        : Maybe.None<string>(),
                Form = Form(s, club.Id),
                Scorer = scorer,
            };
        }

        /// <summary>A club's last <see cref="FormGames"/> results, oldest first, as form dots (win, loss, otloss),
        /// led by "none" when it has played fewer; empty before its first game.</summary>
        internal static IReadOnlyList<string> Form(SeasonState season, string clubId)
        {
            List<string> dots =
            [
                .. season.PlayedBy(clubId).TakeLast(FormGames).Select(g => Dot(g, clubId)),
            ];
            if (dots.Count == 0)
            {
                return dots;
            }

            while (dots.Count < FormGames)
            {
                dots.Insert(0, "none");
            }

            return dots;
        }

        private static string Dot(Fixture game, string clubId)
        {
            if (!game.Result.TryGet(out FixtureResult r))
            {
                return "none";
            }

            bool home = string.Equals(game.Home, clubId, StringComparison.Ordinal);
            int own = home ? r.HomeGoals : r.AwayGoals,
                other = home ? r.AwayGoals : r.HomeGoals;
            return own > other ? "win"
                : own == other || r.Kind != ResultKind.Regulation ? "otloss"
                : "loss";
        }

        private static float Smooth(float x)
        {
            float k = Math.Clamp(x, 0, 1);
            return k * k * (3 - (2 * k));
        }
    }
}
