using System.Numerics;
using Raylib_cs;

namespace StickAndPuck.App.Camera
{
    /// <summary>The title intro's camera (<see cref="Screens.TitleIntro"/>): it starts up in the rafters over the
    /// near stands, close on the far crowd, then tilts down and pulls back until it is the broadcast camera
    /// the start menu sits over. Render only.</summary>
    internal static class TitleCamera
    {
        /// <summary>Where the move starts: high over centre ice, close to the far stands.</summary>
        private static readonly Vector3 FromPosition = new(0f, 8f, -8f);

        /// <summary>What it looks at first: the middle of the far stands.</summary>
        private static readonly Vector3 FromTarget = new(0f, 3.6f, -21f);

        /// <summary>The first shot's vertical field of view, in degrees: the stands fill the frame.</summary>
        private const float FromFov = 24f;

        /// <summary>The camera a share of the way from the rafters to the broadcast view.</summary>
        /// <param name="broadcast">Where the move ends: the gameplay camera the menu shows.</param>
        /// <param name="move">0 in the rafters to 1 at the broadcast view, eased.</param>
        public static Camera3D At(Camera3D broadcast, float move)
        {
            float t = Math.Clamp(move, 0, 1);
            Vector3 fromDir = Vector3.Normalize(FromTarget - FromPosition);
            Vector3 toDir = Vector3.Normalize(broadcast.Target - broadcast.Position);
            Vector3 pos = Vector3.Lerp(FromPosition, broadcast.Position, t);
            Vector3 dir = Vector3.Normalize(Vector3.Lerp(fromDir, toDir, t));
            float reach = float.Lerp(
                Vector3.Distance(FromPosition, FromTarget),
                Vector3.Distance(broadcast.Position, broadcast.Target),
                t
            );
            return new Camera3D
            {
                Position = pos,
                Target = pos + (dir * reach),
                Up = Vector3.UnitY,
                FovY = float.Lerp(FromFov, broadcast.FovY, t),
                Projection = CameraProjection.Perspective,
            };
        }
    }
}
