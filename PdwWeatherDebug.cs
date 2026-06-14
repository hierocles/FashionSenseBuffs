using StardewModdingAPI;
using StardewValley;
using StardewValley.Network;

namespace FashionSenseBuffs;

/// <summary>Debug helper for forcing Project Danger Weather via Cloudy Skies weather data.</summary>
internal static class PdwWeatherDebug
{
    internal const string CloudySkiesModId = "leclair.cloudyskies";
    private const string WeatherDataAsset = "Mods/leclair.cloudyskies/WeatherData";

    /// <summary>Display name → Cloudy Skies weather id (PDW).</summary>
    internal static readonly IReadOnlyList<string> WeatherChoices = new[]
    {
        "Heavy Rain",
        "Meatballs",
        "Locust",
        "Tornado",
        "Heat Wave",
        "Wildfire",
        "Dry Lightning",
        "Sandstorm",
        "Acid Rain",
        "Mud Rain",
        "Smog",
        "Hail",
        "Blizzard",
        "Tropical Storm",
    };

    private static readonly Dictionary<string, string> WeatherIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Heavy Rain"] = "kath.weathering_HeavyRain",
        ["Meatballs"] = "kath.weathering_Meatball",
        ["Locust"] = "kath.weathering_Locust",
        ["Tornado"] = "kath.weathering_Tornado",
        ["Heat Wave"] = "kath.weathering_HeatWave",
        ["Wildfire"] = "kath.weathering_Wildfire",
        ["Dry Lightning"] = "kath.weathering_DryLightning",
        ["Sandstorm"] = "kath.weathering_Sandstorm",
        ["Acid Rain"] = "kath.weathering_AcidRain",
        ["Mud Rain"] = "kath.weathering_MudRain",
        ["Smog"] = "kath.weathering_Smog",
        ["Hail"] = "kath.weathering_Hail",
        ["Blizzard"] = "kath.weathering_Blizzard",
        ["Tropical Storm"] = "kath.weathering_Hurry",
    };

    internal static bool TryGetWeatherId(string displayName, out string weatherId) =>
        WeatherIds.TryGetValue(displayName, out weatherId!);

    internal static bool TryForceWeather(IModHelper helper, IMonitor monitor, string weatherId)
    {
        if (Game1.player == null || Game1.netWorldState?.Value == null)
        {
            monitor.Log("Load a save first.", LogLevel.Error);
            return false;
        }

        if (!Game1.IsMasterGame)
        {
            monitor.Log("Only the host can force weather.", LogLevel.Error);
            return false;
        }

        if (!helper.ModRegistry.IsLoaded(CloudySkiesModId))
        {
            monitor.Log("Cloudy Skies (leclair.cloudyskies) is required for PDW weather.", LogLevel.Error);
            return false;
        }

        var contextId = Game1.currentLocation?.GetLocationContextId() ?? "Default";
        var locationWeather = Game1.netWorldState.Value.GetWeatherForLocation(contextId);
        if (locationWeather.Weather == weatherId)
            return true;

        if (!TryApplyWeatherFlags(helper, monitor, locationWeather, weatherId))
            return false;

        SyncDefaultContextFlags(contextId, locationWeather);
        RefreshWeatherPresentation(locationWeather);

        var cloudySkies = helper.ModRegistry.GetApi<ICloudySkiesApi>(CloudySkiesModId);
        cloudySkies?.RegenerateLayers(weatherId);
        cloudySkies?.RegenerateEffects(weatherId);

        return true;
    }

    internal static void ApplyIfEnabled(IModHelper helper, IMonitor monitor, ModConfig config)
    {
        if (!config.DebugForcePdwWeather)
            return;

        if (!TryGetWeatherId(config.DebugPdwWeather, out var weatherId))
        {
            monitor.Log(
                $"Unknown debug PDW weather '{config.DebugPdwWeather}'. Choose one of: {string.Join(", ", WeatherChoices)}.",
                LogLevel.Warn
            );
            return;
        }

        if (TryForceWeather(helper, monitor, weatherId))
            monitor.Log($"Debug: forced PDW weather to {config.DebugPdwWeather} ({weatherId}).", LogLevel.Info);
    }

    private static bool TryApplyWeatherFlags(
        IModHelper helper,
        IMonitor monitor,
        LocationWeather locationWeather,
        string weatherId)
    {
        var weatherData = helper.GameContent.Load<Dictionary<string, CloudySkiesWeatherData>>(WeatherDataAsset);
        if (!weatherData.TryGetValue(weatherId, out var data))
        {
            monitor.Log(
                $"Weather id '{weatherId}' was not found in {WeatherDataAsset}. Is Project Danger Weather installed?",
                LogLevel.Error
            );
            return false;
        }

        locationWeather.Weather = weatherId;
        locationWeather.IsRaining = data.IsRaining;
        locationWeather.IsSnowing = data.IsSnowing;
        locationWeather.IsLightning = data.IsLightning;
        locationWeather.IsDebrisWeather = data.IsDebrisWeather;
        locationWeather.IsGreenRain = data.IsGreenRain;

        return true;
    }

    private static void SyncDefaultContextFlags(string contextId, LocationWeather locationWeather)
    {
        if (contextId != "Default")
            return;

        Game1.isRaining = locationWeather.IsRaining;
        Game1.isSnowing = locationWeather.IsSnowing;
        Game1.isLightning = locationWeather.IsLightning;
        Game1.isDebrisWeather = locationWeather.IsDebrisWeather;
        Game1.isGreenRain = locationWeather.IsGreenRain;
    }

    private static void RefreshWeatherPresentation(LocationWeather locationWeather)
    {
        var wasDebris = Game1.isDebrisWeather
            && Game1.currentLocation?.IsOutdoors == true
            && Game1.currentLocation?.ignoreDebrisWeather.Value == false;

        if (locationWeather.IsDebrisWeather && !wasDebris)
            Game1.populateDebrisWeatherArray();

        Game1.updateWeatherIcon();

        if (Game1.currentLocation != null)
            GameLocation.HandleMusicChange(Game1.currentLocation, Game1.currentLocation);
    }

    /// <summary>Subset of Cloudy Skies weather data fields needed to sync vanilla flags.</summary>
    private class CloudySkiesWeatherData
    {
        public bool IsRaining { get; set; }
        public bool IsSnowing { get; set; }
        public bool IsLightning { get; set; }
        public bool IsDebrisWeather { get; set; }
        public bool IsGreenRain { get; set; }
    }
}
