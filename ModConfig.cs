namespace FashionSenseBuffs;

public class ModConfig
{
    /// <summary>Whether the mod applies buffs based on outfit. Disabling removes any active buffs immediately.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>When enabled, forces the selected PDW weather each morning (host only). Requires Cloudy Skies + PDW.</summary>
    public bool DebugForcePdwWeather { get; set; }

    /// <summary>PDW weather to force when <see cref="DebugForcePdwWeather"/> is enabled.</summary>
    public string DebugPdwWeather { get; set; } = "Heavy Rain";
}
