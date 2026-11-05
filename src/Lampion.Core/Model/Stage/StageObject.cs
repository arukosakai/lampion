namespace Lampion.Core.Model.Stage;

/// <summary>A piece of set: an oriented box in metres, centred on <see cref="Position"/>. Edited in place by the stage builder.</summary>
public sealed class StageObject
{
    /// <summary>Stable id; a fresh random one by default.</summary>
    public string Id { get; set; } = Ids.New();

    /// <summary>Name shown in the project panel.</summary>
    public string Name { get; set; } = "Object";

    /// <summary>What the object is.</summary>
    public StageObjectKind Kind { get; set; } = StageObjectKind.Platform;

    /// <summary>Centre of the box, in metres.</summary>
    public Vec3 Position { get; set; }

    /// <summary>Width (local X), height (Y) and depth (Z) in metres. A truss runs along its width.</summary>
    public Vec3 Size { get; set; } = new(2, 0.3, 0.3);

    /// <summary>Degrees around Y.</summary>
    public double Yaw { get; set; }

    /// <summary>Surface colour as <c>#RRGGBB</c>.</summary>
    public string Color { get; set; } = "#3A3A40";

    /// <summary>A copy with a new id.</summary>
    public StageObject Clone() =>
        new()
        {
            Name = Name,
            Kind = Kind,
            Position = Position,
            Size = Size,
            Yaw = Yaw,
            Color = Color,
        };
}
