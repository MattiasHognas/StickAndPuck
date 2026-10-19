using System.Text.Json;
using StickAndPuck.App.Core;
using StickAndPuck.App.Replay;
using StickAndPuck.App.Screens;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Network
{
    /// <summary>The match two machines agree on before lockstep starts. The host picks the clubs, its rules,
    /// period length, settings and a fresh seed and sends them as a replay header; the joiner builds the
    /// same world from it. Both players' input arrives as the two network pads (<see cref="NetPad"/>), so
    /// the setup's slots are always the first gamepad for home and the second for the visitors, and
    /// the shared settings keep the default key bindings.</summary>
    internal static class NetMatch
    {
        /// <summary>The settings each machine keeps its own of: language, volumes, graphics card and the fps line only change how the match looks and sounds.</summary>
        public static Settings Local(Settings shared, Settings mine)
        {
            return shared with
            {
                Language = mine.Language,
                OverallVolume = mine.OverallVolume,
                EffectsVolume = mine.EffectsVolume,
                MusicVolume = mine.MusicVolume,
                Gpu = mine.Gpu,
                FpsLine = mine.FpsLine,
            };
        }

        /// <summary>Gets the human slots of a network match: home on the first network pad, the visitors on the second; a side without a human (SAP_HUMAN=0 there) is played by the AI.</summary>
        public static IReadOnlyList<InputSlot> Inputs(bool hostHuman, bool joinerHuman)
        {
            return
            [
                new InputSlot(hostHuman ? InputDevice.Gamepad1 : InputDevice.None, Side.Home),
                new InputSlot(joinerHuman ? InputDevice.Gamepad2 : InputDevice.None, Side.Away),
            ];
        }

        /// <summary>Gets the plan of a network match between two clubs.</summary>
        public static MatchPlan Plan(ClubData home, ClubData away)
        {
            return new MatchPlan(Maybe.Some<IGameMode>(new NetworkMode()), [home, away]);
        }

        /// <summary>The host's side: the match with its clubs, rules, period length and settings, and a new seed.</summary>
        public static ReplayHeader Host(
            AppState app,
            ClubData home,
            ClubData away,
            bool joinerHuman
        )
        {
            MatchSetup setup = app.Screens.Prepare(
                new MatchSetup
                {
                    Home = home,
                    Away = away,
                    Inputs = Inputs(!Env.Is("SAP_HUMAN", "0"), joinerHuman),
                    Overtime = OvertimeRule.Shl,
                },
                Plan(home, away)
            );
            Settings shared = app.Screens.Settings with
            {
                Keys = new Dictionary<GameAction, KeyBinding>(),
            };
            return new ReplayHeader
            {
                Seed = Seeds.NewMatch(),
                Kind = "match",
                Setup = ReplaySetup.Of(setup),
                Mode = nameof(NetworkMode),
                Prefs = ScreenPrefsFile.ToJson(app.Screens.Prefs with { Settings = shared }),
                Clubs = ReplayRecorder.ClubsHash(home, away),
                Recorded = "network",
            };
        }

        /// <summary>Writes a header as the welcome message carries it.</summary>
        public static string Write(ReplayHeader header)
        {
            return JsonSerializer.Serialize(header, ReplayJsonContext.Default.ReplayHeader);
        }

        /// <summary>Reads the header of a welcome message, or None with the reason (a string key) when this machine cannot play it.</summary>
        public static Maybe<ReplayHeader> Read(
            string json,
            IReadOnlyList<ClubData> clubs,
            out string problem
        )
        {
            problem = "";
            ReplayHeader header;
            try
            {
                header =
                    JsonSerializer.Deserialize(json, ReplayJsonContext.Default.ReplayHeader)
                    ?? throw new JsonException("no header");
            }
            catch (JsonException e)
            {
                Console.WriteLine($"SAP: network: the host's match does not read ({e.Message})");
                problem = K.NetVersionDiffers;
                return Maybe.None<ReplayHeader>();
            }

            if (
                !header.Setup.TryGet(out ReplaySetup stored)
                || !clubs.Any(c => string.Equals(c.Id, stored.Home, StringComparison.Ordinal))
                || !clubs.Any(c => string.Equals(c.Id, stored.Away, StringComparison.Ordinal))
            )
            {
                problem = K.NetClubsDiffer;
                return Maybe.None<ReplayHeader>();
            }

            MatchSetup setup = stored.ToSetup(clubs);
            if (
                !string.Equals(
                    ReplayRecorder.ClubsHash(setup.Home, setup.Away),
                    header.Clubs,
                    StringComparison.Ordinal
                )
            )
            {
                problem = K.NetClubsDiffer;
                return Maybe.None<ReplayHeader>();
            }

            return header;
        }

        /// <summary>Starts the match of a header on this machine's screens and returns its world.</summary>
        public static World Start(AppState app, ReplayHeader header)
        {
            ReplaySetup stored = header.Setup.TryGet(out ReplaySetup s)
                ? s
                : throw new InvalidDataException("a network match without its setup");
            MatchSetup setup = stored.ToSetup(app.Screens.Clubs);
            ScreenPrefs prefs = ScreenPrefsFile.FromJson(header.Prefs);
            Settings settings = Local(prefs.Settings, app.Screens.Settings);
            return app.Screens.StartShared(
                setup,
                Plan(setup.Home, setup.Away),
                header.Seed,
                settings,
                prefs with
                {
                    Settings = settings,
                }
            );
        }
    }
}
