using System.Globalization;
using System.Text.Json;
using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Replay;
using StickAndPuck.App.Season.Injuries;
using StickAndPuck.App.Season.Stats;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season.Injuries
{
    /// <summary>
    /// The dressed roster of 22 (W86): every club's two spares, a forward and a seventh defenceman, sit while
    /// everyone is fit (the lines and the replay fingerprint are the 20 they always were), step into the slot
    /// of an injured or suspended skater of their group in both leagues, and once they are used the players
    /// behind move up as before. Replays recorded before the spares still build the same clubs.
    /// </summary>
    public sealed class SpareTests : CheckedTest
    {
        /// <summary>The replay club fingerprints the game had before W86, from the 20-man club files.</summary>
        private static readonly (string Home, string Away, string Hash)[] OldFingerprints =
        [
            ("brynas", "frolunda", "4c441203c8c61d98"),
            ("bjorkloven", "vaxjo", "5c7ca6fc9bf8caa9"),
            ("oilers", "maple_leafs", "e33b8ddb8274c2ba"),
            ("avalanche", "wild", "abe872b61d05d154"),
        ];

        private static IEnumerable<ClubData> Every()
        {
            return Clubs.Load(League.Shl).Concat(Clubs.Load(League.Nhl));
        }

        private static ClubData Club(string id)
        {
            return Every().First(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        }

        [Test]
        public void EveryClubDressesTwentyTwo()
        {
            int clubs = 0;
            foreach (ClubData club in Every())
            {
                clubs++;
                List<PlayerData> all = [.. club.Everyone()];
                Check(
                    club.Roster.Count == 20 && club.Spares.Count == 2,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{club.Id} has 20 in its lines and 2 spares ({club.Roster.Count} + {club.Spares.Count})"
                    )
                );
                Check(
                    club.Spares.Where(p => p.Position == PlayingPosition.D).Take(2).Count() == 1
                        && club.Spares.Where(p =>
                                p.Position
                                    is PlayingPosition.C
                                        or PlayingPosition.LW
                                        or PlayingPosition.RW
                            )
                            .Take(2)
                            .Count() == 1,
                    $"{club.Id}'s spares are a forward and a seventh defenceman"
                );
                Check(
                    all.Select(p => p.Number).ToHashSet().Count == all.Count
                        && !club.Spares.Any(s =>
                            club.Roster.Any(p =>
                                string.Equals(p.FirstName, s.FirstName, StringComparison.Ordinal)
                                && string.Equals(p.LastName, s.LastName, StringComparison.Ordinal)
                            )
                        ),
                    $"{club.Id}'s 22 have their own numbers, and the spares are not in the 20"
                );
                foreach (PlayerData spare in club.Spares)
                {
                    CheckSpare(club, spare);
                }
            }

            Check(
                clubs == 46,
                string.Create(CultureInfo.InvariantCulture, $"all 46 clubs are checked ({clubs})")
            );
        }

        /// <summary>Checks one spare: a real player with an age, his traits on his teammates' scale.</summary>
        private void CheckSpare(ClubData club, PlayerData spare)
        {
            Traits t = spare.Traits;
            int[] skating =
            [
                t.Acceleration,
                t.TopSpeed,
                t.ShotAccuracy,
                t.ShotPower,
                t.Checking,
                t.PuckControl,
                t.HockeyIq,
            ];
            List<PlayerData> mates =
            [
                .. club.Roster.Where(p =>
                    p.Position == PlayingPosition.D == (spare.Position == PlayingPosition.D)
                    && p.Position != PlayingPosition.G
                ),
            ];
            double mean = skating.Average();
            double low = mates.Min(Mean);
            double high = mates.Max(Mean);
            Check(
                spare.FirstName.Length > 1
                    && spare.LastName.Length > 1
                    && spare.Number is > 0 and < 100
                    && spare.Age is >= 16 and <= 45,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{club.Id} spare #{spare.Number} {spare.FirstName} {spare.LastName} is a real player with an age ({spare.Age})"
                )
            );
            Check(
                skating.All(v => v is >= 25 and <= 95)
                    && mean >= low - 10
                    && mean <= high
                    && t.GoalieReflexes == 10,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{club.Id} spare #{spare.Number}'s traits are on his teammates' scale ({mean:0} against {low:0}..{high:0})"
                )
            );
        }

        [Test]
        public void SparesSitWhileEveryoneIsFit()
        {
            foreach (ClubData club in Every())
            {
                Check(
                    ReferenceEquals(AbsenceTable.Dressed(club, []), club)
                        && ReferenceEquals(new AbsenceTable().Without(club), club),
                    $"{club.Id} dresses its 20 when everyone is fit"
                );
            }

            ClubData home = Club("brynas");
            World w = Lined(home);
            Check(
                !w.Players.Exists(p =>
                    p.Side == Side.Home && home.Spares.Any(s => ReferenceEquals(s, p.Data))
                )
                    && w.Players.Where(p => p.Side == Side.Home).Take(21).Count() == 20,
                "no spare is in the match"
            );
            Squad squad = SquadOf(w);
            Check(
                squad.Lines.Select(l => l.Members.Count).SequenceEqual([3, 3, 3, 3])
                    && squad.Pairs.Select(p => p.Members.Count).SequenceEqual([2, 2, 2]),
                "the lines are the four and three they always were"
            );
        }

        [Test]
        [Arguments("brynas")]
        [Arguments("oilers")]
        public void SpareForwardStepsInForTheInjured(string id)
        {
            ClubData club = Club(id);
            PlayerData spare = club.Spares.First(p => p.Position != PlayingPosition.D);
            PlayingPosition other =
                spare.Position == PlayingPosition.LW ? PlayingPosition.RW : PlayingPosition.LW; // a slot of another forward position: he still takes it
            PlayerData hurt = club.Roster.First(p => p.Position == other);
            AbsenceTable table = new();
            table.Add(Out(club.Id, hurt.Number, AbsenceReason.Injury));
            ClubData dressed = table.Without(club);
            int slot = Slot(club, hurt);
            Check(
                dressed.Roster.Count == 20
                    && dressed.Roster[slot].Number == spare.Number
                    && dressed.Roster[slot].Position == other
                    && !dressed.Roster.Any(p => p.Number == hurt.Number)
                    && dressed.Spares.Count == 1,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{id}: spare #{spare.Number} takes #{hurt.Number}'s slot as {other}"
                )
            );
            Squad squad = SquadOf(Lined(dressed));
            Check(
                squad.Lines[0].Members.Any(p => p.Data.Number == spare.Number)
                    && squad.Lines.Select(l => l.Members.Count).SequenceEqual([3, 3, 3, 3]),
                $"{id}: he plays on the first line and nobody moves up"
            );
        }

        [Test]
        [Arguments("frolunda")]
        [Arguments("maple_leafs")]
        public void SeventhDefencemanStepsInForTheSuspended(string id)
        {
            ClubData club = Club(id);
            PlayerData seventh = club.Spares.First(p => p.Position == PlayingPosition.D);
            PlayerData banned = club.Roster.First(p => p.Position == PlayingPosition.D);
            ClubData dressed = AbsenceTable.Dressed(club, [FormTable.Key(club.Id, banned.Number)]);
            Check(
                dressed.Roster[Slot(club, banned)].Number == seventh.Number
                    && dressed.Roster.Count == 20,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{id}: #{seventh.Number} {seventh.LastName} takes #{banned.Number}'s slot"
                )
            );
            Squad squad = SquadOf(Lined(dressed));
            Check(
                squad.Pairs[0].Members.Any(p => p.Data.Number == seventh.Number)
                    && squad.Pairs.Select(p => p.Members.Count).SequenceEqual([2, 2, 2])
                    && squad.Lines.Select(l => l.Members.Count).SequenceEqual([3, 3, 3, 3]),
                $"{id}: he plays on the first pair and the lines stay whole"
            );
        }

        [Test]
        public void PlayersMoveUpOnceTheSparesAreUsed()
        {
            ClubData club = Club("lulea");
            List<PlayerData> wings =
            [
                .. club.Roster.Where(p => p.Position == PlayingPosition.LW).Take(3),
            ];
            List<PlayerData> backs =
            [
                .. club.Roster.Where(p => p.Position == PlayingPosition.D).Take(2),
            ];
            List<string> keys =
            [
                .. wings.Take(2).Concat(backs).Select(p => FormTable.Key(club.Id, p.Number)),
            ];
            ClubData dressed = AbsenceTable.Dressed(club, keys);
            Check(
                dressed.Roster.Count == 18 && dressed.Spares.Count == 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"two forwards and two backs out, two spares in: 18 dress ({dressed.Roster.Count})"
                )
            );
            Squad squad = SquadOf(Lined(dressed));
            PlayerData spareForward = club.Spares.First(p => p.Position != PlayingPosition.D);
            Check(
                squad.Lines[0].Members.Any(p => p.Data.Number == spareForward.Number)
                    && squad.Lines[1].Members.Any(p => p.Data == wings[2]),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the spare forward takes the first line's slot, #{wings[2].Number} moves up to the second"
                )
            );
            Check(
                squad.Lines.Select(l => l.Members.Count).SequenceEqual([3, 3, 3, 2])
                    && squad.Pairs.Select(p => p.Members.Count).SequenceEqual([2, 2, 1]),
                $"the fourth line and the last pair play short ({string.Join(' ', squad.Lines.Select(l => l.Members.Count))} / {string.Join(' ', squad.Pairs.Select(p => p.Members.Count))})"
            );

            // a spare who is out himself does not step in
            ClubData noSpare = AbsenceTable.Dressed(
                club,
                [
                    FormTable.Key(club.Id, wings[0].Number),
                    FormTable.Key(club.Id, spareForward.Number),
                ]
            );
            Check(
                noSpare.Roster.Count == 19
                    && !noSpare.Roster.Any(p => p.Number == spareForward.Number),
                "an injured spare stays out, and the line fills from behind"
            );

            // with everyone hurt the club still dresses its twelve, spares counted
            AbsenceTable many = new();
            foreach (PlayerData p in club.Everyone())
            {
                many.Add(Out(club.Id, p.Number, AbsenceReason.Injury));
            }

            ClubData thin = many.Without(club);
            Check(
                thin.Roster.Where(p => p.Position != PlayingPosition.G).Take(13).Count()
                    == AbsenceTable.MinSkaters
                    && thin.Roster.Where(p => p.Position == PlayingPosition.G).Take(2).Count() == 1,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"with all 22 hurt a club still dresses {AbsenceTable.MinSkaters} skaters and a goalie ({thin.Roster.Count})"
                )
            );
        }

        [Test]
        public void OldReplaysKeepTheirClubs()
        {
            foreach ((string home, string away, string hash) in OldFingerprints)
            {
                Check(
                    string.Equals(
                        ReplayRecorder.ClubsHash(Club(home), Club(away)),
                        hash,
                        StringComparison.Ordinal
                    ),
                    $"{home} against {away} keeps the fingerprint the 20 had before the spares"
                );
            }

            ClubData brynas = Club("brynas");
            PlayerData hurt = brynas.Roster.First(p => p.Position == PlayingPosition.LW);
            Check(
                string.Equals(
                    ReplayRecorder.ClubsHash(
                        AbsenceTable.Without(brynas, [FormTable.Key("brynas", hurt.Number)]),
                        Club("frolunda")
                    ),
                    "14f539b6ba4d0b1c",
                    StringComparison.Ordinal
                ),
                "a club without an injured player keeps its old fingerprint too"
            );
            Check(
                !Clubs
                    .ToJson(brynas with { Spares = [] })
                    .Contains("\"spares\"", StringComparison.Ordinal)
                    && Clubs.ToJson(brynas).Contains("\"spares\"", StringComparison.Ordinal),
                "a club writes its spares, and a club without any writes none, as before"
            );

            ReplaySetup stored = ReplaySetup.Of(
                WorldBuilder.DefaultSetup with
                {
                    Absent = [FormTable.Key(brynas.Id, hurt.Number)],
                }
            ) with
            {
                Home = brynas.Id,
                Away = "frolunda",
            };
            string json = JsonSerializer.Serialize(stored, ReplayJsonContext.Default.ReplaySetup);
            IReadOnlyList<ClubData> shl = Clubs.Load(League.Shl);
            MatchSetup now = Read(json).ToSetup(shl);
            Check(
                stored.DressSpares
                    && now.DressSpares
                    && now.Home.Roster.Count == 20
                    && now.Home.Roster.Any(p => brynas.Spares.Any(s => s.Number == p.Number)),
                "a new replay dresses the spare again"
            );
            MatchSetup old = Read(
                    json.Replace(
                        "\"dressSpares\":true",
                        "\"unused\":true",
                        StringComparison.Ordinal
                    )
                )
                .ToSetup(shl);
            Check(
                !old.DressSpares
                    && old.Home.Roster.Count == 19
                    && !old.Home.Roster.Any(p => brynas.Spares.Any(s => s.Number == p.Number))
                    && string.Equals(
                        ReplayRecorder.ClubsHash(old.Home, old.Away),
                        "14f539b6ba4d0b1c",
                        StringComparison.Ordinal
                    ),
                "a replay from before the spares plays its 19 and keeps its fingerprint"
            );
        }

        private static ReplaySetup Read(string json)
        {
            return JsonSerializer.Deserialize(json, ReplayJsonContext.Default.ReplaySetup)
                ?? throw new InvalidOperationException("replay setup did not read");
        }

        private static double Mean(PlayerData p)
        {
            Traits t = p.Traits;
            return new[]
            {
                t.Acceleration,
                t.TopSpeed,
                t.ShotAccuracy,
                t.ShotPower,
                t.Checking,
                t.PuckControl,
                t.HockeyIq,
            }.Average();
        }

        private static int Slot(ClubData club, PlayerData p)
        {
            for (int i = 0; i < club.Roster.Count; i++)
            {
                if (ReferenceEquals(club.Roster[i], p))
                {
                    return i;
                }
            }

            return -1;
        }

        private static Absence Out(string club, int number, AbsenceReason reason)
        {
            return new Absence
            {
                Club = club,
                Number = number,
                Reason = reason,
                Cause = reason == AbsenceReason.Injury ? InjuryCause.Check : InjuryCause.None,
                Games = 2,
                GamesLeft = 2,
            };
        }

        private static World Lined(ClubData home)
        {
            return new WorldBuilder()
                .Setup(s => s with { Home = home })
                .Then(x => new LinesSystem().Start(x))
                .Build();
        }

        private static Squad SquadOf(World w)
        {
            return Bench.SquadOf(w, Side.Home).TryGet(out Squad s)
                ? s
                : throw new InvalidOperationException("no squad");
        }
    }
}
