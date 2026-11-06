using Lampion.Core.Meshes;
using Lampion.Core.Model;
using Lampion.Core.Model.Stage;
using Lampion.Core.Persistence;

namespace Lampion.Core.Tests;

public sealed class ModelAssetsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("lampion-assets-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void AssetsLiveNextToTheShow()
    {
        var show = Path.Combine(_dir, "Summer Tour.lampion");
        Assert.Equal(Path.Combine(_dir, "Summer Tour Assets"), ModelAssets.AssetsDirectory(show));
        Assert.Equal(Path.Combine(_dir, "Summer Tour Assets", "Models", "Car.glb"), ModelAssets.Resolve(show, "Models/Car.glb"));
    }

    [Fact]
    public void ImportWritesAGlbWithAFreshNameEachTime()
    {
        var show = Path.Combine(_dir, "Show.lampion");
        var source = WriteObj("Prop v2.obj", "v 0 0 0\nv 2 0 0\nv 0 1 0\nf 1 2 3\n");

        var first = ModelAssets.Import(source, show);
        var second = ModelAssets.Import(source, show);

        Assert.Equal("Models/Prop v2.glb", first.Model);
        Assert.Equal("Models/Prop v2 2.glb", second.Model);
        Assert.True(File.Exists(ModelAssets.Resolve(show, first.Model)));
        Assert.Equal(1, first.Triangles);
        // The triangle is flat, so its depth is padded to stay pickable.
        Assert.Equal(new Vec3(2, 1, 0.01), new Vec3(first.Size.X, first.Size.Y, Math.Round(first.Size.Z, 3)));
        var stored = GltfReader.Read(ModelAssets.Resolve(show, first.Model));
        Assert.Equal(System.Numerics.Vector3.Zero, (stored.Bounds().Min + stored.Bounds().Max) * 0.5f);
    }

    [Fact]
    public void SaveAsCarriesTheAssetsAlong()
    {
        var show = Path.Combine(_dir, "Show.lampion");
        var imported = ModelAssets.Import(WriteObj("box.obj", "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n"), show);
        var copy = Path.Combine(_dir, "elsewhere", "Show copy.lampion");

        Assert.Equal(1, ModelAssets.CopyAssets(show, copy));
        Assert.True(File.Exists(ModelAssets.Resolve(copy, imported.Model)));
        Assert.Equal(0, ModelAssets.CopyAssets(show, show));
        Assert.Equal(0, ModelAssets.CopyAssets(Path.Combine(_dir, "no assets.lampion"), copy));
    }

    [Fact]
    public void ModelObjectsSurviveSaving()
    {
        var project = new Project();
        project.Stage.Objects.Add(new StageObject { Name = "Car", Kind = StageObjectKind.Model, Model = "Models/Car.glb", Size = new Vec3(4.5, 1.4, 1.8) });

        var json = ProjectSerializer.Serialize(project);
        Assert.Contains("\"Model\"", json);
        var car = ProjectSerializer.Deserialize(json).Stage.Objects.Single(o => o.Name == "Car");

        Assert.Equal(StageObjectKind.Model, car.Kind);
        Assert.Equal("Models/Car.glb", car.Model);
        Assert.Equal("Models/Car.glb", car.Clone().Model);
    }

    [Fact]
    public void LightPassesThroughModels()
    {
        var car = new StageObject { Kind = StageObjectKind.Model, Model = "Models/Car.glb", Position = new Vec3(0, 1, 0), Size = new Vec3(2, 2, 2) };
        Assert.Equal(5, StageMath.Raycast([car], new Vec3(0, 5, 0), new Vec3(0, -1, 0))!.Value, 6);
    }

    private string WriteObj(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text);
        return path;
    }
}
