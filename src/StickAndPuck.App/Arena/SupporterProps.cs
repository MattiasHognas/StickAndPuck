using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Arena
{
    /// <summary>W120: who in the stands holds up a scarf, a flag or a banner, and what it does in his section's
    /// <see cref="CrowdPose"/>. Nothing goes up while the section sits or boos; standing fans hold their scarves
    /// and banners up taut and their flags flutter, cheering fans shake them on the hop, and a waving section
    /// swings them side to side. Pure maths from the seat index and the time, so every run looks the same and
    /// the simulation never sees it; <see cref="SupporterRenderer"/> draws it.</summary>
    internal static class SupporterProps
    {
        /// <summary>How many home fans hold up a scarf, a flag and (front rows only) a banner.</summary>
        public const float ScarfShare = 0.09f,
            FlagShare = 0.04f,
            BannerShare = 0.06f;

        /// <summary>The travelling fans bring more: their shares of scarves and flags.</summary>
        public const float AwayScarfShare = 0.18f,
            AwayFlagShare = 0.07f;

        /// <summary>Banners are held in the section's front rows: this many rows from the front.</summary>
        public const int BannerRows = 2;

        /// <summary>How high above the seat each thing is held (its middle), in metres: the crowd's standing
        /// frames are two metres tall with the arms up.</summary>
        public const float ScarfHeight = 2.05f,
            FlagHeight = 1.85f,
            BannerHeight = 1.75f;

        /// <summary>How far a waving section swings its scarves and banners to each side, in metres.</summary>
        public const float SwayReach = 0.3f;

        /// <summary>How high a cheering fan hops his scarf or flag on the beat, in metres.</summary>
        public const float Hop = 0.15f;

        /// <summary>One fan's scarf, flag or banner.</summary>
        /// <param name="Foot">His seat.</param>
        /// <param name="Section">His section.</param>
        /// <param name="Kind">What he holds.</param>
        /// <param name="Phase">His own offset, 0 to 1, so a section does not move as one.</param>
        [StructLayout(LayoutKind.Auto)]
        internal readonly record struct Prop(
            Vector3 Foot,
            CrowdSection Section,
            PropKind Kind,
            float Phase
        );

        /// <summary>How a held prop shows this frame.</summary>
        /// <param name="Frame">The frame in its piece of the sheet.</param>
        /// <param name="Sway">Sideways, in metres (the viewer's right positive).</param>
        /// <param name="Rise">Up from where it is held, in metres.</param>
        [StructLayout(LayoutKind.Auto)]
        internal readonly record struct Look(int Frame, float Sway, float Rise);

        /// <summary>Picks the fans holding something up, from the seats file: by a hash of the seat, so the
        /// same fans every match; the visitors' corner holds more.</summary>
        /// <param name="json">art/scene/crowd_seats.json.</param>
        /// <param name="fans">The club each section supports.</param>
        /// <returns>The props, back rows first like the seats.</returns>
        internal static Prop[] Place(string json, Func<CrowdSection, Side> fans)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            List<(Vector3 Foot, int Row, CrowdSection Section)> seats =
            [
                .. doc
                    .RootElement.GetProperty("seats")
                    .EnumerateArray()
                    .Select(s =>
                        (
                            new Vector3(s[0].GetSingle(), s[1].GetSingle(), s[2].GetSingle()),
                            s[3].GetInt32(),
                            (CrowdSection)s[4].GetInt32()
                        )
                    ),
            ];
            Dictionary<CrowdSection, int> front = seats
                .GroupBy(s => s.Section)
                .ToDictionary(g => g.Key, g => g.Min(s => s.Row));
            List<Prop> props = [];
            for (int i = 0; i < seats.Count; i++)
            {
                (Vector3 foot, int row, CrowdSection section) = seats[i];
                bool away = fans(section) == Side.Away;
                float u = CrowdFrames.Unit(i, 11);
                bool banners = !away && row - front[section] < BannerRows; // the front rows' banners
                float bannerShare = banners ? BannerShare : 0f;
                float flagShare = away ? AwayFlagShare : FlagShare;
                float scarfShare = away ? AwayScarfShare : ScarfShare;
                Maybe<PropKind> kind =
                    u < bannerShare ? PropKind.Banner
                    : u < bannerShare + flagShare ? PropKind.Flag
                    : u < bannerShare + flagShare + scarfShare ? PropKind.Scarf
                    : Maybe.None<PropKind>();
                if (kind.TryGet(out PropKind k))
                {
                    props.Add(new Prop(foot, section, k, CrowdFrames.Unit(i, 12)));
                }
            }

            return [.. props];
        }

        /// <summary>How a prop shows in its section's pose: nothing while the section sits or boos.</summary>
        /// <param name="kind">What is held.</param>
        /// <param name="pose">The section's pose.</param>
        /// <param name="time">Seconds since the pose began.</param>
        /// <param name="phase">The fan's own offset, 0 to 1.</param>
        /// <returns>The frame, sway and rise, or none when it is down.</returns>
        internal static Maybe<Look> For(PropKind kind, CrowdPose pose, float time, float phase)
        {
            return pose switch
            {
                // on their feet: scarves and banners held up taut, flags fluttering slowly
                CrowdPose.Stand => kind switch
                {
                    PropKind.Flag => new Look(Cycle(time, 2f, phase, 3), 0f, 0f),
                    PropKind.Scarf or PropKind.Banner => new Look(0, 0f, 0f),
                    _ => Maybe.None<Look>(),
                },
                // cheering: everything shaken on the hop
                CrowdPose.Cheer => new Look(
                    kind switch
                    {
                        PropKind.Scarf => 1 + Cycle(time, 3f, phase, 2),
                        PropKind.Flag => Cycle(time, 6f, phase, 3),
                        PropKind.Banner or _ => Cycle(time, 3f, phase, 2),
                    },
                    0f,
                    Cycle(time, 3f, phase, 2) == 1 ? Hop : 0f
                ),
                // waving: swung from side to side with the section
                CrowdPose.Wave => new Look(
                    kind switch
                    {
                        PropKind.Scarf => 1 + Cycle(time, 1.6f, phase, 2),
                        PropKind.Flag => Cycle(time, 4f, phase, 3),
                        PropKind.Banner or _ => 0,
                    },
                    SwayReach * MathF.Sin(MathF.Tau * ((time * 0.8f) + (phase * 0.25f))),
                    0f
                ),
                CrowdPose.Sit or CrowdPose.Boo or _ => Maybe.None<Look>(),
            };
        }

        /// <summary>Gets how high a prop is held above its seat.</summary>
        /// <param name="kind">What is held.</param>
        /// <returns>Metres to its middle.</returns>
        internal static float Height(PropKind kind)
        {
            return kind switch
            {
                PropKind.Scarf => ScarfHeight,
                PropKind.Flag => FlagHeight,
                PropKind.Banner or _ => BannerHeight,
            };
        }

        /// <summary>Gets the supporter sheet piece a prop is drawn from.</summary>
        /// <param name="kind">What is held.</param>
        /// <returns><see cref="SupporterArt.Scarf"/>, <see cref="SupporterArt.Flag"/> or <see cref="SupporterArt.Banner"/>.</returns>
        internal static string Piece(PropKind kind)
        {
            return kind switch
            {
                PropKind.Scarf => SupporterArt.Scarf,
                PropKind.Flag => SupporterArt.Flag,
                PropKind.Banner or _ => SupporterArt.Banner,
            };
        }

        /// <summary>Steps through <paramref name="frames"/> frames <paramref name="hz"/> times a second, from the fan's own offset.</summary>
        private static int Cycle(float time, float hz, float phase, int frames)
        {
            int step = (int)MathF.Floor((time * hz) + (phase * frames));
            return ((step % frames) + frames) % frames;
        }
    }
}
