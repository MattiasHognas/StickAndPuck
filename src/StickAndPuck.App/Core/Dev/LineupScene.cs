namespace StickAndPuck.App.Core.Dev
{
    /// <summary>Both clubs' starters at the opening faceoff, the human on the home centre. Puck on the centre dot.</summary>
    public sealed class LineupScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "lineup";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = new(Scenes.DefaultSetup(clubs), settings);
            Scenes.AddStarters(world);
            if (world.OnIce(Side.Home).FirstOrDefault(p => !p.IsGoalie) is { } centre)
            {
                centre.Slot = 0;
            }

            world.Puck.Position = RinkSpec.CentreDot;
            return world;
        }
    }
}
