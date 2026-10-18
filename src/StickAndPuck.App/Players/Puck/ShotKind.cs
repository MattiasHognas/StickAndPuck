namespace StickAndPuck.App.Players.Puck
{
    /// <summary>The two kinds of shot.</summary>
    public enum ShotKind
    {
        /// <summary>A quick tap of shoot: a wrist shot, quick and accurate.</summary>
        Wrist,

        /// <summary>Shoot held past <see cref="PuckHandling.SlapAfter"/>: a slap shot, harder the longer the wind-up.</summary>
        Slap,
    }
}
