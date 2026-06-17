namespace FashionSenseBuffs;

public class OutfitBuffEntry
{
    /// <summary>Buff IDs to apply while this outfit is active.</summary>
    public List<string> BuffIds { get; set; } = new();

    /// <summary>Buff IDs to remove from the player while this outfit is active (e.g. weather debuffs).</summary>
    public List<string> RemoveBuffIds { get; set; } = new();

    /// <summary>When true, buffs and RemoveBuffIds only apply while the player is outdoors.</summary>
    public bool OutdoorsOnly { get; set; }

    /// <summary>
    /// Additional Fashion Sense outfit names that share this mapping (exact match, case-insensitive).
    /// Use for seasonal, indoor, or mod-specific name variants.
    /// </summary>
    public List<string> Aliases { get; set; } = new();
}
