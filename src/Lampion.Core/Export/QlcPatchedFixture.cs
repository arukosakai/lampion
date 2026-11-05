using Lampion.Core.Model.Rig;

namespace Lampion.Core.Export;

/// <summary>A Lampion fixture and the QLC+ fixture it becomes.</summary>
/// <param name="Fixture">The Lampion fixture.</param>
/// <param name="FixtureIndex">Its position in the project's fixture list, for reading rendered frames.</param>
/// <param name="Profile">Its profile (never empty: fixtures without channels are left out).</param>
/// <param name="QlcId">The QLC+ fixture id.</param>
/// <param name="IsNew">True when the export adds it; false when a base-project fixture at the same address is reused.</param>
internal sealed record QlcPatchedFixture(Fixture Fixture, int FixtureIndex, FixtureProfile Profile, int QlcId, bool IsNew);
