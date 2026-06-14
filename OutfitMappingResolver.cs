using System.Text.RegularExpressions;

namespace FashionSenseBuffs;

/// <summary>
/// Resolves Fashion Sense outfit names to buff mappings, including Fern/ESWF seasonal and indoor variants.
/// </summary>
internal static class OutfitMappingResolver
{
    private static readonly (string Pattern, string Replacement)[] NameNormalizations =
    {
        ("Heatwaves", "Heat Wave"),
        ("Hailstorm", "Hail"),
        ("Muddy Rain", "Mud Rain"),
    };

    /// <summary>
    /// Finds a mapping for <paramref name="outfitId"/> using exact match, indoor stripping,
    /// Fern preset name normalizations, and longest-prefix fallback (e.g. "Acid Rain Spring" → "Acid Rain").
    /// </summary>
    public static bool TryResolve(
        string outfitId,
        IReadOnlyDictionary<string, OutfitBuffEntry> mappings,
        out string matchedKey,
        out OutfitBuffEntry entry)
    {
        matchedKey = null!;
        entry = null!;

        if (string.IsNullOrWhiteSpace(outfitId) || mappings.Count == 0)
            return false;

        foreach (var candidate in GetCandidates(outfitId))
        {
            var exactKey = mappings.Keys.FirstOrDefault(k =>
                string.Equals(k, candidate, StringComparison.OrdinalIgnoreCase));
            if (exactKey is not null)
            {
                matchedKey = exactKey;
                entry = mappings[exactKey];
                return true;
            }

            var prefixKey = mappings.Keys
                .Where(k =>
                    candidate.Length > k.Length
                    && candidate.StartsWith(k, StringComparison.OrdinalIgnoreCase)
                    && candidate[k.Length] == ' ')
                .MaxBy(k => k.Length);

            if (prefixKey is not null)
            {
                matchedKey = prefixKey;
                entry = mappings[prefixKey];
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> GetCandidates(string outfitId)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in EnumerateCandidates(outfitId))
        {
            if (seen.Add(candidate))
                yield return candidate;
        }
    }

    private static IEnumerable<string> EnumerateCandidates(string outfitId)
    {
        yield return outfitId;

        if (TryStripIndoorSuffix(outfitId, out var withoutIndoor))
            yield return withoutIndoor;

        var normalized = NormalizeName(outfitId);
        if (!string.Equals(normalized, outfitId, StringComparison.OrdinalIgnoreCase))
        {
            yield return normalized;

            if (TryStripIndoorSuffix(normalized, out var normalizedWithoutIndoor))
                yield return normalizedWithoutIndoor;
        }
    }

    private static string NormalizeName(string outfitId)
    {
        var result = outfitId;
        foreach (var (pattern, replacement) in NameNormalizations)
            result = Regex.Replace(result, pattern, replacement, RegexOptions.IgnoreCase);
        return result;
    }

    private static bool TryStripIndoorSuffix(string outfitId, out string stripped)
    {
        const string suffix = " Indoor";
        if (outfitId.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            && outfitId.Length > suffix.Length)
        {
            stripped = outfitId[..^suffix.Length];
            return true;
        }

        stripped = outfitId;
        return false;
    }
}
