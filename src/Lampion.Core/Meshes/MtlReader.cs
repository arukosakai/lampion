using System.Numerics;

namespace Lampion.Core.Meshes;

/// <summary>Reads the parts of a Wavefront .mtl file that map onto <see cref="MeshMaterial"/>: colours, shininess, PBR extensions and map_Kd.</summary>
internal static class MtlReader
{
    // map_* options and how many values each takes, so the file name after them can be found.
    private static readonly Dictionary<string, int> MapOptions = new(StringComparer.Ordinal)
    {
        ["-blendu"] = 1,
        ["-blendv"] = 1,
        ["-bm"] = 1,
        ["-boost"] = 1,
        ["-cc"] = 1,
        ["-clamp"] = 1,
        ["-imfchan"] = 1,
        ["-mm"] = 2,
        ["-o"] = 3,
        ["-s"] = 3,
        ["-t"] = 3,
        ["-texres"] = 1,
    };

    /// <summary>The materials in the .mtl file(s) named by <paramref name="files"/>, relative to <paramref name="dir"/>. Missing files become warnings.</summary>
    public static List<(string Name, MeshMaterial Material)> Read(string dir, string files, List<string> warnings)
    {
        var materials = new List<(string, MeshMaterial)>();
        foreach (var file in SplitFiles(dir, files))
        {
            if (TextureFiles.Find(dir, dir, file) is { } path)
                ReadFile(path, dir, warnings, materials);
            else
                warnings.Add($"Material file \"{file}\" wasn't found; surfaces are drawn grey.");
        }
        return materials;
    }

    // "mtllib a.mtl b.mtl" names two files, but a single name may itself contain spaces.
    private static string[] SplitFiles(string dir, string files) =>
        File.Exists(Path.Combine(dir, files)) ? [files] : files.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static void ReadFile(string path, string objDir, List<string> warnings, List<(string, MeshMaterial)> into)
    {
        var mtlDir = Path.GetDirectoryName(path) ?? objDir;
        Draft? draft = null;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;
            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens[0] == "newmtl")
            {
                if (draft is not null)
                    into.Add((draft.Name, draft.Build()));
                draft = new Draft(line["newmtl".Length..].Trim());
                continue;
            }
            if (draft is null)
                continue;
            switch (tokens[0])
            {
                case "Kd" when tokens.Length >= 4:
                    draft.Color = new Vector3(Linear(tokens[1]), Linear(tokens[2]), Linear(tokens[3]));
                    break;
                case "Ke" when tokens.Length >= 4:
                    draft.Emissive = new Vector3(Linear(tokens[1]), Linear(tokens[2]), Linear(tokens[3]));
                    break;
                case "Ns" when tokens.Length >= 2:
                    // Phong exponent 0..1000 to roughness 1..0, roughly as glossy as it looked in the source app.
                    draft.Roughness ??= Math.Clamp(1 - MathF.Sqrt(Math.Max(0, ObjReader.Float(tokens[1]))) / 31.6f, 0.05f, 1);
                    break;
                case "Pr" when tokens.Length >= 2:
                    draft.Roughness = Math.Clamp(ObjReader.Float(tokens[1]), 0, 1);
                    break;
                case "Pm" when tokens.Length >= 2:
                    draft.Metallic = Math.Clamp(ObjReader.Float(tokens[1]), 0, 1);
                    break;
                case "map_Kd" when tokens.Length >= 2:
                    draft.Image = TextureFiles.Load(mtlDir, objDir, MapFile(tokens), warnings);
                    break;
            }
        }
        if (draft is not null)
            into.Add((draft.Name, draft.Build()));
    }

    private static string MapFile(string[] tokens)
    {
        var i = 1;
        while (i < tokens.Length - 1 && MapOptions.TryGetValue(tokens[i], out var values))
            i += 1 + values;
        return string.Join(' ', tokens[Math.Min(i, tokens.Length - 1)..]);
    }

    private static float Linear(string srgb) => MathF.Pow(Math.Clamp(ObjReader.Float(srgb), 0, 1), 2.2f);

    private sealed class Draft(string name)
    {
        public string Name { get; } = name;
        public Vector3 Color { get; set; } = new(0.8f);
        public Vector3 Emissive { get; set; }
        public float? Roughness { get; set; }
        public float Metallic { get; set; }
        public byte[]? Image { get; set; }

        public MeshMaterial Build() =>
            new()
            {
                Name = Name,
                // With a texture, Kd is usually a leftover grey that would only darken it.
                BaseColor = Image is null ? Color : Vector3.One,
                Emissive = Emissive,
                Roughness = Roughness ?? 0.6f,
                Metallic = Metallic,
                BaseColorImage = Image,
            };
    }
}
