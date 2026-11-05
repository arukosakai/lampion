namespace Lampion.App.Rendering;

internal sealed partial class StageMeshes
{
    /// <summary>A cached truss mesh and the frame it was last drawn in.</summary>
    private sealed class Truss(GpuMesh mesh)
    {
        public GpuMesh Mesh { get; } = mesh;

        public int LastUsed { get; set; }
    }
}
