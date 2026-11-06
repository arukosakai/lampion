using System.Globalization;
using System.Numerics;

namespace Lampion.Core.Meshes;

/// <summary>
/// Reads Wavefront OBJ files with their MTL materials and PNG/JPEG colour textures. Polygons are fanned into
/// triangles, faces without normals get flat ones, and UVs are flipped to the top-left origin glTF uses.
/// Units are taken as metres; <see cref="MeshImport"/> fixes up files that were clearly modelled in cm or mm.
/// </summary>
public static class ObjReader
{
    /// <summary>Reads <paramref name="path"/>; textures and .mtl files are looked up next to it.</summary>
    /// <exception cref="IOException">The file can't be read.</exception>
    /// <exception cref="InvalidDataException">A face points at a vertex that doesn't exist, or a number doesn't parse.</exception>
    public static MeshModel Read(string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        var model = new MeshModel();
        var positions = new List<Vector3>();
        var uvs = new List<Vector2>();
        var normals = new List<Vector3>();
        var materials = new Dictionary<string, MeshMaterial>(StringComparer.Ordinal);
        var parts = new Dictionary<string, PartBuilder>(StringComparer.Ordinal);
        var current = "";
        var lineNumber = 0;
        var faceNumber = 0;
        var corners = new List<(int V, int T, int N)>();

        foreach (var raw in File.ReadLines(path))
        {
            lineNumber++;
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;
            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            try
            {
                switch (tokens[0])
                {
                    case "v":
                        positions.Add(new Vector3(Float(tokens[1]), Float(tokens[2]), Float(tokens[3])));
                        break;
                    case "vt":
                        uvs.Add(new Vector2(Float(tokens[1]), tokens.Length > 2 ? 1 - Float(tokens[2]) : 1));
                        break;
                    case "vn":
                        normals.Add(SafeNormalize(new Vector3(Float(tokens[1]), Float(tokens[2]), Float(tokens[3]))));
                        break;
                    case "mtllib":
                        foreach (var (name, material) in MtlReader.Read(dir, line["mtllib".Length..].Trim(), model.Warnings))
                            materials[name] = material;
                        break;
                    case "usemtl":
                        current = line["usemtl".Length..].Trim();
                        break;
                    case "f" when tokens.Length >= 4:
                        corners.Clear();
                        for (var i = 1; i < tokens.Length; i++)
                            corners.Add(Corner(tokens[i], positions.Count, uvs.Count, normals.Count));
                        if (!parts.TryGetValue(current, out var part))
                            parts[current] = part = new PartBuilder();
                        part.AddPolygon(corners, positions, uvs, normals, faceNumber++);
                        break;
                }
            }
            catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or ArgumentOutOfRangeException)
            {
                throw new InvalidDataException($"Line {lineNumber} of {Path.GetFileName(path)} can't be read: \"{line}\".", ex);
            }
        }

        foreach (var (name, part) in parts)
        {
            var defined = materials.TryGetValue(name, out var material);
            if (name.Length > 0 && !defined)
                model.Warnings.Add($"Material \"{name}\" isn't defined in any .mtl file; it's drawn grey.");
            model.Parts.Add(part.Build(material ?? new MeshMaterial { Name = name }));
        }
        return model;
    }

    /// <summary>Parses an OBJ/MTL number, which is always written with a dot whatever the machine's culture.</summary>
    internal static float Float(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static (int V, int T, int N) Corner(string token, int positionCount, int uvCount, int normalCount)
    {
        var fields = token.Split('/');
        var v = Index(fields[0], positionCount);
        var t = fields.Length > 1 && fields[1].Length > 0 ? Index(fields[1], uvCount) : -1;
        var n = fields.Length > 2 && fields[2].Length > 0 ? Index(fields[2], normalCount) : -1;
        return (v, t, n);
    }

    // OBJ indices are 1-based, and negative ones count back from the latest element.
    private static int Index(string field, int count)
    {
        var i = int.Parse(field, NumberStyles.Integer, CultureInfo.InvariantCulture);
        var index = i > 0 ? i - 1 : count + i;
        if (index < 0 || index >= count)
            throw new ArgumentOutOfRangeException(nameof(field), $"Index {i} is outside the {count} defined so far.");
        return index;
    }

    private static Vector3 SafeNormalize(Vector3 v) => v.LengthSquared() > 0 ? Vector3.Normalize(v) : Vector3.UnitY;

    private sealed class PartBuilder
    {
        private readonly List<float> _vertices = [];
        private readonly List<uint> _indices = [];
        private readonly Dictionary<(int, int, int), uint> _shared = [];

        public void AddPolygon(List<(int V, int T, int N)> corners, List<Vector3> positions, List<Vector2> uvs, List<Vector3> normals, int face)
        {
            var flat = FlatNormal(corners, positions);
            var ids = new uint[corners.Count];
            for (var i = 0; i < corners.Count; i++)
            {
                var (v, t, n) = corners[i];
                // Corners with their own normal can be shared between faces; flat-shaded ones belong to this face only.
                var key = n >= 0 ? (v, t, n) : (v, t, -1 - face);
                if (!_shared.TryGetValue(key, out var id))
                {
                    id = (uint)(_vertices.Count / MeshPart.FloatsPerVertex);
                    var p = positions[v];
                    var normal = n >= 0 ? normals[n] : flat;
                    var uv = t >= 0 ? uvs[t] : Vector2.Zero;
                    _vertices.AddRange([p.X, p.Y, p.Z, normal.X, normal.Y, normal.Z, uv.X, uv.Y]);
                    _shared[key] = id;
                }
                ids[i] = id;
            }
            for (var i = 1; i + 1 < corners.Count; i++)
                _indices.AddRange([ids[0], ids[i], ids[i + 1]]);
        }

        public MeshPart Build(MeshMaterial material) => new(material, [.. _vertices], [.. _indices]);

        // Newell's method: robust for slightly non-planar and concave polygons.
        private static Vector3 FlatNormal(List<(int V, int T, int N)> corners, List<Vector3> positions)
        {
            var n = Vector3.Zero;
            for (var i = 0; i < corners.Count; i++)
            {
                var a = positions[corners[i].V];
                var b = positions[corners[(i + 1) % corners.Count].V];
                n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
            }
            return SafeNormalize(n);
        }
    }
}
