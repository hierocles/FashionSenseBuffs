using GenericModConfigMenu;
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
    private string? _pendingOutfitId;
    private int _pendingOutfitTicks;
    private readonly List<string> _activeBuffIds = new();
    private readonly List<string> _activeRemoveBuffIds = new();
    private string? _lastLoggedUnmappedOutfit;

    /// <summary>Consecutive reads required before switching away from the current outfit.</summary>
    private const int OutfitChangeStableTicks = 3;

    /// <summary>How often to poll Fashion Sense for outfit-id changes.</summary>
    private const int OutfitPollIntervalTicks = 15;

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<ModConfig>();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.GameLoop.UpdateTicked += OnOutfitPollTicked;
        helper.Events.GameLoop.UpdateTicked += OnApplyProtectionBuffs;
        helper.Events.GameLoop.UpdateTicked += OnStripWeatherDebuffs;
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
        ResetOutfitState();
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        Helper.GameContent.InvalidateCache(AssetPath);
        LoadOutfitData();
        ResetOutfitState();
        ApplyBuffsForCurrentOutfit();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        ResetOutfitState();
    }

    private void ResetOutfitState()
    {
        _currentOutfitId = null;
        _pendingOutfitId = null;
        _pendingOutfitTicks = 0;
        _activeBuffIds.Clear();
        _activeRemoveBuffIds.Clear();
    }

    private void OnSpriteDirty(object? sender, EventArgs e)
    {
        if (!CanRun()) return;
        ApplyBuffsForCurrentOutfit();
    }

    private void OnOutfitPollTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!CanRun()) return;

        if (e.IsMultipleOf(OutfitPollIntervalTicks))
            ApplyBuffsForCurrentOutfit();
    }

    /// <summary>
    /// Apply protection before Cloudy Skies / PDW weather effects run so PLAYER_HAS_BUFF checks succeed.
    /// </summary>
    [EventPriority(EventPriority.High)]
    private void OnApplyProtectionBuffs(object? sender, UpdateTickedEventArgs e)
    {
        if (!CanRun()) return;

        var readOutfitId = ReadCurrentOutfitId();
        var protectionOutfitId = GetOutfitIdForProtection(readOutfitId);
        if (!TryGetMappedEntry(protectionOutfitId, out _, out var entry))
            return;

        EnsureProtectionBuffs(entry.BuffIds, logApply: false);
    }

    /// <summary>
    /// Strip configured weather debuffs after weather mods run, but only while protection is active.
    /// </summary>
    [EventPriority(EventPriority.Low)]
    private void OnStripWeatherDebuffs(object? sender, UpdateTickedEventArgs e)
    {
        if (!CanRun()) return;

        var readOutfitId = ReadCurrentOutfitId();
        var protectionOutfitId = GetOutfitIdForProtection(readOutfitId);
        if (!TryGetMappedEntry(protectionOutfitId, out _, out var entry))
            return;

        if (!HasAllProtectionBuffs(entry.BuffIds))
            return;

        StripConfiguredDebuffs(entry.RemoveBuffIds, logRemove: false);
    }

    private bool CanRun()
    {
        return _fsApi is not null && _config.Enabled && Context.IsPlayerFree;
    }

    private string? ReadCurrentOutfitId()
    {
        var result = _fsApi!.GetCurrentOutfitId();
        return result.Key ? result.Value : null;
    }

    private void ApplyBuffsForCurrentOutfit()
    {
        if (!CanRun()) return;

        var readOutfitId = ReadCurrentOutfitId();
        var outfitId = ResolveCommittedOutfitId(readOutfitId);

        if (outfitId == _currentOutfitId)
            return;

        CommitOutfitChange(outfitId);
    }

    /// <summary>
    /// Apply protection immediately when switching to a mapped outfit; debounce only when switching away.
    /// </summary>
    private string? GetOutfitIdForProtection(string? readOutfitId)
    {
        if (readOutfitId is not null && TryGetMappedEntry(readOutfitId, out _, out _))
            return readOutfitId;

        return ResolveCommittedOutfitId(readOutfitId);
    }

    /// <summary>
    /// Ignore brief outfit-id flicker from Fashion Sense so we do not drop protection buffs for a frame.
    /// </summary>
    private string? ResolveCommittedOutfitId(string? readOutfitId)
    {
        if (readOutfitId == _currentOutfitId)
        {
            _pendingOutfitId = readOutfitId;
            _pendingOutfitTicks = OutfitChangeStableTicks;
            return _currentOutfitId;
        }

        if (readOutfitId != _pendingOutfitId)
        {
            _pendingOutfitId = readOutfitId;
            _pendingOutfitTicks = 1;
            return _currentOutfitId;
        }

        _pendingOutfitTicks++;
        return _pendingOutfitTicks >= OutfitChangeStableTicks ? _pendingOutfitId : _currentOutfitId;
    }

    private void CommitOutfitChange(string? newOutfitId)
    {
        if (newOutfitId is null)
        {
            RemoveActiveBuffs();
            _activeRemoveBuffIds.Clear();
            _currentOutfitId = null;
            return;
        }

        if (!TryGetMappedEntry(newOutfitId, out var key, out var entry))
        {
            if (!string.Equals(_lastLoggedUnmappedOutfit, newOutfitId, StringComparison.OrdinalIgnoreCase))
            {
                Monitor.Log($"No buff mapping for outfit '{newOutfitId}'.", LogLevel.Debug);
                _lastLoggedUnmappedOutfit = newOutfitId;
            }

            RemoveActiveBuffs();
            _activeRemoveBuffIds.Clear();
            _currentOutfitId = newOutfitId;
            return;
        }

        _lastLoggedUnmappedOutfit = null;

        EnsureProtectionBuffs(entry.BuffIds, logApply: true, outfitId: newOutfitId, mappingKey: key);
        StripConfiguredDebuffs(entry.RemoveBuffIds, logRemove: true);

        foreach (var buffId in _activeBuffIds.ToList())
        {
            if (entry.BuffIds.Contains(buffId, StringComparer.OrdinalIgnoreCase))
                continue;

            Game1.player.buffs.Remove(buffId);
            Monitor.Log($"Removed buff '{buffId}' (outfit changed to '{newOutfitId}').", LogLevel.Trace);
        }

        _activeBuffIds.Clear();
        foreach (var buffId in entry.BuffIds)
        {
            if (BuffExists(buffId))
                _activeBuffIds.Add(buffId);
        }

        _activeRemoveBuffIds.Clear();
        _activeRemoveBuffIds.AddRange(entry.RemoveBuffIds);
        _currentOutfitId = newOutfitId;
    }

    private void EnsureProtectionBuffs(
        IReadOnlyList<string> buffIds,
        bool logApply,
        string? outfitId = null,
        string? mappingKey = null)
    {
        foreach (var buffId in buffIds)
        {
            if (!BuffExists(buffId))
            {
                if (logApply)
                    Monitor.Log($"Buff '{buffId}' not found in Data/Buffs.", LogLevel.Warn);
                continue;
            }

            if (Game1.player.hasBuff(buffId))
                continue;

            Game1.player.applyBuff(buffId);

            if (!logApply)
                continue;

            Monitor.Log(
                $"Applied buff '{buffId}' for outfit '{outfitId}' (mapping '{mappingKey}').",
                LogLevel.Trace);
        }
    }

    private static bool HasAllProtectionBuffs(IReadOnlyList<string> buffIds)
    {
        foreach (var buffId in buffIds)
        {
            if (!BuffExists(buffId))
                continue;

            if (!Game1.player.hasBuff(buffId))
                return false;
        }

        return buffIds.Any(BuffExists);
    }

    private void StripConfiguredDebuffs(IReadOnlyList<string> buffIds, bool logRemove)
    {
        if (buffIds.Count == 0) return;

        foreach (var buffId in buffIds)
        {
            if (!Game1.player.hasBuff(buffId))
                continue;

            Game1.player.buffs.Remove(buffId);

            if (logRemove)
                Monitor.Log($"Removed buff '{buffId}' for outfit '{_currentOutfitId}'.", LogLevel.Trace);
        }
    }

    private bool TryGetMappedEntry(
        string? outfitId,
        out string matchedKey,
        out OutfitBuffEntry entry)
    {
        matchedKey = null!;
        entry = null!;

        if (outfitId is null)
            return false;

        return OutfitMappingResolver.TryResolve(outfitId, _outfitData, out matchedKey, out entry);
    }

    private static bool BuffExists(string buffId)
    {
        return DataLoader.Buffs(Game1.content).ContainsKey(buffId);
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
                if (!v)
                {
                    RemoveActiveBuffs();
                    _activeRemoveBuffIds.Clear();
                }
            },
            name: () => "Enable outfit buffs",
            tooltip: () => "When enabled, wearing a mapped Fashion Sense outfit automatically applies its buffs or removes debuffs."
        );

        gmcm.AddSectionTitle(ModManifest, () => "Active Outfit Mappings");

        gmcm.AddTable(
            mod: ModManifest,
            getHeaders: () => new[] { "Outfit ID", "Apply Buffs", "Remove Buffs" },
            getRows: () => _outfitData
                .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => new[]
                {
                    kvp.Key,
                    string.Join(", ", kvp.Value.BuffIds),
                    kvp.Value.RemoveBuffIds.Count > 0
                        ? string.Join(", ", kvp.Value.RemoveBuffIds)
                        : ""
                })
                .ToList(),
            emptyCellText: "—",
            getEmptyMessage: () =>
                "No mappings loaded. Note: Outfit mapping won't show up here until a save is first loaded!"
        );
    }
}
