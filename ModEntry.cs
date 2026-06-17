using GenericModConfigMenu;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace FashionSenseBuffs;

public class ModEntry : Mod
{
    /// <summary>Asset path exposed for Content Patcher (and other mods) to patch.</summary>
    internal const string AssetPath = "hierocles.FashionSenseBuffs/Outfits";

    /// <summary>GMCM sub-page for per-mapping Content Patcher overrides.</summary>
    private const string GmcmOverridesPageId = "overrides";

    private IFashionSenseApi? _fsApi;
    private IGenericModConfigMenuApi? _gmcmApi;
    private ModConfig _config = new();
    private Dictionary<string, OutfitBuffEntry> _outfitData = new();
    private Dictionary<string, OutfitMappingResolver.OutfitLookupEntry> _outfitLookup = new(StringComparer.OrdinalIgnoreCase);
    private string? _currentOutfitId;
    private string? _pendingOutfitId;
    private int _pendingOutfitTicks;
    private readonly List<string> _activeBuffIds = new();
    private readonly List<string> _activeRemoveBuffIds = new();
    private string? _lastLoggedUnmappedOutfit;
    private string? _lastMappedOutfitId;
    private bool? _lastWasOutdoors;
    private readonly HashSet<string> _gmcmRegisteredOutdoorsOnlyKeys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Consecutive reads required before switching away from the current outfit.</summary>
    private const int OutfitChangeStableTicks = 3;

    /// <summary>How often to poll Fashion Sense for outfit-id changes.</summary>
    private const int OutfitPollIntervalTicks = 15;

    private string T(string key) => Helper.Translation.Get(key);

    private string T(string key, object tokens) => Helper.Translation.Get(key, tokens);

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
        helper.Events.Player.Warped += OnPlayerWarped;
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
        ReconcileBuffState(force: true, silent: true);
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        ResetOutfitState();
    }

    private void OnPlayerWarped(object? sender, WarpedEventArgs e)
    {
        if (!e.IsLocalPlayer) return;
        ReconcileBuffState(force: true, isOutdoors: e.NewLocation?.IsOutdoors == true, notifyChanges: true);
    }

    private void ResetOutfitState()
    {
        _currentOutfitId = null;
        _pendingOutfitId = null;
        _pendingOutfitTicks = 0;
        _activeBuffIds.Clear();
        _activeRemoveBuffIds.Clear();
        _lastWasOutdoors = null;
        _lastMappedOutfitId = null;
    }

    private void OnSpriteDirty(object? sender, EventArgs e)
    {
        if (!CanMaintainBuffs()) return;
        ReconcileBuffState();
    }

    private void OnOutfitPollTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!CanDetectOutfitChange()) return;

        if (e.IsMultipleOf(OutfitPollIntervalTicks))
            ReconcileBuffState();
    }

    /// <summary>
    /// Apply protection before Cloudy Skies / PDW weather effects run so PLAYER_HAS_BUFF checks succeed.
    /// </summary>
    [EventPriority(EventPriority.High)]
    private void OnApplyProtectionBuffs(object? sender, UpdateTickedEventArgs e)
    {
        ReconcileBuffState(stripDebuffs: false);
    }

    /// <summary>
    /// Strip configured weather debuffs after weather mods run, but only while protection is active.
    /// </summary>
    [EventPriority(EventPriority.Low)]
    private void OnStripWeatherDebuffs(object? sender, UpdateTickedEventArgs e)
    {
        ReconcileBuffState(stripDebuffs: true);
    }

    private bool CanDetectOutfitChange()
    {
        return _fsApi is not null && _config.Enabled && Context.IsPlayerFree;
    }

    private bool CanMaintainBuffs()
    {
        return _fsApi is not null && _config.Enabled && Context.IsWorldReady && Game1.player is not null;
    }

    private string? ReadCurrentOutfitId()
    {
        var result = _fsApi!.GetCurrentOutfitId();
        return result.Key ? result.Value : null;
    }

    /// <summary>
    /// Apply protection immediately when switching to a mapped outfit; debounce only when switching away.
    /// </summary>
    private string? ResolveEffectiveOutfitId(string? readOutfitId)
    {
        if (readOutfitId is not null && TryGetMappedEntry(readOutfitId, out _, out _))
            return readOutfitId;

        var debounced = ResolveCommittedOutfitId(readOutfitId);
        if (debounced is not null && TryGetMappedEntry(debounced, out _, out _))
            return debounced;

        if (_lastMappedOutfitId is not null && TryGetMappedEntry(_lastMappedOutfitId, out _, out _))
            return _lastMappedOutfitId;

        return debounced;
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

    private void ReconcileBuffState(bool force = false, bool stripDebuffs = false, bool? isOutdoors = null, bool notifyChanges = false, bool silent = false)
    {
        if (!CanMaintainBuffs()) return;

        var outdoors = isOutdoors ?? (Game1.currentLocation?.IsOutdoors == true);
        if (_lastWasOutdoors is null)
            _lastWasOutdoors = outdoors;
        else if (_lastWasOutdoors != outdoors)
        {
            _lastWasOutdoors = outdoors;
            force = true;
            notifyChanges = true;
        }

        var readOutfitId = ReadCurrentOutfitId();
        var effectiveOutfitId = ResolveEffectiveOutfitId(readOutfitId);

        if (TryGetMappedEntry(effectiveOutfitId, out var syncKey, out var syncEntry)
            && IsLocationEligible(syncKey, syncEntry, outdoors)
            && IsMissingRequiredProtectionBuffs(syncEntry))
        {
            force = true;
        }

        var outfitIdChanged = !string.Equals(effectiveOutfitId, _currentOutfitId, StringComparison.Ordinal);
        var outfitChanged = force || outfitIdChanged;

        if (outfitChanged)
        {
            var shouldNotify = notifyChanges || (outfitIdChanged && !silent);
            ApplyOutfitBuffState(effectiveOutfitId, logChanges: shouldNotify, notifyChanges: shouldNotify, isOutdoors: outdoors);
            return;
        }

        if (!TryGetMappedEntry(effectiveOutfitId, out var key, out var entry))
            return;

        if (!IsLocationEligible(key, entry, outdoors))
        {
            if (_activeBuffIds.Count > 0)
            {
                RemoveActiveBuffs(notify: notifyChanges);
                _activeRemoveBuffIds.Clear();
            }

            return;
        }

        EnsureProtectionBuffs(entry.BuffIds, logApply: false);

        if (stripDebuffs && HasAllProtectionBuffs(entry.BuffIds))
            StripConfiguredDebuffs(entry.RemoveBuffIds, logRemove: false);
    }

    private static bool IsMissingRequiredProtectionBuffs(OutfitBuffEntry entry)
    {
        foreach (var buffId in entry.BuffIds)
        {
            if (!BuffExists(buffId))
                continue;

            if (!Game1.player.hasBuff(buffId))
                return true;
        }

        return false;
    }

    private void ApplyOutfitBuffState(string? newOutfitId, bool logChanges, bool notifyChanges = false, bool? isOutdoors = null)
    {
        var notify = notifyChanges;
        if (newOutfitId is null)
        {
            RemoveActiveBuffs(notify: notify);
            _activeRemoveBuffIds.Clear();
            _currentOutfitId = null;
            return;
        }

        if (!TryGetMappedEntry(newOutfitId, out var key, out var entry))
        {
            if (logChanges
                && !string.Equals(_lastLoggedUnmappedOutfit, newOutfitId, StringComparison.OrdinalIgnoreCase))
            {
                Monitor.Log($"No buff mapping for outfit '{newOutfitId}'.", LogLevel.Debug);
                _lastLoggedUnmappedOutfit = newOutfitId;
            }

            RemoveActiveBuffs(notify: notify);
            _activeRemoveBuffIds.Clear();
            _currentOutfitId = newOutfitId;
            return;
        }

        _lastLoggedUnmappedOutfit = null;

        if (!IsLocationEligible(key, entry, isOutdoors))
        {
            RemoveActiveBuffs(notify: notify);
            _activeRemoveBuffIds.Clear();
            _currentOutfitId = newOutfitId;
            return;
        }

        EnsureProtectionBuffs(entry.BuffIds, logApply: logChanges, notifyApply: notify, outfitId: newOutfitId, mappingKey: key);

        if (HasAllProtectionBuffs(entry.BuffIds))
            StripConfiguredDebuffs(entry.RemoveBuffIds, logRemove: logChanges, notifyRemove: notify);

        foreach (var buffId in _activeBuffIds.ToList())
        {
            if (entry.BuffIds.Contains(buffId, StringComparer.OrdinalIgnoreCase))
                continue;

            Game1.player.buffs.Remove(buffId);
            if (logChanges)
                Monitor.Log($"Removed buff '{buffId}' (outfit changed to '{newOutfitId}').", LogLevel.Trace);
            if (notify)
                ShowBuffRemovedNotification(buffId);
        }

        _activeBuffIds.Clear();
        foreach (var buffId in entry.BuffIds)
        {
            if (BuffExists(buffId) && Game1.player.hasBuff(buffId))
                _activeBuffIds.Add(buffId);
        }

        _activeRemoveBuffIds.Clear();
        _activeRemoveBuffIds.AddRange(entry.RemoveBuffIds);
        _currentOutfitId = newOutfitId;
        _lastMappedOutfitId = newOutfitId;
    }

    /// <summary>
    /// Resolves outdoors-only for a mapping: GMCM override wins, then Content Patcher default.
    /// Future override types: follow this same GetEffective* pattern.
    /// </summary>
    private bool GetEffectiveOutdoorsOnly(string mappingKey, OutfitBuffEntry entry)
    {
        if (_config.OutdoorsOnlyOverrides.TryGetValue(mappingKey, out var overrideValue))
            return overrideValue;

        return entry.OutdoorsOnly;
    }

    private bool IsLocationEligible(string mappingKey, OutfitBuffEntry entry, bool? isOutdoors = null)
    {
        if (!GetEffectiveOutdoorsOnly(mappingKey, entry))
            return true;

        return isOutdoors ?? (Game1.currentLocation?.IsOutdoors == true);
    }

    private void EnsureProtectionBuffs(
        IReadOnlyList<string> buffIds,
        bool logApply,
        bool notifyApply = false,
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

            if (logApply)
            {
                Monitor.Log(
                    $"Applied buff '{buffId}' for outfit '{outfitId}' (mapping '{mappingKey}').",
                    LogLevel.Trace);
            }

            if (notifyApply)
                ShowBuffAddedNotification(buffId);
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

    private void StripConfiguredDebuffs(IReadOnlyList<string> buffIds, bool logRemove, bool notifyRemove = false)
    {
        if (buffIds.Count == 0) return;

        foreach (var buffId in buffIds)
        {
            if (!Game1.player.hasBuff(buffId))
                continue;

            Game1.player.buffs.Remove(buffId);

            if (logRemove)
                Monitor.Log($"Removed buff '{buffId}' for outfit '{_currentOutfitId}'.", LogLevel.Trace);
            if (notifyRemove)
                ShowBuffRemovedNotification(buffId);
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

        return OutfitMappingResolver.TryResolve(outfitId, _outfitLookup, out matchedKey, out entry);
    }

    private static bool BuffExists(string buffId)
    {
        return DataLoader.Buffs(Game1.content).ContainsKey(buffId);
    }

    private void RemoveActiveBuffs(bool notify = false)
    {
        if (notify)
        {
            foreach (var buffId in _activeBuffIds)
                ShowBuffRemovedNotification(buffId);
        }

        foreach (var buffId in _activeBuffIds)
            Game1.player.buffs.Remove(buffId);
        _activeBuffIds.Clear();
    }

    private void ShowBuffAddedNotification(string buffId)
    {
        if (!_config.ShowBuffNotifications || !Context.IsWorldReady || Game1.player is null)
            return;

        Game1.addHUDMessage(new HUDMessage(T("notification.buff-added", new { buffName = GetBuffDisplayName(buffId) }))
        {
            noIcon = true,
            timeLeft = 4000f
        });
    }

    private void ShowBuffRemovedNotification(string buffId)
    {
        if (!_config.ShowBuffNotifications || !Context.IsWorldReady || Game1.player is null)
            return;

        Game1.addHUDMessage(new HUDMessage(T("notification.buff-removed", new { buffName = GetBuffDisplayName(buffId) }))
        {
            noIcon = true,
            timeLeft = 4000f
        });
    }

    private static string GetBuffDisplayName(string buffId)
    {
        if (!DataLoader.Buffs(Game1.content).TryGetValue(buffId, out var buffData))
            return buffId;

        return string.IsNullOrWhiteSpace(buffData.DisplayName) ? buffId : buffData.DisplayName;
    }

    private void LoadOutfitData()
    {
        _outfitData = Helper.GameContent.Load<Dictionary<string, OutfitBuffEntry>>(AssetPath);
        _outfitLookup = OutfitMappingResolver.BuildLookup(_outfitData, Monitor);
        Monitor.Log(
            $"Loaded {_outfitData.Count} outfit-buff mapping(s) ({_outfitLookup.Count} lookup name(s)) from {AssetPath}.",
            LogLevel.Debug);

        PruneOutdoorsOnlyOverrides();
        RefreshGmcmOverrides();
    }

    /// <summary>
    /// Register GMCM options for any new per-mapping overrides after outfit data loads.
    /// Future override types: add a Register*OverrideOptions() call here.
    /// </summary>
    private void RefreshGmcmOverrides()
    {
        RegisterOutdoorsOnlyOverrideOptions();
    }

    /// <summary>Remove outdoors-only overrides for mappings that no longer exist in loaded data.</summary>
    private void PruneOutdoorsOnlyOverrides()
    {
        foreach (var key in _config.OutdoorsOnlyOverrides.Keys.ToList())
        {
            if (!_outfitData.ContainsKey(key))
                _config.OutdoorsOnlyOverrides.Remove(key);
        }
    }

    private void RegisterOutdoorsOnlyOverrideOptions()
    {
        if (_gmcmApi is null || _outfitData.Count == 0) return;

        // Switch GMCM context to the shared overrides page before adding options.
        _gmcmApi.AddPage(ModManifest, GmcmOverridesPageId, () => T("config.page.overrides"));

        foreach (var mappingKey in _outfitData.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            if (!_gmcmRegisteredOutdoorsOnlyKeys.Add(mappingKey))
                continue;

            _gmcmApi.AddBoolOption(
                mod: ModManifest,
                getValue: () => GetEffectiveOutdoorsOnlyForMapping(mappingKey),
                setValue: value => SetOutdoorsOnlyOverride(mappingKey, value),
                name: () => mappingKey,
                tooltip: () => T("config.overrides.outdoors-only.tooltip", new { mappingKey }),
                fieldId: $"OutdoorsOnlyOverride.{mappingKey}"
            );
        }
    }

    private bool GetEffectiveOutdoorsOnlyForMapping(string mappingKey)
    {
        if (!_outfitData.TryGetValue(mappingKey, out var entry))
            return false;

        return GetEffectiveOutdoorsOnly(mappingKey, entry);
    }

    private void SetOutdoorsOnlyOverride(string mappingKey, bool value)
    {
        _config.OutdoorsOnlyOverrides[mappingKey] = value;
        ReconcileBuffState(force: true, notifyChanges: true);
    }

    private void RegisterGmcm()
    {
        var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null) return;

        _gmcmApi = gmcm;

        gmcm.Register(
            mod: ModManifest,
            reset: () =>
            {
                _config = new ModConfig();
                ReconcileBuffState(force: true, silent: true);
            },
            save: () => Helper.WriteConfig(_config)
        );

        gmcm.AddSectionTitle(ModManifest, () => T("config.section.settings"));

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
            name: () => T("config.enabled.name"),
            tooltip: () => T("config.enabled.tooltip")
        );

        gmcm.AddBoolOption(
            mod: ModManifest,
            getValue: () => _config.ShowBuffNotifications,
            setValue: v => _config.ShowBuffNotifications = v,
            name: () => T("config.show-notifications.name"),
            tooltip: () => T("config.show-notifications.tooltip")
        );

        gmcm.AddPageLink(
            mod: ModManifest,
            pageId: GmcmOverridesPageId,
            text: () => T("config.page.overrides"),
            tooltip: () => T("config.page.overrides.link-tooltip")
        );

        gmcm.AddSectionTitle(ModManifest, () => T("config.section.mappings"));

        gmcm.AddTable(
            mod: ModManifest,
            getHeaders: () => new[]
            {
                T("config.table.header.outfit"),
                T("config.table.header.buffs"),
                T("config.table.header.remove-buffs"),
                T("config.table.header.outdoors-only"),
                T("config.table.header.aliases")
            },
            getRows: () => _outfitData
                .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => new[]
                {
                    kvp.Key,
                    string.Join(", ", kvp.Value.BuffIds),
                    kvp.Value.RemoveBuffIds.Count > 0
                        ? string.Join(", ", kvp.Value.RemoveBuffIds)
                        : "",
                    GetEffectiveOutdoorsOnly(kvp.Key, kvp.Value)
                        ? T("config.table.yes")
                        : T("config.table.no"),
                    kvp.Value.Aliases.Count > 0
                        ? string.Join(", ", kvp.Value.Aliases)
                        : ""
                })
                .ToList(),
            columnWidthFractions: new[] { 0.22f, 0.24f, 0.18f, 0.12f, 0.24f },
            getEmptyCellText: () => T("config.table.empty-cell"),
            getEmptyMessage: () => T("config.table.empty-message")
        );

        gmcm.AddPage(ModManifest, GmcmOverridesPageId, () => T("config.page.overrides"));

        // Outdoors-only section (first override type). Future types: AddSectionTitle here once at
        // registration, then register per-mapping options from a dedicated Register*OverrideOptions()
        // method called by RefreshGmcmOverrides(). Use a per-type _gmcmRegistered*Keys set so new
        // mappings can be added on save load without duplicating section titles or options.
        gmcm.AddSectionTitle(ModManifest, () => T("config.overrides.outdoors-only.section-title"));
    }
}
