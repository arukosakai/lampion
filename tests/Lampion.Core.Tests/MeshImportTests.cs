using System.Globalization;
using System.Numerics;
using Lampion.Core.Meshes;

namespace Lampion.Core.Tests;

public sealed class MeshImportTests : IDisposable
{
    // A real 1×1 PNG, so writers that validate images accept it.
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly string _dir = Directory.CreateTempSubdirectory("lampion-mesh-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ObjQuadWithMaterialAndTexture()
    {
        var obj = WriteTexturedQuad();
        var model = ObjReader.Read(obj);

        var part = Assert.Single(model.Parts);
        Assert.Equal(2, part.Indices.Length / 3);
        Assert.Equal(4, part.VertexCount);
        Assert.Equal("Wood", part.Material.Name);
        Assert.Equal(Png, part.Material.BaseColorImage);
        Assert.Equal(Vector3.One, part.Material.BaseColor);
        // vt 0 0 is the bottom-left in OBJ, which is v = 1 in glTF's top-left convention.
        Assert.Equal(1, part.Vertices[7]);
        Assert.Empty(model.Warnings);
    }

    [Fact]
    public void ObjWithoutNormalsGetsFlatOnesAndNegativeIndicesWork()
    {
        var obj = Write("tri.obj", "v 0 0 0\nv 1 0 0\nv 0 1 0\nf -3 -2 -1\n");
        var part = Assert.Single(ObjReader.Read(obj).Parts);
        Assert.Equal(new Vector3(0, 0, 1), new Vector3(part.Vertices[3], part.Vertices[4], part.Vertices[5]));
    }

    [Fact]
    public void ObjMissingTextureWarnsAndKeepsTheColour()
    {
        Write("m.mtl", "newmtl Red\nKd 1 0 0\nmap_Kd -s 1 1 1 C:\\elsewhere\\red.png\n");
        var obj = Write("red.obj", "mtllib m.mtl\nusemtl Red\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
        var model = ObjReader.Read(obj);
        Assert.Contains(model.Warnings, w => w.Contains("red.png"));
        Assert.Equal(new Vector3(1, 0, 0), model.Parts[0].Material.BaseColor);
    }

    [Fact]
    public void ObjFindsATextureByNameWhenItsPathIsFromAnotherMachine()
    {
        File.WriteAllBytes(Path.Combine(_dir, "red.png"), Png);
        Write("m.mtl", "newmtl Red\nmap_Kd C:\\Users\\someone\\Desktop\\red.png\n");
        var obj = Write("red.obj", "mtllib m.mtl\nusemtl Red\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
        Assert.Equal(Png, ObjReader.Read(obj).Parts[0].Material.BaseColorImage);
    }

    [Fact]
    public void ObjFaceOutsideTheVertexListIsReportedWithItsLine()
    {
        var obj = Write("bad.obj", "v 0 0 0\nv 1 0 0\nf 1 2 3\n");
        var ex = Assert.Throws<InvalidDataException>(() => ObjReader.Read(obj));
        Assert.Contains("Line 3", ex.Message);
    }

    [Fact]
    public void ObjNumbersParseWhateverTheMachineCulture()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
        try
        {
            var obj = Write("half.obj", "v 0.5 0 0\nv 1.5 0 0\nv 0.5 1 0\nf 1 2 3\n");
            Assert.Equal(0.5f, ObjReader.Read(obj).Parts[0].Vertices[0]);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void NormalizeTurnsCentimetresIntoMetresAndCentres()
    {
        var obj = Write("cm.obj", "v 100 0 0\nv 280 0 0\nv 100 90 0\nf 1 2 3\n");
        var model = ObjReader.Read(obj);
        var note = MeshImport.Normalize(model);

        Assert.Contains("centimetres", note);
        var (min, max) = model.Bounds();
        Assert.Equal(1.8f, model.Size().X, 4);
        Assert.Equal(Vector3.Zero, (min + max) * 0.5f);
    }

    [Fact]
    public void NormalizeLeavesMetresAlone()
    {
        var model = ObjReader.Read(Write("m.obj", "v 0 0 0\nv 2 0 0\nv 0 3 0\nf 1 2 3\n"));
        Assert.Null(MeshImport.Normalize(model));
        Assert.Equal(new Vector3(2, 3, 0), model.Size());
    }

    [Fact]
    public void NormalizeRejectsAModelWithoutTriangles()
    {
        var model = ObjReader.Read(Write("empty.obj", "v 0 0 0\n"));
        Assert.Throws<InvalidDataException>(() => MeshImport.Normalize(model));
    }

    [Fact]
    public void ObjSurvivesTheTripThroughGlb()
    {
        var model = ObjReader.Read(WriteTexturedQuad());
        MeshImport.Normalize(model);
        var glb = Path.Combine(_dir, "quad.glb");
        GlbWriter.Write(model, glb);

        var back = GltfReader.Read(glb);
        Assert.Equal(model.TriangleCount, back.TriangleCount);
        Assert.Equal(model.Bounds(), back.Bounds());
        var original = model.Parts[0].Material;
        var material = Assert.Single(back.Parts).Material;
        Assert.Equal(Png, material.BaseColorImage);
        Assert.Equal(original.BaseColor, material.BaseColor);
        Assert.Equal(original.Roughness, material.Roughness, 4);
        Assert.Equal(original.Metallic, material.Metallic, 4);
        Assert.Empty(back.Warnings);
    }

    [Fact]
    public void ImportPicksTheReaderByExtension()
    {
        Assert.True(MeshImport.CanImport("Stage.GLB"));
        Assert.False(MeshImport.CanImport("stage.fbx"));
        Assert.Throws<InvalidDataException>(() => MeshImport.Read(Write("stage.fbx", "")));
    }

    private string WriteTexturedQuad()
    {
        File.WriteAllBytes(Path.Combine(_dir, "wood.png"), Png);
        Write("quad.mtl", "newmtl Wood\nKd 0.5 0.5 0.5\nNs 250\nmap_Kd wood.png\n");
        return Write(
            "quad.obj",
            "mtllib quad.mtl\nv 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nvt 0 0\nvt 1 0\nvt 1 1\nvt 0 1\nvn 0 0 1\nusemtl Wood\nf 1/1/1 2/2/1 3/3/1 4/4/1\n");
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text);
        return path;
    }
}
