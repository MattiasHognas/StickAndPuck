namespace StickAndPuck.App.Physics
{
    /// <summary>The numbers that decide how the puck feels. Metres, seconds, metres per second.
    /// Restitution is the share of the speed into a surface that comes back out of it.</summary>
    public static class PuckTuning
    {
        /// <summary>Gravity on a puck in the air.</summary>
        public const float Gravity = 9.81f;

        /// <summary>Steady slowing of a puck sliding on the ice (kinetic friction, about μ = 0.08).</summary>
        public const float IceFriction = 0.8f;

        /// <summary>Extra slowing in proportion to speed, sliding or flying (air and snow on the ice).</summary>
        public const float Drag = 0.06f;

        /// <summary>Restitution when a falling puck lands on the ice.</summary>
        public const float IceBounce = 0.3f;

        /// <summary>A landing slower than this sticks to the ice instead of bouncing.</summary>
        public const float LandSpeed = 0.6f;

        /// <summary>Restitution off the boards.</summary>
        public const float BoardBounce = 0.55f;

        /// <summary>Restitution off the glass above the boards: livelier than the boards. Above the glass a puck leaves the rink (<see cref="Core.RuleSet.PuckOverGlass"/>), or netting catches it.</summary>
        public const float GlassBounce = 0.7f;

        /// <summary>Rubbing against the boards or glass: speed lost along the wall per unit of impact (a friction coefficient).</summary>
        public const float WallGrip = 0.15f;

        /// <summary>Radius of a goal post and of the crossbar (a 60 mm pipe).</summary>
        public const float PostRadius = 0.03f;

        /// <summary>Restitution off a post or the crossbar: steel rings.</summary>
        public const float PostBounce = 0.75f;

        /// <summary>Restitution off the net mesh from inside: the mesh swallows the puck.</summary>
        public const float MeshBounce = 0.05f;

        /// <summary>Slowing per second of a puck inside the net, dragging through the mesh and the bag.</summary>
        public const float NetDrag = 5f;

        /// <summary>Restitution off the back or sides of the net from outside (frame and mesh).</summary>
        public const float NetOutsideBounce = 0.3f;

        /// <summary>Radius of a skater's body for puck contact.</summary>
        public const float BodyRadius = 0.32f;

        /// <summary>Height of a skater's body for puck contact; a higher puck flies over.</summary>
        public const float BodyHeight = 1.8f;

        /// <summary>Radius of a skater's footprint against the goal cage (body, skates and elbows); matches the skating body.</summary>
        public const float SkaterNetRadius = 0.45f;

        /// <summary>Radius of a goalie's footprint against the goal cage.</summary>
        public const float GoalieNetRadius = 0.2f;

        /// <summary>Speed along the frame that steers a player pushing into the cage around it.</summary>
        public const float AroundSpeed = 2.5f;

        /// <summary>A skater whose stick points into the cage at least this hard (0 to 1) wants past it and is steered around.</summary>
        public const float PushingIntent = 0.5f;

        /// <summary>A stick pointing along the frame at least this much (0 to 1) picks the way round; less and the nearer edge does, so a player aiming at a spot just past the far side still gets there.</summary>
        public const float SlideIntent = 0.5f;

        /// <summary>A goalie skating slower than this is standing its ground, not trying to get past the cage.</summary>
        public const float GoalieMoving = 0.3f;

        /// <summary>Restitution off a body: pads and legs soak up most of a hit.</summary>
        public const float BodyBounce = 0.2f;

        /// <summary>Seconds after a release during which the puck ignores the player who released it.</summary>
        public const float ReleaseGrace = 0.2f;

        /// <summary>Impacts slower than this are rubbing, not hits, and publish no event.</summary>
        public const float EventSpeed = 1f;

        /// <summary>Longest move in one sub-step, so a fast shot cannot pass through a post.</summary>
        public const float MaxSubstepMove = 0.025f;

        /// <summary>A puck moved further than this between two steps by something other than physics was placed, not played, and is trusted where it lands.</summary>
        public const float TeleportDistance = 1.5f;
    }
}
