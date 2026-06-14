using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace FashionSenseBuffs;

public class ModEntry : Mod
{
    /// <summary>Asset path exposed for Content Patcher (and other mods) to patch.</summary>
    internal const string AssetPath = "hierocles.FashionSenseBuffs/Outfits";

    private IFashionSenseApi? _fsApi;
    private ModConfig _config = new();
    private Dictionary<string, OutfitBuffEntry> _outfitData = new();
    private string? _currentOutfitId;
    private readonly List<string> _activeBuffIds = new();

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<ModConfig>();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Content.AssetRequested += OnAssetRequested;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        _fsApi = Helper.ModRegistry.GetApi<IFashionSenseApi>("PeacefulEnd.FashionSense");
        if (_fsApi is null)
        {
            Monitor.Log("Fashion Sense API not found — mod will not function.", LogLevel.Error);
            return;
        }

        _fsApi.SetSpriteDirtyTriggered += OnSpriteDirty;
        Monitor.Log("Fashion Sense API acquired.", LogLevel.Debug);

        RegisterGmcm();
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (!e.Name.IsEquivalentTo(AssetPath)) return;

        // Provide the base data from assets/outfits.json; CP and other mods can then EditData on top.
        e.LoadFrom(
            () => Helper.Data.ReadJsonFile<Dictionary<string, OutfitBuffEntry>>("assets/outfits.json")
                  ?? new Dictionary<string, OutfitBuffEntry>(),
            AssetLoadPriority.Low
        );
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        Helper.GameContent.InvalidateCache(AssetPath);
        LoadOutfitData();
        _currentOutfitId = null;
        _activeBuffIds.Clear();
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        // Invalidate so CP patches with day/season conditions are re-evaluated.
        Helper.GameContent.InvalidateCache(AssetPath);
        LoadOutfitData();
        _currentOutfitId = null;
        ApplyBuffsForCurrentOutfit();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        _currentOutfitId = null;
        _activeBuffIds.Clear();
    }

    private void OnSpriteDirty(object? sender, EventArgs e)
    {
        if (!Context.IsPlayerFree) return;
        ApplyBuffsForCurrentOutfit();
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        // Fallback poll in case SetSpriteDirtyTriggered doesn't fire for outfit switches.
        if (!e.IsMultipleOf(60) || !Context.IsPlayerFree) return;
        ApplyBuffsForCurrentOutfit();
    }

    private void ApplyBuffsForCurrentOutfit()
    {
        if (_fsApi is null || !_config.Enabled) return;

        var result = _fsApi.GetCurrentOutfitId();
        // result.Key is false when no outfit is active.
        string? newOutfitId = result.Key ? result.Value : null;

        if (newOutfitId == _currentOutfitId) return;

        RemoveActiveBuffs();
        _currentOutfitId = newOutfitId;

        if (newOutfitId is null) return;

        // Case-insensitive lookup against the dictionary keys.
        var key = _outfitData.Keys.FirstOrDefault(k =>
            string.Equals(k, newOutfitId, StringComparison.OrdinalIgnoreCase));
        if (key is null) return;

        var entry = _outfitData[key];
        var buffData = DataLoader.Buffs(Game1.content);
        foreach (var buffId in entry.BuffIds)
        {
            if (!buffData.ContainsKey(buffId))
            {
                Monitor.Log($"Buff '{buffId}' not found in Data/Buffs.", LogLevel.Warn);
                continue;
            }

            Game1.player.applyBuff(new Buff(buffId));
            _activeBuffIds.Add(buffId);
            Monitor.Log($"Applied buff '{buffId}' for outfit '{newOutfitId}'.", LogLevel.Trace);
        }
    }

    private void RemoveActiveBuffs()
    {
        foreach (var buffId in _activeBuffIds)
            Game1.player.buffs.Remove(buffId);
        _activeBuffIds.Clear();
    }

    private void LoadOutfitData()
    {
        _outfitData = Helper.GameContent.Load<Dictionary<string, OutfitBuffEntry>>(AssetPath);
        Monitor.Log($"Loaded {_outfitData.Count} outfit-buff mapping(s) from {AssetPath}.", LogLevel.Debug);
    }

    private void RegisterGmcm()
    {
        var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null) return;

        gmcm.Register(
            mod: ModManifest,
            reset: () => _config = new ModConfig(),
            save: () => Helper.WriteConfig(_config)
        );

        gmcm.AddSectionTitle(ModManifest, () => "Settings");

        gmcm.AddBoolOption(
            mod: ModManifest,
            getValue: () => _config.Enabled,
            setValue: v =>
            {
                _config.Enabled = v;
                if (!v) RemoveActiveBuffs();
            },
            name: () => "Enable outfit buffs",
            tooltip: () => "When enabled, wearing a mapped Fashion Sense outfit automatically applies its buffs."
        );

        gmcm.AddSectionTitle(ModManifest, () => "Active Outfit Mappings");

        gmcm.AddParagraph(ModManifest, () =>
        {
            if (_outfitData.Count == 0)
                return "No mappings loaded. Provide data via assets/outfits.json or a Content Patcher pack targeting\n" + AssetPath;

            return string.Join("\n", _outfitData.Select(kvp =>
                $"{kvp.Key}  →  {string.Join(", ", kvp.Value.BuffIds)}"));
        });

        gmcm.AddParagraph(ModManifest, () =>
            $"To add mappings, edit assets/outfits.json or create a Content Patcher pack that targets \"{AssetPath}\" with an EditData action.");

        gmcm.AddParagraph(ModManifest, () =>
            $"Note: Outfit mapping won't show up here until a save is first loaded!");
    }
}
