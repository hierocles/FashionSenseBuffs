namespace FashionSenseBuffs;

public class ModConfig
{
    /// <summary>Whether the mod applies buffs based on outfit. Disabling removes any active buffs immediately.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Whether to show on-screen notifications when outfit buffs are applied or removed.</summary>
    public bool ShowBuffNotifications { get; set; } = true;

    /// <summary>Per-mapping outdoors-only overrides keyed by outfit mapping ID. Overrides Content Patcher defaults.</summary>
    public Dictionary<string, bool> OutdoorsOnlyOverrides { get; set; } = new();

    // Future override types: add a dedicated dictionary (or nested MappingOverrides class) per type,
    // e.g. Dictionary<string, bool> EnabledOverrides. Each type needs its own prune + GetEffective* resolver.
}
