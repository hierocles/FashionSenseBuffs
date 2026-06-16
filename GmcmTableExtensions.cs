using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace FashionSenseBuffs;
/// <summary>GMCM helpers for rendering tabular data.</summary>
public static class GmcmTableExtensions
{
    private static readonly float[] DefaultColumnFractions = { 0.35f, 0.40f, 0.25f };

    /// <summary>Add a read-only table at the current position in a GMCM form.</summary>
    public static void AddTable(
        this IGenericModConfigMenuApi gmcm,
        IManifest mod,
        Func<string[]> getHeaders,
        Func<IReadOnlyList<string[]>> getRows,
        Func<string>? name = null,
        Func<string>? tooltip = null,
        float[]? columnWidthFractions = null,
        string emptyCellText = "—",
        Func<string>? getEmptyMessage = null,
        string? fieldId = null)
    {
        GmcmTableData BuildTable() => new()
        {
            Headers = getHeaders(),
            Rows = getRows(),
            ColumnWidthFractions = columnWidthFractions ?? DefaultColumnFractions,
            EmptyCellText = emptyCellText,
            EmptyMessage = getEmptyMessage?.Invoke(),
        };

        gmcm.AddComplexOption(
            mod: mod,
            name: name ?? (() => ""),
            draw: (b, pos) => GmcmTableRenderer.Draw(b, pos, BuildTable()),
            tooltip: tooltip,
            height: () => GmcmTableRenderer.MeasureHeight(BuildTable()),
            fieldId: fieldId
        );
    }
}
