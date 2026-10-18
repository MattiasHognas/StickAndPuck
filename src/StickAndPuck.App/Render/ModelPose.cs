using Raylib_cs;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Render
{
    /// <summary>Applies CPU animation poses with raylib 6.0's WebGL normal-buffer workaround.</summary>
    public static class ModelPose
    {
        /// <summary>Updates a pose and uploads both positions and normals.</summary>
#pragma warning disable S6640 // Raylib interop requires pointer access here.
        public static unsafe void Apply(Model model, ModelAnimation animation, int frame)
#pragma warning restore S6640
        {
            if (!OperatingSystem.IsBrowser())
            {
                UpdateModelAnimation(model, animation, frame);
                return;
            }

            // raylib 6.0 uploads animated normals using SHADER_LOC_VERTEX_NORMAL (3),
            // although UploadMesh stores that buffer at RL_DEFAULT_SHADER_ATTRIB_LOCATION_NORMAL (2).
            // Temporarily alias the upload slot, then restore the colour buffer before drawing/unloading.
            uint[] colors = new uint[model.MeshCount];
            for (int i = 0; i < model.MeshCount; i++)
            {
                uint* vboIds = model.Meshes[i].VboId;
                colors[i] = vboIds[3];
                vboIds[3] = vboIds[2];
            }
            try
            {
                UpdateModelAnimation(model, animation, frame);
            }
            finally
            {
                for (int i = 0; i < model.MeshCount; i++)
                {
                    uint* vboIds = model.Meshes[i].VboId;
                    vboIds[3] = colors[i];
                }
            }
        }
    }
}
