using Raylib_cs;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Arena
{
    /// <summary>A17: which of arena.glb's swappable layers a match shows, picked by its league. An SHL match
    /// keeps the arena as it always was: the 'SHL' centre mark (the Logo part) and the dusk line colours.
    /// An NHL match drops the mark for the home club's crest at centre ice (<see cref="CentreIce"/>),
    /// paints the lines in the NHL's deeper red and blue, and hangs banners in the rafters. The
    /// trapezoid behind the goal lines is its own layer for either league, shown when the trapezoid rule
    /// (<see cref="RuleSet.Trapezoid"/>, W72) is on; SAP_TRAPEZOID=1 or 0 overrides the rule for previews. Render only: nothing here touches the simulation.</summary>
    /// <param name="ShlLogo">Whether the 'SHL' centre-ice mark shows.</param>
    /// <param name="HomeCrest">Whether the home club's crest shows at centre ice.</param>
    /// <param name="Banners">Whether the rafter banners hang.</param>
    /// <param name="NhlLines">Whether the red and blue lines take the NHL colours.</param>
    /// <param name="Trapezoid">Whether the trapezoid shows behind each goal line.</param>
    internal readonly record struct ArenaLayers(
        bool ShlLogo,
        bool HomeCrest,
        bool Banners,
        bool NhlLines,
        bool Trapezoid
    )
    {
        /// <summary>The NHL's red line, a deeper, truer red than the dusk SHL coral (#c8574a): palette crimson #b8323c.</summary>
        public static readonly Color NhlRed = new(0xb8, 0x32, 0x3c, 255);

        /// <summary>The NHL's blue line, deeper than the dusk SHL slate (#3e5875): palette navy #2b3d6b.</summary>
        public static readonly Color NhlBlue = new(0x2b, 0x3d, 0x6b, 255);

        /// <summary>Gets the layers for a league.</summary>
        /// <param name="league">The match's league.</param>
        /// <param name="trapezoid">Whether the trapezoid rule is on.</param>
        public static ArenaLayers For(League league, bool trapezoid)
        {
            bool nhl = league == League.Nhl;
            return new(
                ShlLogo: !nhl,
                HomeCrest: nhl,
                Banners: nhl,
                NhlLines: nhl,
                Trapezoid: trapezoid
            );
        }

        /// <summary>Gets SAP_TRAPEZOID's override of the trapezoid rule for the markings: 1 shows them, 0 hides them, else None.</summary>
        public static Maybe<bool> TrapezoidFromEnvironment()
        {
            bool on = Env.Is("SAP_TRAPEZOID", "1");
            return on || Env.Is("SAP_TRAPEZOID", "0") ? Maybe.Some(on) : Maybe.None<bool>();
        }

        /// <summary>Gets a value indicating whether the trapezoid shows: with its rule (<see cref="RuleSet.Trapezoid"/>),
        /// unless <paramref name="environment"/> overrides it.</summary>
        /// <param name="rules">The match's rules.</param>
        /// <param name="environment">The override from <see cref="TrapezoidFromEnvironment"/>.</param>
        public static bool TrapezoidShown(RuleSet rules, Maybe<bool> environment)
        {
            return environment.Or(rules.Trapezoid);
        }

        /// <summary>Gets a value indicating whether an arena part shows with these layers.</summary>
        /// <param name="part">The part's name in arena_meshes.json.</param>
        public bool Shows(string part)
        {
            return part switch
            {
                "Logo" => ShlLogo,
                "Trapezoid" => Trapezoid,
                "Banners" => Banners,
                _ => true,
            };
        }
    }
}
