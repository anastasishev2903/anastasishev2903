#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

/// <summary>
/// Writes a minimal .xlsx workbook with one worksheet formatted as an Excel Table
/// (filter arrows on open). Cells use dark fills with orange text.
/// Long values (e.g. naming JSON) go through sharedStrings and are capped at Excel's
/// 32,767-character cell limit so they are not dropped on open.
/// </summary>
static class CloudSaveExcelTableExporter
{
    // OOXML colors are AARRGGBB.
    const string TextOrangeRgb = "FFFF8C00";
    const string HeaderFillRgb = "FF111111";
    const string RowFillRgb = "FF1E1E1E";
    const string RowStripeFillRgb = "FF2A2A2A";

    // Excel hard limit per cell / shared-string item.
    const int ExcelMaxCellChars = 32767;
    const string TruncatedSuffix = "…[truncated]";

    // Excel row height is in points (default Calibri 11 ≈ 15).
    const double DefaultRowHeight = 22;
    const double HeaderRowHeight = 24;

    const int StyleDefault = 0;
    const int StyleHeader = 1;
    const int StyleRow = 2;
    const int StyleRowStripe = 3;

    public static void WriteTable(
        string path,
        IReadOnlyList<string> columnKeys,
        IReadOnlyList<Dictionary<string, string>> rows,
        Func<Dictionary<string, string>, string, string> getDisplayValue)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));
        if (columnKeys == null || columnKeys.Count == 0)
            throw new ArgumentException("At least one column is required.", nameof(columnKeys));
        if (rows == null)
            throw new ArgumentNullException(nameof(rows));
        if (getDisplayValue == null)
            throw new ArgumentNullException(nameof(getDisplayValue));

        var headers = MakeUniqueColumnNames(columnKeys);
        var lastCol = ToColumnName(headers.Count);
        var dataRowCount = Math.Max(rows.Count, 1);
        var lastRow = 1 + dataRowCount;
        var tableRef = $"A1:{lastCol}{lastRow}";

        var sharedStrings = new SharedStringTable();
        var sheetXml = BuildSheetXml(
            columnKeys,
            headers,
            rows,
            getDisplayValue,
            lastCol,
            lastRow,
            sharedStrings);
        var sharedStringsXml = sharedStrings.BuildXml();
        var tableXml = BuildTableXml(headers, tableRef);

        if (File.Exists(path))
            File.Delete(path);

        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(fileStream, ZipArchiveMode.Create);
        WriteEntry(zip, "[Content_Types].xml", BuildContentTypesXml());
        WriteEntry(zip, "_rels/.rels", BuildRootRelsXml());
        WriteEntry(zip, "xl/workbook.xml", BuildWorkbookXml());
        WriteEntry(zip, "xl/_rels/workbook.xml.rels", BuildWorkbookRelsXml());
        WriteEntry(zip, "xl/styles.xml", BuildStylesXml());
        WriteEntry(zip, "xl/sharedStrings.xml", sharedStringsXml);
        WriteEntry(zip, "xl/worksheets/sheet1.xml", sheetXml);
        WriteEntry(zip, "xl/worksheets/_rels/sheet1.xml.rels", BuildSheetRelsXml());
        WriteEntry(zip, "xl/tables/table1.xml", tableXml);
    }

    static void WriteEntry(ZipArchive zip, string entryName, string xml)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(xml);
    }

    static List<string> MakeUniqueColumnNames(IReadOnlyList<string> columns)
    {
        var result = new List<string>(columns.Count);
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < columns.Count; i++)
        {
            var name = string.IsNullOrWhiteSpace(columns[i]) ? $"Column{i + 1}" : SanitizeHeader(columns[i].Trim());

            if (!seen.TryGetValue(name, out var count))
            {
                seen[name] = 1;
                result.Add(name);
            }
            else
            {
                count++;
                seen[name] = count;
                result.Add($"{name}_{count}");
            }
        }

        return result;
    }

    static string SanitizeHeader(string name)
    {
        var sb = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c == '\r' || c == '\n' || c == '\t')
                sb.Append(' ');
            else
                sb.Append(c);
        }

        return sb.ToString();
    }

    static string BuildSheetXml(
        IReadOnlyList<string> columnKeys,
        IReadOnlyList<string> headers,
        IReadOnlyList<Dictionary<string, string>> rows,
        Func<Dictionary<string, string>, string, string> getDisplayValue,
        string lastCol,
        int lastRow,
        SharedStringTable sharedStrings)
    {
        var sb = new StringBuilder(4096 + rows.Count * headers.Count * 24);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" ");
        sb.Append("xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
        sb.Append("<dimension ref=\"A1:").Append(lastCol).Append(lastRow).Append("\"/>");
        sb.Append("<sheetViews><sheetView tabSelected=\"1\" workbookViewId=\"0\">");
        sb.Append("<pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/>");
        sb.Append("</sheetView></sheetViews>");
        sb.Append("<sheetFormatPr defaultRowHeight=\"")
            .Append(DefaultRowHeight.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append("\"/>");
        sb.Append("<cols>");
        for (var c = 0; c < headers.Count; c++)
        {
            var width = EstimateColumnWidth(headers[c]);
            sb.Append("<col min=\"").Append(c + 1).Append("\" max=\"").Append(c + 1)
                .Append("\" width=\"").Append(width.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append("\" customWidth=\"1\"/>");
        }

        sb.Append("</cols><sheetData>");

        AppendRowOpen(sb, 1, HeaderRowHeight);
        for (var c = 0; c < headers.Count; c++)
            AppendSharedStringCell(sb, ToColumnName(c + 1) + "1", headers[c], StyleHeader, sharedStrings);
        sb.Append("</row>");

        if (rows.Count == 0)
        {
            AppendRowOpen(sb, 2, DefaultRowHeight);
            for (var c = 0; c < headers.Count; c++)
                AppendSharedStringCell(sb, ToColumnName(c + 1) + "2", string.Empty, StyleRow, sharedStrings);
            sb.Append("</row>");
        }
        else
        {
            for (var r = 0; r < rows.Count; r++)
            {
                var excelRow = r + 2;
                var styleIndex = (r % 2 == 0) ? StyleRow : StyleRowStripe;
                AppendRowOpen(sb, excelRow, DefaultRowHeight);
                for (var c = 0; c < columnKeys.Count; c++)
                {
                    var value = getDisplayValue(rows[r], columnKeys[c]) ?? string.Empty;
                    AppendSharedStringCell(
                        sb,
                        ToColumnName(c + 1) + excelRow,
                        value,
                        styleIndex,
                        sharedStrings);
                }

                sb.Append("</row>");
            }
        }

        sb.Append("</sheetData>");
        sb.Append("<tableParts count=\"1\">");
        sb.Append("<tablePart r:id=\"rId1\"/>");
        sb.Append("</tableParts>");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    static string BuildTableXml(IReadOnlyList<string> headers, string tableRef)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<table xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" ");
        sb.Append("id=\"1\" name=\"CloudSaveResults\" displayName=\"CloudSaveResults\" ");
        sb.Append("ref=\"").Append(tableRef).Append("\" totalsRowShown=\"0\">");
        sb.Append("<autoFilter ref=\"").Append(tableRef).Append("\"/>");
        sb.Append("<tableColumns count=\"").Append(headers.Count).Append("\">");
        for (var i = 0; i < headers.Count; i++)
        {
            sb.Append("<tableColumn id=\"").Append(i + 1)
                .Append("\" name=\"").Append(EscapeXml(headers[i])).Append("\"/>");
        }

        sb.Append("</tableColumns>");
        // Omit built-in tableStyleInfo so custom dark fills / orange text from styles.xml are preserved.
        sb.Append("</table>");
        return sb.ToString();
    }

    static string BuildWorkbookXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
        "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
        "<sheets><sheet name=\"Results\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
        "</workbook>";

    static string BuildStylesXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
        "<fonts count=\"3\">" +
        "<font><sz val=\"11\"/><color theme=\"1\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
        "<font><b/><sz val=\"11\"/><color rgb=\"" + TextOrangeRgb + "\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
        "<font><sz val=\"11\"/><color rgb=\"" + TextOrangeRgb + "\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
        "</fonts>" +
        "<fills count=\"5\">" +
        "<fill><patternFill patternType=\"none\"/></fill>" +
        "<fill><patternFill patternType=\"gray125\"/></fill>" +
        "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"" + HeaderFillRgb + "\"/></patternFill></fill>" +
        "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"" + RowFillRgb + "\"/></patternFill></fill>" +
        "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"" + RowStripeFillRgb + "\"/></patternFill></fill>" +
        "</fills>" +
        "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
        "<cellStyleXfs count=\"1\">" +
        "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/>" +
        "</cellStyleXfs>" +
        "<cellXfs count=\"4\">" +
        "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
        "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyAlignment=\"1\">" +
        "<alignment wrapText=\"1\" vertical=\"top\"/></xf>" +
        "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyAlignment=\"1\">" +
        "<alignment wrapText=\"1\" vertical=\"top\"/></xf>" +
        "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"4\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyAlignment=\"1\">" +
        "<alignment wrapText=\"1\" vertical=\"top\"/></xf>" +
        "</cellXfs>" +
        "</styleSheet>";

    static string BuildContentTypesXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
        "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
        "<Override PartName=\"/xl/tables/table1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.table+xml\"/>" +
        "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
        "<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>" +
        "</Types>";

    static string BuildRootRelsXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "</Relationships>";

    static string BuildWorkbookRelsXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
        "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>" +
        "</Relationships>";

    static string BuildSheetRelsXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/table\" Target=\"../tables/table1.xml\"/>" +
        "</Relationships>";

    static void AppendRowOpen(StringBuilder sb, int rowNumber, double height)
    {
        sb.Append("<row r=\"").Append(rowNumber)
            .Append("\" ht=\"")
            .Append(height.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append("\" customHeight=\"1\">");
    }

    static void AppendSharedStringCell(
        StringBuilder sb,
        string cellRef,
        string value,
        int styleIndex,
        SharedStringTable sharedStrings)
    {
        var index = sharedStrings.GetIndex(SanitizeCellValue(value));
        sb.Append("<c r=\"").Append(cellRef)
            .Append("\" s=\"").Append(styleIndex)
            .Append("\" t=\"s\"><v>").Append(index).Append("</v></c>");
    }

    static string SanitizeCellValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Normalize newlines; keep content readable in Excel.
        var cleaned = StripInvalidXmlChars(value)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');

        if (cleaned.Length <= ExcelMaxCellChars)
            return cleaned;

        var keep = ExcelMaxCellChars - TruncatedSuffix.Length;
        if (keep < 1)
            keep = ExcelMaxCellChars;

        return cleaned.Substring(0, keep) + TruncatedSuffix;
    }

    static bool NeedsXmlPreserveSpace(string value) =>
        !string.IsNullOrEmpty(value)
        && (value[0] == ' ' || value[value.Length - 1] == ' ' || value.IndexOf('\n') >= 0);

    static string EscapeXml(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var cleaned = StripInvalidXmlChars(value);
        return cleaned
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    static string StripInvalidXmlChars(string value)
    {
        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == 0x9 || c == 0xA || c == 0xD || (c >= 0x20 && c <= 0xD7FF) || (c >= 0xE000 && c <= 0xFFFD))
                sb.Append(c);
        }

        return sb.ToString();
    }

    static string ToColumnName(int columnIndex1Based)
    {
        var n = columnIndex1Based;
        var sb = new StringBuilder();
        while (n > 0)
        {
            n--;
            sb.Insert(0, (char)('A' + n % 26));
            n /= 26;
        }

        return sb.ToString();
    }

    static double EstimateColumnWidth(string header)
    {
        if (string.Equals(header, "naming", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "metadata", StringComparison.OrdinalIgnoreCase))
            return 72;

        if (string.Equals(header, "playerId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "pushToken", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "finalLink", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "userAgent", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "referer", StringComparison.OrdinalIgnoreCase))
            return 36;

        var len = string.IsNullOrEmpty(header) ? 10 : header.Length;
        return Math.Min(48, Math.Max(14, len + 6));
    }

    sealed class SharedStringTable
    {
        readonly List<string> _strings = new();
        readonly Dictionary<string, int> _indexByValue = new(StringComparer.Ordinal);

        public int GetIndex(string value)
        {
            value ??= string.Empty;
            if (_indexByValue.TryGetValue(value, out var existing))
                return existing;

            var index = _strings.Count;
            _strings.Add(value);
            _indexByValue[value] = index;
            return index;
        }

        public string BuildXml()
        {
            var sb = new StringBuilder(256 + _strings.Count * 64);
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\"")
                .Append(_strings.Count)
                .Append("\" uniqueCount=\"")
                .Append(_strings.Count)
                .Append("\">");

            for (var i = 0; i < _strings.Count; i++)
            {
                var value = _strings[i] ?? string.Empty;
                sb.Append("<si><t");
                if (NeedsXmlPreserveSpace(value))
                    sb.Append(" xml:space=\"preserve\"");
                sb.Append('>').Append(EscapeXml(value)).Append("</t></si>");
            }

            sb.Append("</sst>");
            return sb.ToString();
        }
    }
}
#endif
