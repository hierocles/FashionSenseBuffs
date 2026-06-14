namespace FashionSenseBuffs;

public class OutfitBuffEntry
{
    /// <summary>Buff IDs to apply while this outfit is active.</summary>
    public List<string> BuffIds { get; set; } = new();

    /// <summary>Buff IDs to remove from the player while this outfit is active (e.g. weather debuffs).</summary>
    public List<string> RemoveBuffIds { get; set; } = new();
}
