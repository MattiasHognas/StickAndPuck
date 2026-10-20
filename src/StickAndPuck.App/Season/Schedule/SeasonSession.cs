using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Morale;
using StickAndPuck.App.Season.News;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Trades;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.Schedule
{
    /// <summary>The season being played in this run, if any, and which of its games the current match is.
    /// A scene (or the start screen) calls <see cref="Continue(IReadOnlyList{ClubData})"/> or <see cref="StartNew(IReadOnlyList{ClubData}, string)"/>, builds
    /// the world from <see cref="NextSetup"/>, and <see cref="ScheduleSystem"/> records the result
    /// when that match ends; <see cref="AutosaveSystem"/> then writes the save.</summary>
    public sealed class SeasonSession
    {
        private IReadOnlyList<ClubData> dataClubs = [];
        private IReadOnlyList<ClubData> tradedClubs = [];
        private int tradedDeals = -1;

        /// <summary>Initializes a new instance of the <see cref="SeasonSession"/> class, outside season mode.</summary>
        public SeasonSession()
        {
            Playoffs = new PlayoffSession(this);
        }

        /// <summary>Gets the playoffs of the season.</summary>
        public PlayoffSession Playoffs { get; }

        /// <summary>Gets the season being played, or None outside season mode.</summary>
        public Maybe<SeasonState> Current { get; private set; }

        /// <summary>Gets the game the current match is, or None when the match is not a season game.</summary>
        public Maybe<Fixture> Playing { get; private set; }

        /// <summary>Gets the clubs the season was loaded with, with the season's trades made
        /// (<see cref="SeasonState.Rosters"/>): every match setup, simulated game and stat sheet of the season reads these.</summary>
        public IReadOnlyList<ClubData> Clubs
        {
            get
            {
                if (!Current.TryGet(out SeasonState season))
                {
                    return dataClubs;
                }

                if (tradedDeals != season.Trades.Deals.Count)
                {
                    tradedClubs = season.Rosters(dataClubs);
                    tradedDeals = season.Trades.Deals.Count;
                }

                return tradedClubs;
            }
        }

        /// <summary>Gets the save file this session writes.</summary>
        public string SavePath { get; private set; } = "";

        /// <summary>Gets a value indicating whether the season changed since it was last saved.</summary>
        public bool Unsaved { get; internal set; }

        /// <summary>Continues the season saved for the clubs' league (<see cref="SeasonSave.PathFor"/>).</summary>
        public Maybe<SeasonState> Continue(IReadOnlyList<ClubData> clubs)
        {
            return Continue(clubs, SeasonSave.PathFor(Leagues.Of(clubs)));
        }

        /// <summary>Continues the saved season.</summary>
        public Maybe<SeasonState> Continue(IReadOnlyList<ClubData> clubs, string path)
        {
            if (!SeasonSave.Read(path).TryGet(out SeasonState season))
            {
                return Maybe.None<SeasonState>();
            }

            if (clubs.Count > 0 && season.League != clubs[0].League)
            {
                Console.WriteLine(
                    $"SAP: season save {path} is a {season.League} season, not one of these {clubs[0].League} clubs"
                );
                return Maybe.None<SeasonState>();
            }

            Use(season, clubs, path);
            Unsaved = false;
            return season;
        }

        /// <summary>Starts a new season with a random seed at the clubs' league save (<see cref="SeasonSave.PathFor"/>), replacing that save, and saves it at once.</summary>
        public SeasonState StartNew(IReadOnlyList<ClubData> clubs, string userClub)
        {
            return StartNew(clubs, userClub, Maybe.None<int>());
        }

        /// <summary>Starts a new season at the clubs' league save (<see cref="SeasonSave.PathFor"/>), replacing that save, and saves it at once.</summary>
        public SeasonState StartNew(IReadOnlyList<ClubData> clubs, string userClub, Maybe<int> seed)
        {
            return StartNew(clubs, userClub, seed, SeasonSave.PathFor(Leagues.Of(clubs)));
        }

        /// <summary>Starts a new season, replacing any save, and saves it at once.</summary>
        public SeasonState StartNew(
            IReadOnlyList<ClubData> clubs,
            string userClub,
            Maybe<int> seed,
            string path
        )
        {
            SeasonState season = SeasonState.New(
                clubs,
                userClub,
                seed.TryGet(out int s) ? s : Random.Shared.Next()
            );
            Use(season, clubs, path);
            SeasonSave.Write(season, path);
            Unsaved = false;
            return season;
        }

        /// <summary>The setup for the human's next game, and marks it as the game being played, so its result is
        /// recorded when the match ends. Over when the human's club has no games left.</summary>
        public NextMatch NextSetup(InputDevice device = InputDevice.KeyboardMouse)
        {
            if (!Current.TryGet(out SeasonState season))
            {
                Playing = Maybe.None<Fixture>();
                return NextMatch.Over;
            }
            Playing = season.NextGame;
            return Playing.TryGet(out Fixture game)
                ? new NextMatch.Play(season.SetupFor(game, Clubs, device))
                : NextMatch.Over;
        }

        /// <summary>Leaves season mode: nothing is recorded or saved after this.</summary>
        public void End()
        {
            Current = Maybe.None<SeasonState>();
            Playing = Maybe.None<Fixture>();
            Unsaved = false;
        }

        /// <summary>Records the result of the game being played, settles the rest of its round (and any earlier
        /// games still open), and marks the season for saving. Ignored outside season mode.</summary>
        public bool Finish(FixtureResult result)
        {
            if (!Current.TryGet(out SeasonState season) || !Playing.TryGet(out Fixture game))
            {
                return false;
            }

            season.Record(game.Number, result);
            // once the human's club has played its last game, the rounds after it (an NHL schedule's
            // last rounds need not hold every club) are settled too, so the playoffs can start
            _ = season.SimulateUpTo(
                season.NextGame.HasValue ? game.Round : season.Rounds - 1,
                Clubs
            );
            NewsDesk.AfterGame(season, Clubs); // the news for the next game
            TradeDesk.AfterGame(season, Clubs); // the trade deadline's offers, at its game
            Playing = Maybe.None<Fixture>();
            Unsaved = true;
            return true;
        }

        /// <summary>Plays the human's current game off screen instead of on the ice, with the same
        /// <see cref="QuickSim"/> the other games of the round use (and the playoffs' other series),
        /// then records and saves it exactly as a played game: the result goes into the fixture or the
        /// series, the rest of the round is settled and the save is written. None when there is nothing
        /// to play; once the regular season is over it starts the playoffs and sims the next playoff game.</summary>
        public Maybe<SimmedGame> SimCurrentGame()
        {
            return !Current.TryGet(out SeasonState season) ? Maybe.None<SimmedGame>()
                : season.IsOver ? SimCurrentPlayoffGame()
                : SimCurrentRegularGame(season);
        }

        private Maybe<ClubData> FindClub(string id)
        {
            return Clubs.FirstOrNone(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        }

        private Maybe<SimmedGame> SimCurrentRegularGame(SeasonState season)
        {
            if (
                !season.NextGame.TryGet(out Fixture game)
                || !FindClub(game.Home).TryGet(out ClubData home)
                || !FindClub(game.Away).TryGet(out ClubData away)
            )
            {
                return Maybe.None<SimmedGame>();
            }

            Playing = game;
            FixtureResult result = TeamMorale.Sim(
                season,
                season.InForm(home),
                season.InForm(away),
                season.GameSeed(game.Number)
            );
            if (!Finish(result))
            {
                return Maybe.None<SimmedGame>();
            }

            _ = season.Stats.CatchUp(season, Clubs); // the round's lines now, so the next sim plays with the form they leave
            Save();
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: simmed season game {game.Number + 1}, round {game.Round + 1}: {game.Home} {result.HomeGoals}-{result.AwayGoals} {game.Away} ({result.Kind})"
                )
            );
            return new SimmedGame(
                game.Home,
                game.Away,
                result,
                Playoff: false,
                Text.Format(K.SimRound, game.Round + 1)
            );
        }

        private Maybe<SimmedGame> SimCurrentPlayoffGame()
        {
            if (
                !Playoffs.Open().TryGet(out PlayoffState playoffs)
                || Playoffs.NextSetup(InputDevice.None) is not NextMatch.Play { Setup: var setup }
                || !Playoffs.Playing.TryGet(out PlayoffSeries series)
            )
            {
                return Maybe.None<SimmedGame>();
            }

            int round = playoffs.Rounds.Count - 1,
                index = playoffs.Current.Series.IndexOf(series),
                gameNo = series.Games.Count;
            string stage = playoffs.Current.Name,
                title = playoffs.CurrentTitle; // read before Finish, which may draw the next round
            FixtureResult result = QuickSim.Play(
                setup.Home,
                setup.Away,
                playoffs.GameSeed(round, index, gameNo)
            );
            if (result.Kind == ResultKind.Shootout && !Playoffs.Rules.Overtime.HasShootout)
            {
                result = result with { Kind = ResultKind.Overtime }; // the mode's overtime rules, SHL playoffs have no shootout
            }

            if (!Playoffs.Finish(result))
            {
                return Maybe.None<SimmedGame>();
            }

            Save();
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: simmed playoff game {gameNo + 1} of {stage}: {setup.Home.Id} {result.HomeGoals}-{result.AwayGoals} {setup.Away.Id} ({result.Kind})"
                )
            );
            return new SimmedGame(
                setup.Home.Id,
                setup.Away.Id,
                result,
                Playoff: true,
                Text.Format(K.SimStage, title, gameNo + 1)
            );
        }

        /// <summary>Answers a trade offer of the deadline (<see cref="TradeDesk"/>) and saves the season: accepted, the
        /// players change clubs for the rest of the season. Returns the trade made, or None.</summary>
        public Maybe<Trade> AnswerTrade(TradeOffer offer, bool accept)
        {
            if (!Current.TryGet(out SeasonState season) || !offer.IsOpen)
            {
                return Maybe.None<Trade>();
            }

            Maybe<Trade> trade = Maybe.None<Trade>();
            if (accept)
            {
                trade = TradeDesk.Accept(season, dataClubs, offer);
            }
            else
            {
                TradeDesk.Decline(offer);
            }

            Console.WriteLine(
                trade.TryGet(out Trade made)
                    ? string.Create(
                        CultureInfo.InvariantCulture,
                        $"SAP: trade: {made.Club} #{made.Number} {offer.GiveName} to {made.OtherClub} as #{made.NewNumber}, {made.OtherClub} #{made.OtherNumber} {offer.GetName} to {made.Club} as #{made.OtherNewNumber}"
                    )
                    : $"SAP: trade: {season.UserClub} declined {offer.Club}'s offer ({offer.GiveName} for {offer.GetName})"
            );
            Unsaved = true;
            Save();
            return trade;
        }

        /// <summary>Writes the save when the season changed, as <see cref="AutosaveSystem"/> does after a played match.</summary>
        private void Save()
        {
            if (!Unsaved || !Current.TryGet(out SeasonState season))
            {
                return;
            }

            SeasonSave.Write(season, SavePath);
            Unsaved = false;
        }

        private void Use(SeasonState season, IReadOnlyList<ClubData> clubs, string path)
        {
            Current = season;
            dataClubs = clubs;
            tradedDeals = -1;
            SavePath = path;
            Playing = Maybe.None<Fixture>();
        }
    }
}
