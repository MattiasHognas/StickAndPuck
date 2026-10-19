using StickAndPuck.App.Core;
using StickAndPuck.App.Season.DressingRoom;
using StickAndPuck.App.Season.Schedule;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>The coach's pre-game talk (W125), over the dressing room after PLAY NEXT GAME: the portrait,
    /// what the coach says (<see cref="PreGameTalkSources"/>: tonight's opponent, later a scouting tip),
    /// then the three tones to pick, with what the selected one does under them and the coach's face
    /// following the selection (<see cref="Mood"/>). Picking one plays the game
    /// with it on the setup (<see cref="ScreenFlow.PlaySeasonGameAfterTalk"/>); back returns to the room.</summary>
    internal sealed class CoachTalkScreen(AppState app) : MenuScreen(app)
    {
        private const int PanelW = 340,
            Line = 11,
            PortraitW = 64,
            PortraitH = 80,
            TalkLines = 6;

        /// <summary>The tones in menu order.</summary>
        public static IReadOnlyList<TalkTone> Tones { get; } =
        [TalkTone.Calm, TalkTone.FireUp, TalkTone.Discipline];

        /// <inheritdoc/>
        public override IReadOnlyList<MenuItem> Items()
        {
            return
            [
                .. Tones.Select(t => new MenuItem(
                    Text.Get(Label(t)),
                    () => Flow.PlaySeasonGameAfterTalk(t)
                )),
            ];
        }

        /// <summary>Gets the string key of a tone's menu label.</summary>
        public static string Label(TalkTone tone)
        {
            return tone switch
            {
                TalkTone.Calm => K.TalkCalm,
                TalkTone.FireUp => K.TalkFireUp,
                TalkTone.Discipline => K.TalkDiscipline,
                _ => throw new NotSupportedException("no label for " + tone),
            };
        }

        /// <summary>Gets the coach's face while a tone is selected (A20's moods, local to this panel): level
        /// for keeping calm, a grin for firing them up, shouting for discipline.</summary>
        public static CoachMood Mood(TalkTone tone)
        {
            return tone switch
            {
                TalkTone.Calm => CoachMood.Neutral,
                TalkTone.FireUp => CoachMood.Pleased,
                TalkTone.Discipline => CoachMood.Angry,
                _ => CoachMood.Neutral,
            };
        }

        /// <summary>Gets the string key of what a tone does in the match.</summary>
        public static string Hint(TalkTone tone)
        {
            return tone switch
            {
                TalkTone.Calm => K.TalkCalmHint,
                TalkTone.FireUp => K.TalkFireUpHint,
                TalkTone.Discipline => K.TalkDisciplineHint,
                _ => throw new NotSupportedException("no hint for " + tone),
            };
        }

        /// <inheritdoc/>
        public override void Draw(ScreenArt art, World world)
        {
            if (!App.Season.Current.TryGet(out SeasonState season))
            {
                return;
            }

            _ = art.DrawDressingRoom();
            DressingRoomContext context = DressingRoomContext.Of(
                season,
                Flow.Clubs,
                DressingRoomScreen.Rules(App)
            );
            IReadOnlyList<MenuItem> items = Items();
            int h =
                5
                + ScreenArt.Row
                + 2
                + PortraitH
                + 8
                + (items.Count * ScreenArt.Row)
                + 4
                + Line
                + 6;
            (int x, int top) = art.Panel(
                Text.Get(K.PreGameTalk),
                PanelW,
                h,
                Math.Max(2, (Game.Height - h - 14) / 2)
            );

            string coach = Text.Get(K.RoomCoach);
            TalkTone selected = Tones[Math.Clamp(Selected, 0, Tones.Count - 1)];
            art.DrawCoach(x + 10, top, PortraitW, PortraitH, coach, Mood(selected));
            int tx = x + 10 + PortraitW + 10;
            int tw = PanelW - (tx - x) - 10;
            art.Left(coach, tx, top, art.Gold);
            int y = top + Line + 2;
            int room = TalkLines;
            foreach (DressingRoomEntry said in PreGameTalkSources.Build(context).All)
            {
                foreach (string line in DressingRoomScreen.Wrap(art, said.Text, tw).Take(room))
                {
                    art.Left(line, tx, y, said.Highlight ? art.Gold : art.Text);
                    y += Line;
                    room--;
                }

                y += 3; // a little air between the coach's points
            }

            top += PortraitH + 8;
            art.Menu(items, Selected, x + 70, PanelW - 140, top);
            top += (items.Count * ScreenArt.Row) + 4;
            art.Centred(
                art.Clip(Text.Get(Hint(selected)), PanelW - 20),
                Game.Width / 2,
                top,
                ScreenArt.Muted
            );
        }
    }
}
