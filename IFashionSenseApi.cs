namespace FashionSenseBuffs;

/// <summary>Thin copy of Fashion Sense's public IApi interface — only the members we use.</summary>
public interface IFashionSenseApi
{
    /// <summary>Returns (true, outfitId) when an outfit is active, or (false, _) when none.</summary>
    KeyValuePair<bool, string> GetCurrentOutfitId();

    /// <summary>Fired whenever Fashion Sense marks the player sprite dirty (e.g. outfit change).</summary>
    event EventHandler SetSpriteDirtyTriggered;
}
