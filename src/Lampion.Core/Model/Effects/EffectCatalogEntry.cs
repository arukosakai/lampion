namespace Lampion.Core.Model.Effects;

/// <summary>One effect in the Effects &amp; Presets library.</summary>
/// <param name="Kind">Stable key for <see cref="EffectCatalog.Create"/>, e.g. "rainbow" (also the JSON discriminator).</param>
/// <param name="Name">Display name; matches the effect's <see cref="Effect.DisplayName"/>.</param>
/// <param name="Description">One-line description for the library and tooltips.</param>
public readonly record struct EffectCatalogEntry(string Kind, string Name, string Description);
