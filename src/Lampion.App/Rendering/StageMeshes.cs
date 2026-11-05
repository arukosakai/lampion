using System.Numerics;
using Silk.NET.OpenGL;

namespace Lampion.App.Rendering;

/// <summary>
/// The renderer's GPU geometry: unit primitives built once, and truss meshes built per size the first time
/// a truss of that size is drawn. Truss sizes nobody has drawn for a while are freed, so resizing a truss
/// doesn't leave a trail of meshes behind.
/// </summary>
internal sealed partial class StageMeshes : IDisposable
{
    // Frames a truss mesh may go undrawn before it is freed; a couple of seconds at 60 fps.
    private const int TrussIdleFrames = 120;

    private readonly GL _gl;
    private readonly Dictionary<(int, int, int), Truss> _trusses = [];
    private readonly List<(int, int, int)> _idle = [];
    private int _frame;

    /// <summary>Builds and uploads the unit primitives; the GL context must be current.</summary>
    public StageMeshes(GL gl)
    {
        _gl = gl;
        Box = new GpuMesh(gl, MeshBuilder.UnitBox());
        Quad = new GpuMesh(gl, MeshBuilder.UnitQuad());
        Cylinder = new GpuMesh(gl, MeshBuilder.UnitCylinder());
        Cone = new GpuMesh(gl, MeshBuilder.Cone());
    }

    /// <summary>See <see cref="MeshBuilder.UnitBox"/>.</summary>
    public GpuMesh Box { get; }

    /// <summary>See <see cref="MeshBuilder.UnitQuad"/>.</summary>
    public GpuMesh Quad { get; }

    /// <summary>See <see cref="MeshBuilder.UnitCylinder"/>.</summary>
    public GpuMesh Cylinder { get; }

    /// <summary>See <see cref="MeshBuilder.Cone"/>.</summary>
    public GpuMesh Cone { get; }

    /// <summary>The truss mesh for a size in metres (to the millimetre), built on first use.</summary>
    public GpuMesh TrussMesh(Vector3 size)
    {
        var key = ((int)(size.X * 1000), (int)(size.Y * 1000), (int)(size.Z * 1000));
        if (!_trusses.TryGetValue(key, out var truss))
        {
            truss = new Truss(new GpuMesh(_gl, MeshBuilder.Truss(size.X, size.Y, size.Z)));
            _trusses[key] = truss;
        }
        truss.LastUsed = _frame;
        return truss.Mesh;
    }

    /// <summary>Call once per rendered frame, after drawing: frees truss meshes that have gone unused.</summary>
    public void EndFrame()
    {
        _frame++;
        if (_frame % TrussIdleFrames != 0)
            return;
        foreach (var (key, truss) in _trusses)
            if (_frame - truss.LastUsed > TrussIdleFrames)
                _idle.Add(key);
        foreach (var key in _idle)
        {
            _trusses[key].Mesh.Dispose();
            _trusses.Remove(key);
        }
        _idle.Clear();
    }

    /// <summary>Frees every mesh.</summary>
    public void Dispose()
    {
        foreach (var truss in _trusses.Values)
            truss.Mesh.Dispose();
        _trusses.Clear();
        Box.Dispose();
        Quad.Dispose();
        Cylinder.Dispose();
        Cone.Dispose();
    }
}
