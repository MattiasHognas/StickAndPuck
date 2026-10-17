using Raylib_cs;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Render;

/// <summary>Applies CPU animation poses with raylib 6.0's WebGL normal-buffer workaround.</summary>
public static class ModelPose
{
    /// <summary>Updates a pose and uploads both positions and normals.</summary>
    public static unsafe void Apply(Model model, ModelAnimation animation, int frame)
    {
        if (!OperatingSystem.IsBrowser())
        {
            UpdateModelAnimation(model, animation, frame);
            return;
        }

        // raylib 6.0 uploads animated normals using SHADER_LOC_VERTEX_NORMAL (3),
        // although UploadMesh stores that buffer at RL_DEFAULT_SHADER_ATTRIB_LOCATION_NORMAL (2).
        // Temporarily alias the upload slot, then restore the colour buffer before drawing/unloading.
        Span<uint> colors = stackalloc uint[model.MeshCount];
        for (int i = 0; i < model.MeshCount; i++)
        {
            colors[i] = model.Meshes[i].VboId[3];
            model.Meshes[i].VboId[3] = model.Meshes[i].VboId[2];
        }
        try { UpdateModelAnimation(model, animation, frame); }
        finally
        {
            for (int i = 0; i < model.MeshCount; i++) model.Meshes[i].VboId[3] = colors[i];
        }
    }
}
