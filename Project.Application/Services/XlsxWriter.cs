using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Project.Application.Services;

/// <summary>Một ô: chuỗi (inlineStr), số (&lt;v&gt;) hoặc trống.</summary>
public sealed record XlsxCell(string? Text, decimal? Number)
{
    public static readonly XlsxCell Empty = new(null, null);
    public static XlsxCell Str(string? value) => value == null ? Empty : new(value, null);
    public static XlsxCell Num(decimal? value) => value == null ? Empty : new(null, value);
    public static XlsxCell Num(int value) => new(null, value);
}

public sealed record XlsxSheet(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<XlsxCell>> Rows,
    IReadOnlyList<double>? Widths = null);

/// <summary>
/// To-be T13 (Q14): bộ ghi .xlsx tối thiểu dùng chung — nhiều sheet, ô chuỗi/số, cột vượt Z, tiêu đề in đậm + cố định
/// hàng tiêu đề, độ rộng cột. KHÔNG bao giờ ghi công thức (chuỗi bắt đầu bằng = + - @ vẫn là text). Ký tự không hợp lệ
/// trong XML bị loại (giữ cặp surrogate hợp lệ). Hai bộ ghi cũ (câu hỏi, tài khoản) chưa chuyển sang (QT13-1).
/// </summary>
public static class XlsxWriter
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public const int MaxSheetNameLength = 31;

    /// <summary>Tên cột theo chỉ số 0: 0 → A, 25 → Z, 26 → AA.</summary>
    public static string ColumnName(int index)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        var name = string.Empty;
        for (var n = index + 1; n > 0; n = (n - 1) / 26) name = (char)('A' + (n - 1) % 26) + name;
        return name;
    }

    /// <summary>Giới hạn ký tự của một ô Excel; vượt thì Excel báo lỗi nội dung khi mở file.</summary>
    public const int MaxCellLength = 32767;

    /// <summary>Review T13: cắt chuỗi quá dài còn ≤ <see cref="MaxCellLength"/> ký tự (thêm "…"), không tách cặp surrogate.</summary>
    public static string Limit(string value)
    {
        if (value.Length <= MaxCellLength) return value;
        var cut = MaxCellLength - 1;
        if (char.IsLowSurrogate(value[cut])) cut--;
        return value[..cut] + "…";
    }

    public static string Clean(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            { builder.Append(value[i]).Append(value[i + 1]); i++; continue; }
            if (!char.IsSurrogate(value[i]) && XmlConvert.IsXmlChar(value[i])) builder.Append(value[i]);
        }
        return builder.ToString();
    }

    public static byte[] Write(IReadOnlyList<XlsxSheet> sheets)
    {
        if (sheets.Count == 0) throw new ArgumentException("Cần ít nhất một sheet.", nameof(sheets));
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sheet in sheets)
        {
            if (string.IsNullOrWhiteSpace(sheet.Name) || sheet.Name.Length > MaxSheetNameLength ||
                sheet.Name.IndexOfAny(['[', ']', ':', '*', '?', '/', '\\']) >= 0 || sheet.Name.StartsWith('\'') || sheet.Name.EndsWith('\''))
                throw new ArgumentException($"Tên sheet không hợp lệ: '{sheet.Name}'.", nameof(sheets));
            if (!names.Add(sheet.Name)) throw new ArgumentException($"Tên sheet bị trùng: '{sheet.Name}'.", nameof(sheets));
            if (sheet.Rows.Any(r => r.Count > sheet.Headers.Count))
                throw new ArgumentException($"Sheet '{sheet.Name}' có dòng nhiều ô hơn tiêu đề.", nameof(sheets));
        }

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Entry(string name, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
            var overrides = string.Concat(sheets.Select((_, i) =>
                $"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"));
            Entry("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                overrides + "</Types>");
            Entry("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            var workbook = new XElement(S + "workbook", new XAttribute(XNamespace.Xmlns + "r", R),
                new XElement(S + "sheets", sheets.Select((sheet, i) => new XElement(S + "sheet",
                    new XAttribute("name", Clean(sheet.Name)), new XAttribute("sheetId", i + 1), new XAttribute(R + "id", $"rId{i + 1}")))));
            Entry("xl/workbook.xml", workbook.ToString(SaveOptions.DisableFormatting));
            Entry("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                string.Concat(sheets.Select((_, i) => $"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>")) +
                $"<Relationship Id=\"rId{sheets.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
            // Style 0: mặc định; style 1: chữ đậm (hàng tiêu đề)
            Entry("xl/styles.xml", $"<styleSheet xmlns=\"{S}\"><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
                "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill>" +
                "<fill><patternFill patternType=\"gray125\"/></fill></fills><borders count=\"1\"><border/></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/></cellXfs>" +
                "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>");
            for (var i = 0; i < sheets.Count; i++) Entry($"xl/worksheets/sheet{i + 1}.xml", SheetXml(sheets[i]));
        }
        return output.ToArray();
    }

    private static string SheetXml(XlsxSheet sheet)
    {
        XElement Cell(int column, int row, XlsxCell cell, bool header)
        {
            var element = new XElement(S + "c", new XAttribute("r", $"{ColumnName(column)}{row}"));
            if (header) element.Add(new XAttribute("s", 1));
            if (cell.Number is { } number)
                element.Add(new XElement(S + "v", number.ToString(CultureInfo.InvariantCulture)));
            else
                element.Add(new XAttribute("t", "inlineStr"), new XElement(S + "is",
                    new XElement(S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), Limit(Clean(cell.Text ?? string.Empty)))));
            return element;
        }
        var sheetData = new XElement(S + "sheetData",
            new XElement(S + "row", new XAttribute("r", 1), sheet.Headers.Select((h, c) => Cell(c, 1, XlsxCell.Str(h), true))));
        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            var rowNumber = r + 2;
            sheetData.Add(new XElement(S + "row", new XAttribute("r", rowNumber),
                sheet.Rows[r].Select((cell, c) => (cell, c)).Where(x => x.cell.Text != null || x.cell.Number != null)
                    .Select(x => Cell(x.c, rowNumber, x.cell, false))));
        }
        var worksheet = new XElement(S + "worksheet",
            new XElement(S + "sheetViews", new XElement(S + "sheetView", new XAttribute("workbookViewId", 0),
                new XElement(S + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"),
                    new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))));
        if (sheet.Widths is { Count: > 0 } widths)
            worksheet.Add(new XElement(S + "cols", widths.Select((w, i) => new XElement(S + "col", new XAttribute("min", i + 1),
                new XAttribute("max", i + 1), new XAttribute("width", Math.Clamp(w, 2, 120).ToString(CultureInfo.InvariantCulture)),
                new XAttribute("customWidth", 1)))));
        worksheet.Add(sheetData);
        return worksheet.ToString(SaveOptions.DisableFormatting);
    }
}
