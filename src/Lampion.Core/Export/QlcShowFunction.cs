namespace Lampion.Core.Export;

/// <summary>A function placed on a Show Manager track.</summary>
/// <param name="Id">QLC+ function id.</param>
/// <param name="Start">Milliseconds from the start of the show.</param>
/// <param name="Duration">Milliseconds.</param>
/// <param name="Color">Block colour, <c>#RRGGBB</c>.</param>
internal readonly record struct QlcShowFunction(int Id, int Start, int Duration, string Color);
