using System.IO.Compression;
using Project.Api.Controllers;

internal static class QuestionExportSmoke
{
    public static void Run()
    {
        var data = new List<string[]>
        {
            new[] { "1", "2", "2", "A & B < C > \"q\" 'r'", "x=1;y;z", "2" },
            new[] { "1", "1", "3", "=1+1", "+a;-b;@c", "1,3" },
        };
        var bytes = QuestionWorkbook.Write(data);
        using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
        {
            var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ReadToEnd();
            if (sheet.Contains("<f>") || sheet.Contains("<f ")) throw new Exception("Question export must not contain formulas.");
        }
        var rows = QuestionWorkbook.Read(new MemoryStream(bytes), 500);
        if (rows.Count != 2) throw new Exception("Question export row count mismatch.");
        for (var r = 0; r < data.Count; r++)
        {
            if (rows[r].Number != r + 2 || rows[r].Values.Length != 6) throw new Exception("Question export row shape mismatch.");
            for (var c = 0; c < 6; c++)
                if (rows[r].Values[c] != data[r][c]) throw new Exception($"Question export cell mismatch at row {r + 2}, column {c}.");
        }
        try { QuestionWorkbook.Write(new[] { new[] { "1", "2", "3", "4", "5" } }); throw new Exception("Short row accepted."); }
        catch (ArgumentException) { }
    }
}
