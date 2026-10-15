using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Camera.Dev;

/// <summary>SAP_SCENE=camera-depth: an empty rink with the puck sliding from the far boards to the near
/// boards and back, so the camera's slide down the frame and back up can be watched or recorded.
/// SAP_PUCK_Z=metres instead starts the puck at that depth at centre ice (negative toward the far
/// boards), at rest for stills of each framing, or sliding at SAP_PUCK_VZ m/s along z.</summary>
public sealed class DepthScene : IScene
{
    /// <inheritdoc/>
    public string Name => "camera-depth";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
    {
        var world = new World(Scenes.DefaultSetup(clubs), settings);
        if (float.TryParse(Env.Get("SAP_PUCK_Z"), NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
        {
            world.Puck.Position = new Vector3(0, 0, z);
            world.Puck.Velocity = float.TryParse(Env.Get("SAP_PUCK_VZ"), NumberStyles.Float, CultureInfo.InvariantCulture, out float vz)
                ? new Vector3(1.2f, 0, vz) : Vector3.Zero;
            return world;
        }

        world.Puck.Position = new Vector3(-4f, 0, -11.5f);
        world.Puck.Velocity = new Vector3(1.2f, 0, 9.5f);
        return world;
    }
}
