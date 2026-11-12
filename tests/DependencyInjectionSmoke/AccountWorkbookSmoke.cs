using System.IO.Compression;
using System.Text;
using Project.Api.Controllers;

internal static class AccountWorkbookSmoke
{
    public static void Run()
    {
        var template = AccountWorkbook.Template();
        if (AccountWorkbook.Read(new MemoryStream(template), 500).Count != 0)
            throw new Exception("Account template should contain headers only.");

        var populated = Patch(template, "xl/worksheets/sheet1.xml", content => content.Replace("</sheetData>",
            "<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>Nguyen Van A</t></is></c><c r=\"B2\"><v>36526</v></c>" +
            "<c r=\"C2\" t=\"inlineStr\"><is><t>a@example.com</t></is></c><c r=\"F2\" t=\"inlineStr\"><is><t>1</t></is></c>" +
            "<c r=\"H2\" t=\"inlineStr\"><is><t xml:space=\"preserve\"> Abcdef12 </t></is></c></row></sheetData>"));
        var rows = AccountWorkbook.Read(new MemoryStream(populated), 500);
        if (rows.Count != 1 || rows[0].Number != 2 || rows[0].Values.Length != 8 || rows[0].Values[0] != "Nguyen Van A" ||
            rows[0].Values[1] != "36526" || rows[0].Values[3] != "" || rows[0].Values[7] != " Abcdef12 ")
            throw new Exception("Account workbook data row was not parsed correctly.");

        try { AccountWorkbook.Read(new MemoryStream(), 500); throw new Exception("Empty stream accepted."); }
        catch (InvalidDataException) { }
        Reject(Patch(template, "xl/workbook.xml", c => c.Replace("name=\"Accounts\"", "name=\"Other\"")), "wrong sheet name");
        Reject(Patch(template, "xl/worksheets/sheet1.xml", c => c.Replace("</sheetData>",
            "<row r=\"2\"><c r=\"I2\" t=\"inlineStr\"><is><t>x</t></is></c></row></sheetData>")), "column I");
        Reject(Patch(template, "xl/worksheets/sheet1.xml", c => c.Replace("</sheetData>",
            "<row r=\"2\"><c r=\"A2\"><f>1+1</f><v>2</v></c></row></sheetData>")), "formula");
        Reject(Patch(template, "xl/worksheets/sheet1.xml", c => c.Replace(">FullName<", ">Name<")), "wrong header");

        var tricky = new[] { "=1+1", "<a&b>", "\"'", "tab\there", "emoji \U0001F600", "ctrl\u0001x" };
        var headers = new[] { "A", "B", "C", "D", "E", "F" };
        var written = AccountWorkbook.Write(headers, new[] { tricky });
        using (var zip = new ZipArchive(new MemoryStream(written), ZipArchiveMode.Read))
        {
            var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ReadToEnd();
            if (sheet.Contains("<f>") || sheet.Contains("<f ")) throw new Exception("Account export must not contain formulas.");
        }
        // Đọc lại bằng XML trực tiếp vì Read đòi tiêu đề v1; kiểm nguyên văn từng ô.
        using var archive = new ZipArchive(new MemoryStream(written), ZipArchiveMode.Read);
        var xml = System.Xml.Linq.XDocument.Load(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        System.Xml.Linq.XNamespace s = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var cells = xml.Descendants(s + "row").Last().Elements(s + "c").Select(c => c.Value).ToArray();
        var expected = new[] { "=1+1", "<a&b>", "\"'", "tab\there", "emoji \U0001F600", "ctrlx" };
        if (!cells.SequenceEqual(expected)) throw new Exception("Account export cells were not preserved verbatim.");
        try { AccountWorkbook.Write(headers, new[] { new[] { "1" } }); throw new Exception("Short row accepted."); }
        catch (ArgumentException) { }
    }

    private static void Reject(byte[] workbook, string label)
    {
        try { AccountWorkbook.Read(new MemoryStream(workbook), 500); }
        catch (InvalidDataException) { return; }
        throw new Exception($"Account workbook accepted: {label}.");
    }

    private static byte[] Patch(byte[] source, string entryName, Func<string, string> change)
    {
        using var output = new MemoryStream();
        using (var input = new ZipArchive(new MemoryStream(source), ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var entry in input.Entries)
            {
                using var reader = new StreamReader(entry.Open());
                var content = reader.ReadToEnd();
                if (entry.FullName == entryName) content = change(content);
                using var writer = new StreamWriter(target.CreateEntry(entry.FullName).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        return output.ToArray();
    }
}
