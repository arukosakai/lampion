using System.Numerics;
using Lampion.Core.Meshes;

namespace Lampion.App.Rendering.Models;

/// <summary>A model read and decoded off the render thread, ready to upload; see <see cref="ModelLoader"/>.</summary>
/// <param name="Size">Width, height and depth of its bounding box as stored, metres; the stage scales it to the object's size.</param>
/// <param name="Parts">Its surfaces, each with its colour texture decoded (or null).</param>
internal sealed record LoadedModel(Vector3 Size, IReadOnlyList<(MeshPart Part, DecodedImage? Image)> Parts);
