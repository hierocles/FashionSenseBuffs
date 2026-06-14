namespace FashionSenseBuffs;

/// <summary>Minimal Cloudy Skies API surface for refreshing weather after debug overrides.</summary>
public interface ICloudySkiesApi
{
    void RegenerateLayers(string? weatherId = null);

    void RegenerateEffects(string? weatherId = null);
}
