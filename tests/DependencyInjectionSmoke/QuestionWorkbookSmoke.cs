using System.IO.Compression;
using System.Text;
using Project.Api.Controllers;

internal static class QuestionWorkbookSmoke
{
    public static void Run()
    {
        var template = QuestionWorkbook.Template();
        using var stream = new MemoryStream(template);
        var rows = QuestionWorkbook.Read(stream, 500);
        if (rows.Count != 0) throw new Exception("Question template should contain headers only.");
        using var populated = new MemoryStream();
        using (var source = new ZipArchive(new MemoryStream(template), ZipArchiveMode.Read))
        using (var target = new ZipArchive(populated, ZipArchiveMode.Create, true))
            foreach (var entry in source.Entries)
            {
                using var reader = new StreamReader(entry.Open());
                var content = reader.ReadToEnd();
                if (entry.FullName == "xl/worksheets/sheet1.xml")
                    content = content.Replace("</sheetData>",
                        "<row r=\"2\"><c r=\"A2\"><v>1</v></c><c r=\"B2\"><v>1</v></c>" +
                        "<c r=\"C2\"><v>2</v></c><c r=\"D2\" t=\"inlineStr\"><is><t>2+2?</t></is></c>" +
                        "<c r=\"E2\" t=\"inlineStr\"><is><t>3;4;5</t></is></c>" +
                        "<c r=\"F2\"><v>2</v></c></row></sheetData>");
                using var writer = new StreamWriter(target.CreateEntry(entry.FullName).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        populated.Position = 0;
        var imported = QuestionWorkbook.Read(populated, 500);
        if (imported.Count != 1 || imported[0].Number != 2 || imported[0].Values[4] != "3;4;5")
            throw new Exception("Workbook data row was not parsed correctly.");
        using var bad = new MemoryStream();
        try { QuestionWorkbook.Read(bad, 500); throw new Exception("Invalid workbook accepted."); }
        catch (InvalidDataException) { }
    }
}
