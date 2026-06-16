using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FashionSenseBuffs;

/// <summary>Draws read-only tables for GMCM complex options.</summary>
internal static class GmcmTableRenderer
{
    private const int CellPadding = 6;
    private const int BorderWidth = 1;

    private static readonly Color HeaderBackground = Color.Black * 0.15f;
    private static readonly Color RowAltBackground = Color.Black * 0.05f;
    private static readonly Color BorderColor = new(80, 50, 30);

    public static int TableWidth => Math.Min(1200, Game1.uiViewport.Width - 200);

    public static int MeasureHeight(GmcmTableData table)
    {
        var font = Game1.smallFont;
        var columnWidths = GetColumnWidths(table);
        var height = MeasureRowHeight(font, table.Headers, columnWidths, bold: true);

        if (table.Rows.Count == 0)
        {
            var message = table.EmptyMessage ?? "No data.";
            height += (int)(MeasureWrappedHeight(font, message, TableWidth - CellPadding * 2) + CellPadding * 2);
            return height;
        }

        for (var i = 0; i < table.Rows.Count; i++)
            height += MeasureDataRowHeight(font, table.Rows[i], columnWidths, table.EmptyCellText);

        return height;
    }

    public static void Draw(SpriteBatch b, Vector2 position, GmcmTableData table)
    {
        var tableWidth = TableWidth;
        var x = (int)position.X - tableWidth / 2;
        var y = (int)position.Y;
        var font = Game1.smallFont;
        var columnWidths = GetColumnWidths(table);

        var headerHeight = MeasureRowHeight(font, table.Headers, columnWidths, bold: true);
        DrawRowBackground(b, x, y, tableWidth, headerHeight, HeaderBackground);
        DrawRowCells(b, font, x, y, table.Headers, columnWidths, bold: true);
        DrawRowBorders(b, x, y, tableWidth, headerHeight, columnWidths);
        y += headerHeight;

        if (table.Rows.Count == 0)
        {
            var message = table.EmptyMessage ?? "No data.";
            var rowHeight = (int)(MeasureWrappedHeight(font, message, tableWidth - CellPadding * 2) + CellPadding * 2);
            DrawWrappedText(b, font, message, new Vector2(x + CellPadding, y + CellPadding), tableWidth - CellPadding * 2, Game1.textColor);
            DrawOuterBorder(b, x, y, tableWidth, rowHeight);
            return;
        }

        for (var i = 0; i < table.Rows.Count; i++)
        {
            var cells = NormalizeRow(table.Rows[i], table.Headers.Length, table.EmptyCellText);
            var rowHeight = MeasureDataRowHeight(font, cells, columnWidths, table.EmptyCellText);

            if (i % 2 == 1)
                DrawRowBackground(b, x, y, tableWidth, rowHeight, RowAltBackground);

            DrawRowCells(b, font, x, y, cells, columnWidths);
            DrawRowBorders(b, x, y, tableWidth, rowHeight, columnWidths);
            y += rowHeight;
        }
    }

    private static int[] GetColumnWidths(GmcmTableData table)
    {
        var fractions = table.ColumnWidthFractions;
        var innerWidth = TableWidth - BorderWidth * (fractions.Length + 1);
        return fractions.Select(f => (int)(innerWidth * f)).ToArray();
    }

    private static string[] NormalizeRow(string[] row, int columnCount, string emptyCellText)
    {
        var cells = new string[columnCount];
        for (var i = 0; i < columnCount; i++)
        {
            var value = i < row.Length ? row[i] : "";
            cells[i] = string.IsNullOrWhiteSpace(value) ? emptyCellText : value;
        }

        return cells;
    }

    private static int MeasureDataRowHeight(SpriteFont font, string[] cells, int[] columnWidths, string emptyCellText)
    {
        var normalized = NormalizeRow(cells, columnWidths.Length, emptyCellText);
        return MeasureRowHeight(font, normalized, columnWidths);
    }

    private static int MeasureRowHeight(SpriteFont font, string[] cells, int[] columnWidths, bool bold = false)
    {
        var maxHeight = 0f;
        for (var i = 0; i < cells.Length && i < columnWidths.Length; i++)
        {
            var wrapWidth = columnWidths[i] - CellPadding * 2;
            var cellHeight = MeasureWrappedHeight(font, cells[i], wrapWidth);
            maxHeight = Math.Max(maxHeight, cellHeight);
        }

        return (int)maxHeight + CellPadding * 2;
    }

    private static float MeasureWrappedHeight(SpriteFont font, string text, int width)
    {
        var parsed = Game1.parseText(text, font, width);
        var height = 0f;
        foreach (var line in parsed.Split('\n'))
            height += font.MeasureString(line).Y;

        return height;
    }

    private static void DrawRowCells(
        SpriteBatch b,
        SpriteFont font,
        int x,
        int y,
        string[] cells,
        int[] columnWidths,
        bool bold = false)
    {
        var cellX = x + BorderWidth;
        for (var i = 0; i < cells.Length && i < columnWidths.Length; i++)
        {
            var wrapWidth = columnWidths[i] - CellPadding * 2;
            DrawWrappedText(
                b,
                font,
                cells[i],
                new Vector2(cellX + CellPadding, y + CellPadding),
                wrapWidth,
                Game1.textColor,
                bold);
            cellX += columnWidths[i] + BorderWidth;
        }
    }

    private static void DrawWrappedText(
        SpriteBatch b,
        SpriteFont font,
        string text,
        Vector2 pos,
        int width,
        Color color,
        bool bold = false)
    {
        var parsed = Game1.parseText(text, font, width);
        var y = pos.Y;
        foreach (var line in parsed.Split('\n'))
        {
            if (bold)
                Utility.drawTextWithShadow(b, line, font, new Vector2(pos.X, y), color);
            else
                b.DrawString(font, line, new Vector2(pos.X, y), color);

            y += font.MeasureString(line).Y;
        }
    }

    private static void DrawRowBackground(SpriteBatch b, int x, int y, int width, int height, Color color)
    {
        b.Draw(Game1.staminaRect, new Rectangle(x, y, width, height), color);
    }

    private static void DrawRowBorders(SpriteBatch b, int x, int y, int width, int height, int[] columnWidths)
    {
        DrawOuterBorder(b, x, y, width, height);

        var cellX = x + BorderWidth;
        for (var i = 0; i < columnWidths.Length - 1; i++)
        {
            cellX += columnWidths[i];
            b.Draw(Game1.staminaRect, new Rectangle(cellX, y, BorderWidth, height), BorderColor);
            cellX += BorderWidth;
        }
    }

    private static void DrawOuterBorder(SpriteBatch b, int x, int y, int width, int height)
    {
        b.Draw(Game1.staminaRect, new Rectangle(x, y, width, BorderWidth), BorderColor);
        b.Draw(Game1.staminaRect, new Rectangle(x, y + height - BorderWidth, width, BorderWidth), BorderColor);
        b.Draw(Game1.staminaRect, new Rectangle(x, y, BorderWidth, height), BorderColor);
        b.Draw(Game1.staminaRect, new Rectangle(x + width - BorderWidth, y, BorderWidth, height), BorderColor);
    }
}

/// <summary>Snapshot of table content passed to <see cref="GmcmTableRenderer"/>.</summary>
internal sealed class GmcmTableData
{
    public string[] Headers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string[]> Rows { get; init; } = Array.Empty<string[]>();
    public float[] ColumnWidthFractions { get; init; } = Array.Empty<float>();
    public string EmptyCellText { get; init; } = "—";
    public string? EmptyMessage { get; init; }
}
