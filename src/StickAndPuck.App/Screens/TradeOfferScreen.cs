using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Trades;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>A trade offer at the deadline (W135), over the dressing room right after the result that brought
    /// it (<see cref="ScreenFlow.AfterSeasonGame"/>) or from the room's TRADE OFFERS row: who is calling and
    /// where they stand, the player they want and the one they offer side by side (position, kind of
    /// player, traits and overall), the coach between them and his read on it, then ACCEPT or DECLINE.
    /// With two offers the second follows the first; once both are answered, or on back, the dressing
    /// room (or the table) comes back. An offer left open lapses when the next game is played.</summary>
    internal sealed class TradeOfferScreen(AppState app) : MenuScreen(app)
    {
        private const int PanelW = 440,
            Line = 11,
            TraitRow = 10,
            PortraitW = 64,
            PortraitH = 80,
            CardH = Line + Line + Line + (5 * TraitRow) + Line + 2,
            PitchLines = 2,
            PanelH =
                5
                + ScreenArt.Row
                + 2
                + Line
                + Line
                + 6
                + CardH
                + 6
                + (PitchLines * Line)
                + 6
                + 4
                + Line
                + 6; // without the menu rows

        /// <inheritdoc/>
        public override IReadOnlyList<MenuItem> Items()
        {
            return
            [
                new MenuItem(Text.Get(K.TradeAccept), () => Answer(accept: true)),
                new MenuItem(Text.Get(K.TradeDecline), () => Answer(accept: false)),
            ];
        }

        /// <inheritdoc/>
        protected override void Back()
        {
            Leave();
        }

        /// <summary>Gets the offer shown: the first still open.</summary>
        private Maybe<TradeOffer> Offer()
        {
            return App.Season.Current.TryGet(out SeasonState season)
                ? season.Trades.Offers.FirstOrNone(o => o.IsOpen)
                : Maybe.None<TradeOffer>();
        }

        private void Answer(bool accept)
        {
            if (Offer().TryGet(out TradeOffer offer))
            {
                _ = App.Season.AnswerTrade(offer, accept);
            }

            if (!Offer().HasValue)
            {
                Leave();
            }
        }

        /// <summary>Back to the dressing room this was opened from, or on to the screen after the result.</summary>
        private void Leave()
        {
            if (Flow.Screens.Count >= 2 && Flow.Screens[^2] is DressingRoomScreen)
            {
                Flow.Pop();
                return;
            }

            Flow.Replace(Flow.AfterTrades());
        }

        /// <summary>Gets the string key of a kind of player.</summary>
        public static string ProfileKey(TradeProfile profile)
        {
            return profile switch
            {
                TradeProfile.Sniper => K.TradeSniper,
                TradeProfile.Speedster => K.TradeSpeedster,
                TradeProfile.Grinder => K.TradeGrinder,
                TradeProfile.Playmaker => K.TradePlaymaker,
                _ => throw new NotSupportedException("no name for " + profile),
            };
        }

        /// <summary>Gets the string key of a position's name.</summary>
        public static string PositionKey(PlayingPosition position)
        {
            return position switch
            {
                PlayingPosition.C => K.TradePosC,
                PlayingPosition.LW => K.TradePosLW,
                PlayingPosition.RW => K.TradePosRW,
                PlayingPosition.D => K.TradePosD,
                PlayingPosition.G => K.TradePosG,
                _ => throw new NotSupportedException("no name for " + position),
            };
        }

        /// <summary>Gets what the coach says about an offer: the kinds of player, then how they compare.</summary>
        public static string Pitch(PlayerData give, PlayerData get)
        {
            int gap = TradeDesk.Value(get) - TradeDesk.Value(give);
            string compare = Text.Get(
                gap >= 2 ? K.TradeBetter
                : gap <= -1 ? K.TradeWorse
                : K.TradeEven
            );
            return Text.Format(
                    K.TradePitch,
                    Text.Get(ProfileKey(TradeDesk.Profile(get))),
                    Text.Get(ProfileKey(TradeDesk.Profile(give)))
                )
                + " "
                + compare;
        }

        /// <inheritdoc/>
        public override void Draw(ScreenArt art, World world)
        {
            if (
                !App.Season.Current.TryGet(out SeasonState season)
                || !Offer().TryGet(out TradeOffer offer)
                || !TradeDesk
                    .Players(season, Flow.Clubs, offer)
                    .TryGet(out (PlayerData Give, PlayerData Get) pair)
            )
            {
                return;
            }

            KitColours kit = Flow
                .Clubs.FirstOrNone(c =>
                    string.Equals(c.Id, season.UserClub, StringComparison.Ordinal)
                )
                .Select(c => c.Home)
                .Or(new KitColours());
            bool room = art.DrawDressingRoom(kit);
            IReadOnlyList<MenuItem> items = Items();
            int h = PanelH + (items.Count * ScreenArt.Row);
            int py = Math.Max(2, (Game.Height - h - 14) / 2);
            (int x, int top) = room
                ? art.RoomPanel(kit, Text.Get(K.TradeOffer), PanelW, h, py)
                : art.Panel(Text.Get(K.TradeOffer), PanelW, h, py);
            int cx = Game.Width / 2;
            top = DrawCaller(art, season, offer, cx, top);

            int cardW = (PanelW - 20 - PortraitW - 20) / 2;
            int gap = TradeDesk.Value(pair.Get) - TradeDesk.Value(pair.Give);
            DrawCard(art, x + 10, top, cardW, Text.Get(K.TradeWeGive), pair.Give, gap < 0);
            DrawCard(
                art,
                x + PanelW - 10 - cardW,
                top,
                cardW,
                Text.Get(K.TradeWeGet),
                pair.Get,
                gap > 0
            );
            art.DrawCoach(
                cx - (PortraitW / 2),
                top + ((CardH - PortraitH) / 2),
                PortraitW,
                PortraitH,
                Text.Get(K.RoomCoach),
                gap >= 2 ? CoachMood.Pleased : CoachMood.Neutral
            );
            top += CardH + 6;

            int pitchTop = top;
            foreach (
                string line in DressingRoomScreen
                    .Wrap(art, Pitch(pair.Give, pair.Get), PanelW - 20)
                    .Take(PitchLines)
            )
            {
                art.Centred(line, cx, top, art.Text);
                top += Line;
            }

            top = pitchTop + (PitchLines * Line) + 6;
            art.Menu(items, Selected, x + 140, PanelW - 280, top);
            top += (items.Count * ScreenArt.Row) + 4;
            art.Centred(
                art.Clip(
                    Text.Get(Selected == 0 ? K.TradeAcceptHint : K.TradeDeclineHint),
                    PanelW - 20
                ),
                cx,
                top,
                ScreenArt.Muted
            );
        }

        /// <summary>The offer's number and the club making it, with its place in the table; returns the next top.</summary>
        private int DrawCaller(ScreenArt art, SeasonState season, TradeOffer offer, int cx, int top)
        {
            int index = season.Trades.Offers.IndexOf(offer) + 1;
            art.Centred(
                Text.Format(K.TradeDeadlineLine, index, season.Trades.Offers.Count),
                cx,
                top,
                ScreenArt.Muted
            );
            top += Line;
            int place = season
                .Table.Select((r, i) => (r.ClubId, Place: i + 1))
                .FirstOrNone(r => string.Equals(r.ClubId, offer.Club, StringComparison.Ordinal))
                .Select(r => r.Place)
                .Or(0);
            string club = Flow
                .Clubs.FirstOrNone(c => string.Equals(c.Id, offer.Club, StringComparison.Ordinal))
                .Select(c => c.Name)
                .Or(offer.Club);
            art.Centred(Text.Format(K.TradeFrom, club, place), cx, top, art.Gold);
            return top + Line + 6;
        }

        /// <summary>One player: the side's heading, number and name, position and kind of player, his traits as
        /// bars and his overall (in gold on the better player's side).</summary>
        private static void DrawCard(
            ScreenArt art,
            int x,
            int top,
            int w,
            string heading,
            PlayerData player,
            bool better
        )
        {
            art.Left(heading, x, top, ScreenArt.Muted);
            top += Line;
            art.Left(
                art.Clip(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"#{player.Number} {player.FirstName} {player.LastName}"
                    ),
                    w
                ),
                x,
                top,
                art.Gold
            );
            top += Line;
            art.Left(
                art.Clip(
                    Text.Get(PositionKey(player.Position))
                        + "  "
                        + Text.Get(ProfileKey(TradeDesk.Profile(player))),
                    w
                ),
                x,
                top,
                art.Text
            );
            top += Line;
            Traits t = player.Traits;
            (string Key, int Value)[] rows =
            [
                (K.TradeSpeed, (t.Acceleration + t.TopSpeed) / 2),
                (K.TradeShot, (t.ShotAccuracy + t.ShotPower) / 2),
                (K.TradeChecking, t.Checking),
                (K.TradeHands, t.PuckControl),
                (K.TradeIq, t.HockeyIq),
            ];
            int labelW = 70;
            int barW = w - labelW - 22;
            foreach ((string key, int value) in rows)
            {
                art.Left(art.Clip(Text.Get(key), labelW - 4), x, top, ScreenArt.Muted);
                art.Bar(value, x + labelW, top - 1, barW, art.Gold);
                top += TraitRow;
            }

            top += 2;
            art.Left(
                Text.Format(K.Overall, TradeDesk.Value(player)),
                x,
                top,
                better ? art.Gold : art.Text
            );
        }
    }
}
