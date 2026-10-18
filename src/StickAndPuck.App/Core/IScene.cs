namespace StickAndPuck.App.Core
{
    /// <summary>A starting setup the game can boot straight into with SAP_SCENE=name, for example a faceoff
    /// or a shot on goal. Any class implementing this with a parameterless constructor is found
    /// automatically, so a task adds a scene in its own folder without touching Core.</summary>
    public interface IScene
    {
        /// <summary>Gets the name used in SAP_SCENE.</summary>
        string Name { get; }

        /// <summary>Builds the world: the match setup, the players and the puck. The world runs with <paramref name="app"/>, the game's screens and sessions.</summary>
        World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app);
    }
}
