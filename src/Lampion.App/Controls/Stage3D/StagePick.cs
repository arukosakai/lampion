namespace Lampion.App.Controls.Stage3D;

/// <summary>What a click on the stage hit.</summary>
/// <param name="Id">The fixture or set piece id.</param>
/// <param name="IsFixture">True for a fixture, false for a set piece.</param>
/// <param name="Distance">Along the pick ray, in multiples of its (unit) direction, so metres.</param>
internal readonly record struct StagePick(string Id, bool IsFixture, float Distance);
