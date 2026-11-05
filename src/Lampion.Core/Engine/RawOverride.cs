namespace Lampion.Core.Engine;

/// <summary>A value forced onto one channel of a fixture by channel automation.</summary>
/// <param name="Offset">0-based channel within the fixture's footprint; offsets outside it are ignored when mapping.</param>
/// <param name="Value">Level 0..1 (clamped when mapped to DMX).</param>
public readonly record struct RawOverride(int Offset, double Value);
