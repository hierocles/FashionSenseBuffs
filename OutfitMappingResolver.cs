using StardewModdingAPI;

namespace FashionSenseBuffs;

/// <summary>
/// Builds a flat outfit-name lookup from primary keys and explicit <see cref="OutfitBuffEntry.Aliases"/>.
/// </summary>
internal static class OutfitMappingResolver
{
    public readonly record struct OutfitLookupEntry(string MatchedKey, OutfitBuffEntry Entry);

    /// <summary>
    /// Indexes each primary key and alias (case-insensitive). Duplicate names log a debug warning; last wins.
    /// </summary>
    public static Dictionary<string, OutfitLookupEntry> BuildLookup(
        IReadOnlyDictionary<string, OutfitBuffEntry> mappings,
        IMonitor? monitor = null)
    {
        var lookup = new Dictionary<string, OutfitLookupEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, entry) in mappings)
            RegisterName(lookup, key, key, entry, monitor);

        foreach (var (key, entry) in mappings)
        {
            foreach (var alias in entry.Aliases)
            {
                if (string.IsNullOrWhiteSpace(alias))
                    continue;

                RegisterName(lookup, alias, key, entry, monitor);
            }
        }

        return lookup;
    }

    /// <summary>Resolves <paramref name="outfitId"/> by exact name match against the lookup.</summary>
    public static bool TryResolve(
        string outfitId,
        IReadOnlyDictionary<string, OutfitLookupEntry> lookup,
        out string matchedKey,
        out OutfitBuffEntry entry)
    {
        matchedKey = null!;
        entry = null!;

        if (string.IsNullOrWhiteSpace(outfitId) || lookup.Count == 0)
            return false;

        if (!lookup.TryGetValue(outfitId, out var match))
            return false;

        matchedKey = match.MatchedKey;
        entry = match.Entry;
        return true;
    }

    private static void RegisterName(
        Dictionary<string, OutfitLookupEntry> lookup,
        string name,
        string matchedKey,
        OutfitBuffEntry entry,
        IMonitor? monitor)
    {
        if (lookup.TryGetValue(name, out var existing)
            && !string.Equals(existing.MatchedKey, matchedKey, StringComparison.OrdinalIgnoreCase))
        {
            monitor?.Log(
                $"Duplicate outfit mapping for '{name}' ('{existing.MatchedKey}' vs '{matchedKey}'); using '{matchedKey}'.",
                LogLevel.Debug);
        }

        lookup[name] = new OutfitLookupEntry(matchedKey, entry);
    }
}
