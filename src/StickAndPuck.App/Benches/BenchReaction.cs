namespace StickAndPuck.App.Benches
{
    /// <summary>What a bench figure is doing over his base pose for a while, from what happens on the ice.</summary>
    public enum BenchReaction
    {
        /// <summary>Nothing: his base pose (watching, tired, up for a change).</summary>
        None,

        /// <summary>A conversation's other half: seated, turned to whoever talks to him (a neighbour, the trainer, the coach).</summary>
        Listen,

        /// <summary>Seated, turned to a neighbour, one hand talking.</summary>
        Chat,

        /// <summary>Seated, the trainer leaning in to him.</summary>
        Trainer,

        /// <summary>Seated, the coach leaning over him and pointing at the ice.</summary>
        Instruct,

        /// <summary>Seated: left hand up, the helmet comes off, then the bare head until he next goes on.</summary>
        HelmetOff,

        /// <summary>Seated, hand to the helmet: the puck rang off a post.</summary>
        Ooh,

        /// <summary>Up at the boards, pointing out where a line call was made.</summary>
        Point,

        /// <summary>Up at the boards, a short fist pump: the other club took a penalty.</summary>
        FistPump,

        /// <summary>Up, hammering the stick on the board top: a big hit or a big save.</summary>
        Bang,

        /// <summary>Up at the boards, arms flung wide at the referee: a penalty against.</summary>
        Argue,

        /// <summary>Seated, head hung: a goal against.</summary>
        Slump,

        /// <summary>Up, both arms in the air: a goal for.</summary>
        Cheer,

        /// <summary>Up, leaning on the boards to watch a fight until it ends.</summary>
        StandFight,
    }
}
