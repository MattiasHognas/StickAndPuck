using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Morale;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Stats;

namespace StickAndPuck.App.Season.News
{
    /// <summary>Season news events (W130). After each of the human's regular-season games the events in play
    /// use up a game, then one card of the <see cref="NewsDeck"/> may be drawn for the human's club: a
    /// one-in-<see cref="RuleSet.NewsEvery"/> chance, and the card by weight among those that fit the
    /// next game. The draw comes from a stream fixed by the season seed and the games played, so a season
    /// tells the same story every time. Events in play are saved in <see cref="SeasonState.News"/> and
    /// reach a match only through its <see cref="MatchSetup"/>: trait points in <see cref="MatchSetup.Form"/>
    /// and a tired start in <see cref="MatchSetup.StartEnergy"/>, both stored in replays; a morale card
    /// nudges <see cref="TeamMorale"/> when drawn, which the setup's morale carries.</summary>
    public static class NewsDesk
    {
        /// <summary>The most events in play at once; no card is drawn while this many run.</summary>
        public const int MaxActive = 2;

        /// <summary>The stream name of the draw.</summary>
        private const string Stream = "news";

        /// <summary>Gets the human's club's regular-season games played.</summary>
        public static int Played(SeasonState season)
        {
            return season.PlayedBy(season.UserClub).Count();
        }

        /// <summary>Uses up a game of every event in play and maybe draws a new one; <see cref="SeasonSession.Finish"/>
        /// calls it after the human's regular-season game is recorded.</summary>
        public static void AfterGame(SeasonState season, IReadOnlyList<ClubData> clubs)
        {
            AfterGame(season, clubs, NewsDeck.Default);
        }

        /// <summary>Uses up a game of every event in play and maybe draws a new one from a deck.</summary>
        public static void AfterGame(
            SeasonState season,
            IReadOnlyList<ClubData> clubs,
            NewsDeck deck
        )
        {
            Tick(season);
            if (Draw(season, clubs, deck).TryGet(out NewsEvent drawn))
            {
                Start(season, drawn);
            }
        }

        /// <summary>Uses up one game of every event in play and drops the finished ones.</summary>
        public static void Tick(SeasonState season)
        {
            foreach (NewsEvent e in season.News)
            {
                e.GamesLeft--;
            }

            foreach (NewsEvent done in season.News.Where(e => e.GamesLeft <= 0).ToList())
            {
                _ = season.News.Remove(done);
            }
        }

        /// <summary>Draws the event for the human's next game, or None: news is off, enough events run, the
        /// season has no next game, or the dice say not this time.</summary>
        public static Maybe<NewsEvent> Draw(
            SeasonState season,
            IReadOnlyList<ClubData> clubs,
            NewsDeck deck
        )
        {
            int every = season.Rules.Or(RuleSet.Shl).NewsEvery;
            if (
                every <= 0
                || season.News.Count >= MaxActive
                || !season.NextGame.TryGet(out Fixture next)
                || !Club(clubs, season.UserClub).TryGet(out ClubData club)
            )
            {
                return Maybe.None<NewsEvent>();
            }

            Random dice = Dice(season);
            if (dice.Next(every) != 0)
            {
                return Maybe.None<NewsEvent>();
            }

            bool home = string.Equals(next.Home, season.UserClub, StringComparison.Ordinal);
            List<NewsCard> cards =
            [
                .. deck.Cards.Where(c =>
                    c.Weight > 0
                    && Fits(c.Venue, home)
                    && !season.News.Any(e => string.Equals(e.Id, c.Id, StringComparison.Ordinal))
                ),
            ];
            int total = cards.Sum(c => c.Weight);
            return total <= 0
                ? Maybe.None<NewsEvent>()
                : Build(Pick(cards, dice.Next(total)), club, dice, Played(season));
        }

        /// <summary>Gets the card a roll lands on, from 0 to the cards' total weight less one: each card takes
        /// as many rolls as its weight, in deck order.</summary>
        public static NewsCard Pick(IReadOnlyList<NewsCard> cards, int roll)
        {
            foreach (NewsCard card in cards)
            {
                if (roll < card.Weight)
                {
                    return card;
                }

                roll -= card.Weight;
            }

            return cards[^1];
        }

        /// <summary>Makes a card an event for a club: its games and, for a traits card, its skaters.</summary>
        public static NewsEvent Build(NewsCard card, ClubData club, Random dice, int played)
        {
            int games = dice.Next(card.GamesMin, Math.Max(card.GamesMin, card.GamesMax) + 1);
            List<int> players = [];
            if (card.Effect == NewsEffect.Traits)
            {
                List<PlayerData> skaters =
                [
                    .. club
                        .Roster.Where(p => p.Position != PlayingPosition.G)
                        .OrderBy(p => p.Number),
                ];
                int count = Math.Min(
                    skaters.Count,
                    dice.Next(card.PlayersMin, Math.Max(card.PlayersMin, card.PlayersMax) + 1)
                );
                IEnumerable<PlayerData> picked = card.Pick switch
                {
                    NewsPick.Best => skaters.OrderByDescending(Overall).ThenBy(p => p.Number),
                    NewsPick.Weakest => skaters.OrderBy(Overall).ThenBy(p => p.Number),
                    NewsPick.Random => skaters.OrderBy(_ => dice.Next()),
                    _ => skaters,
                };
                players = [.. picked.Take(count).Select(p => p.Number).Order()];
            }

            return new NewsEvent
            {
                Id = card.Id,
                Text = card.Text,
                Effect = card.Effect,
                Club = club.Id,
                Players = players,
                Amount = card.Amount,
                Energy = card.Energy,
                GamesLeft = Math.Max(1, games),
                Drawn = played,
            };
        }

        /// <summary>Puts an event in play: it is saved with the season, and a morale event moves the club's morale now.</summary>
        public static void Start(SeasonState season, NewsEvent e)
        {
            season.News.Add(e);
            if (e.Effect == NewsEffect.Morale)
            {
                _ = TeamMorale.Nudge(season, e.Club, e.Amount);
            }
        }

        /// <summary>Puts a card in play for the human's club at once, whatever the dice say; the dressing room
        /// preview uses it (SAP_NEWS). None when the card or the club is unknown.</summary>
        public static Maybe<NewsEvent> Force(
            SeasonState season,
            IReadOnlyList<ClubData> clubs,
            NewsDeck deck,
            string id
        )
        {
            if (
                !deck.Card(id).TryGet(out NewsCard card)
                || !Club(clubs, season.UserClub).TryGet(out ClubData club)
            )
            {
                return Maybe.None<NewsEvent>();
            }

            NewsEvent e = Build(card, club, Dice(season), Played(season));
            Start(season, e);
            return e;
        }

        /// <summary>Adds the trait points of the events in play to a match's nudges, by <see cref="FormTable.Key"/>.</summary>
        public static void AddNudges(SeasonState season, IDictionary<string, int> nudges)
        {
            if (season.Rules.Or(RuleSet.Shl).NewsEvery <= 0)
            {
                return;
            }

            foreach (NewsEvent e in season.News.Where(e => e.Effect == NewsEffect.Traits))
            {
                foreach (int number in e.Players)
                {
                    string key = FormTable.Key(e.Club, number);
                    nudges[key] = nudges.TryGetValue(key, out int n) ? n + e.Amount : e.Amount;
                }
            }
        }

        /// <summary>Gets the energy each club starts a game with, home then away: 1, or less after a travel delay.</summary>
        public static IReadOnlyList<float> StartEnergy(SeasonState season, Fixture game)
        {
            float Of(string club)
            {
                return season.Rules.Or(RuleSet.Shl).NewsEvery <= 0
                    ? 1f
                    : season
                        .News.Where(e =>
                            e.Effect == NewsEffect.Energy
                            && string.Equals(e.Club, club, StringComparison.Ordinal)
                        )
                        .Select(e => Math.Clamp(e.Energy, 0f, 1f))
                        .DefaultIfEmpty(1f)
                        .Min();
            }

            return [Of(game.Home), Of(game.Away)];
        }

        /// <summary>A skater's average skating, shooting and playing trait, to find the best and the weakest.</summary>
        public static double Overall(PlayerData p)
        {
            Traits t = p.Traits;
            return (
                    t.Acceleration
                    + t.TopSpeed
                    + t.ShotAccuracy
                    + t.ShotPower
                    + t.Checking
                    + t.PuckControl
                    + t.HockeyIq
                ) / 7.0;
        }

        private static bool Fits(NewsVenue venue, bool home)
        {
            return venue switch
            {
                NewsVenue.Home => home,
                NewsVenue.Away => !home,
                NewsVenue.Any => true,
                _ => true,
            };
        }

        /// <summary>The draw's dice: fixed by the season seed and the human's games played.</summary>
        private static Random Dice(SeasonState season)
        {
            int played = Played(season);
            return Seeds.Dice(
                season.Seed,
                Stream + played.ToString(CultureInfo.InvariantCulture),
                7_919 * (played + 1)
            );
        }

        private static Maybe<ClubData> Club(IReadOnlyList<ClubData> clubs, string id)
        {
            return clubs.FirstOrNone(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        }
    }
}
