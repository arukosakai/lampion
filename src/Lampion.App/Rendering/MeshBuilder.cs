using System.Numerics;

namespace Lampion.App.Rendering;

/// <summary>CPU-side triangle mesh: interleaved position, normal, uv (8 floats per vertex).</summary>
public sealed class MeshBuilder
{
    public List<float> Vertices { get; } = [];
    public List<uint> Indices { get; } = [];

    private uint VertexCount => (uint)(Vertices.Count / 8);

    public uint Add(Vector3 p, Vector3 n, Vector2 uv)
    {
        Vertices.AddRange([p.X, p.Y, p.Z, n.X, n.Y, n.Z, uv.X, uv.Y]);
        return VertexCount - 1;
    }

    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
    {
        var i = Add(a, normal, new(0, 0));
        Add(b, normal, new(1, 0));
        Add(c, normal, new(1, 1));
        Add(d, normal, new(0, 1));
        Indices.AddRange([i, i + 1, i + 2, i, i + 2, i + 3]);
    }

    /// <summary>Axis-aligned box between two corners, faces wound counter-clockwise from outside.</summary>
    public void Box(Vector3 min, Vector3 max)
    {
        var (x0, y0, z0) = (min.X, min.Y, min.Z);
        var (x1, y1, z1) = (max.X, max.Y, max.Z);
        Quad(new(x0, y0, z1), new(x1, y0, z1), new(x1, y1, z1), new(x0, y1, z1), Vector3.UnitZ);
        Quad(new(x1, y0, z0), new(x0, y0, z0), new(x0, y1, z0), new(x1, y1, z0), -Vector3.UnitZ);
        Quad(new(x1, y0, z1), new(x1, y0, z0), new(x1, y1, z0), new(x1, y1, z1), Vector3.UnitX);
        Quad(new(x0, y0, z0), new(x0, y0, z1), new(x0, y1, z1), new(x0, y1, z0), -Vector3.UnitX);
        Quad(new(x0, y1, z1), new(x1, y1, z1), new(x1, y1, z0), new(x0, y1, z0), Vector3.UnitY);
        Quad(new(x0, y0, z0), new(x1, y0, z0), new(x1, y0, z1), new(x0, y0, z1), -Vector3.UnitY);
    }

    /// <summary>Capped cylinder along +Z from z0 to z1.</summary>
    public void Cylinder(float radius, float z0, float z1, int segments = 20, bool caps = true)
    {
        var start = VertexCount;
        for (var i = 0; i <= segments; i++)
        {
            var a = i / (float)segments * MathF.Tau;
            var n = new Vector3(MathF.Cos(a), MathF.Sin(a), 0);
            Add(new Vector3(n.X * radius, n.Y * radius, z0), n, new(i / (float)segments, 0));
            Add(new Vector3(n.X * radius, n.Y * radius, z1), n, new(i / (float)segments, 1));
        }
        for (var i = 0u; i < segments; i++)
        {
            var k = start + i * 2;
            Indices.AddRange([k, k + 2, k + 1, k + 1, k + 2, k + 3]);
        }
        if (!caps)
            return;
        Disc(radius, z1, Vector3.UnitZ, segments);
        Disc(radius, z0, -Vector3.UnitZ, segments);
    }

    public void Disc(float radius, float z, Vector3 normal, int segments = 20)
    {
        var center = Add(new Vector3(0, 0, z), normal, new(0.5f, 0.5f));
        for (var i = 0; i <= segments; i++)
        {
            var a = i / (float)segments * MathF.Tau;
            Add(new Vector3(MathF.Cos(a) * radius, MathF.Sin(a) * radius, z), normal, new(0.5f + MathF.Cos(a) / 2, 0.5f + MathF.Sin(a) / 2));
        }
        for (var i = 0u; i < segments; i++)
            if (normal.Z > 0)
                Indices.AddRange([center, center + 1 + i, center + 2 + i]);
            else
                Indices.AddRange([center, center + 2 + i, center + 1 + i]);
    }

    /// <summary>Closed cone with its apex at the origin, opening along +Z to radius 1 at z = 1.</summary>
    public static MeshBuilder Cone(int segments = 40)
    {
        var mesh = new MeshBuilder();
        var apex = mesh.Add(Vector3.Zero, -Vector3.UnitZ, new(0.5f, 0));
        for (var i = 0; i <= segments; i++)
        {
            var a = i / (float)segments * MathF.Tau;
            var dir = new Vector3(MathF.Cos(a), MathF.Sin(a), 0);
            mesh.Add(new Vector3(dir.X, dir.Y, 1), Vector3.Normalize(dir - Vector3.UnitZ), new(i / (float)segments, 1));
        }
        for (var i = 0u; i < segments; i++)
            mesh.Indices.AddRange([apex, apex + 1 + i, apex + 2 + i]);
        mesh.Disc(1, 1, Vector3.UnitZ, segments);
        return mesh;
    }

    /// <summary>
    /// A box truss centred on the origin, running along X: four tube chords, ladder rungs and diagonal
    /// lacing on every face, like a real 30 cm aluminium square truss.
    /// </summary>
    public static MeshBuilder Truss(float length, float height, float depth)
    {
        var mesh = new MeshBuilder();
        var chord = Math.Min(height, depth) * 0.08f;
        var lace = chord * 0.45f;
        var hy = height / 2 - chord;
        var hz = depth / 2 - chord;
        var hx = length / 2;
        Vector3[] corners = [new(0, -hy, -hz), new(0, hy, -hz), new(0, hy, hz), new(0, -hy, hz)];
        foreach (var c in corners)
            mesh.Tube(new Vector3(-hx, c.Y, c.Z), new Vector3(hx, c.Y, c.Z), chord, 10);

        var bays = Math.Max(1, (int)MathF.Round(length / 0.5f));
        var bay = length / bays;
        for (var b = 0; b <= bays; b++)
        {
            var x = -hx + b * bay;
            for (var k = 0; k < 4; k++)
            {
                var c0 = corners[k];
                var c1 = corners[(k + 1) % 4];
                mesh.Tube(new Vector3(x, c0.Y, c0.Z), new Vector3(x, c1.Y, c1.Z), lace, 6);
                if (b < bays)
                {
                    var flip = (b + k) % 2 == 0;
                    var xa = flip ? x : x + bay;
                    var xb = flip ? x + bay : x;
                    mesh.Tube(new Vector3(xa, c0.Y, c0.Z), new Vector3(xb, c1.Y, c1.Z), lace, 6);
                }
            }
        }
        return mesh;
    }

    public void Tube(Vector3 a, Vector3 b, float radius, int segments)
    {
        var axis = b - a;
        var length = axis.Length();
        if (length < 1e-5f)
            return;
        var z = axis / length;
        var helper = MathF.Abs(z.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
        var x = Vector3.Normalize(Vector3.Cross(helper, z));
        var y = Vector3.Cross(z, x);
        var start = VertexCount;
        for (var i = 0; i <= segments; i++)
        {
            var angle = i / (float)segments * MathF.Tau;
            var n = x * MathF.Cos(angle) + y * MathF.Sin(angle);
            Add(a + n * radius, n, new(i / (float)segments, 0));
            Add(b + n * radius, n, new(i / (float)segments, 1));
        }
        for (var i = 0u; i < segments; i++)
        {
            var k = start + i * 2;
            Indices.AddRange([k, k + 2, k + 1, k + 1, k + 2, k + 3]);
        }
    }

    public static MeshBuilder UnitBox()
    {
        var mesh = new MeshBuilder();
        mesh.Box(new Vector3(-0.5f), new Vector3(0.5f));
        return mesh;
    }

    public static MeshBuilder UnitQuad()
    {
        var mesh = new MeshBuilder();
        mesh.Quad(new(-0.5f, -0.5f, 0), new(0.5f, -0.5f, 0), new(0.5f, 0.5f, 0), new(-0.5f, 0.5f, 0), Vector3.UnitZ);
        return mesh;
    }

    public static MeshBuilder UnitCylinder()
    {
        var mesh = new MeshBuilder();
        mesh.Cylinder(1, 0, 1, 24);
        return mesh;
    }
}
