using System.IO.Compression;
using System.Xml.Linq;
using Project.Api.Services;
using Project.Application.Services;

// To-be T13: bộ ghi .xlsx dùng chung + dựng 4 sheet báo cáo lớp.
internal static class XlsxReportSmoke
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static void Run()
    {
        Check(XlsxWriter.ColumnName(0) == "A" && XlsxWriter.ColumnName(25) == "Z" && XlsxWriter.ColumnName(26) == "AA" &&
            XlsxWriter.ColumnName(27) == "AB" && XlsxWriter.ColumnName(701) == "ZZ" && XlsxWriter.ColumnName(702) == "AAA", "Column names beyond Z.");

        var wide = Enumerable.Range(0, 30).Select(i => $"H{i}").ToArray();
        var bytes = XlsxWriter.Write([
            new XlsxSheet("Một", ["Text", "Số", "Trống", "Công thức?", "Emoji"],
                [[XlsxCell.Str("a\u0001b"), XlsxCell.Num(7.25m), XlsxCell.Empty, XlsxCell.Str("=1+1"), XlsxCell.Str("x😀y")]], [10, 8]),
            new XlsxSheet("Rộng", wide, [wide.Select((_, i) => XlsxCell.Num(i)).ToArray()]),
        ]);
        var book = Read(bytes);
        Check(book.Names.SequenceEqual(new[] { "Một", "Rộng" }), "Two sheets with names.");
        var s1 = book.Sheets[0];
        Check(!book.All.Contains("<f>") && !book.All.Contains("<f "), "Never writes formulas.");
        Check(Cell(s1, "A2")?.Attribute("t")?.Value == "inlineStr" && Cell(s1, "A2")!.Value == "ab", "Invalid XML char removed; text is inlineStr.");
        Check(Cell(s1, "B2")?.Attribute("t") == null && Cell(s1, "B2")!.Element(S + "v")?.Value == "7.25", "Number cell uses <v> invariant.");
        Check(Cell(s1, "C2") == null, "Empty cell omitted.");
        Check(Cell(s1, "D2")!.Value == "=1+1" && Cell(s1, "D2")!.Attribute("t")!.Value == "inlineStr", "'=1+1' stays text.");
        Check(Cell(s1, "E2")!.Value == "x😀y", "Emoji kept.");
        Check(Cell(s1, "A1")!.Attribute("s")?.Value == "1" && s1.Descendants(S + "pane").Any(), "Bold, frozen header.");
        Check(Cell(book.Sheets[1], "AD2")!.Element(S + "v")!.Value == "29", "Column AD written.");
        var bad = 0;
        foreach (var name in new[] { "", "a/b", "x[1]", new string('a', 32) })
            try { XlsxWriter.Write([new XlsxSheet(name, ["A"], [])]); } catch (ArgumentException) { bad++; }
        try { XlsxWriter.Write([new XlsxSheet("A", ["A"], []), new XlsxSheet("a", ["A"], [])]); } catch (ArgumentException) { bad++; }
        try { XlsxWriter.Write([new XlsxSheet("A", ["A"], [[XlsxCell.Num(1), XlsxCell.Num(2)]])]); } catch (ArgumentException) { bad++; }
        Check(bad == 6, "Invalid/duplicate sheet names and too-wide rows rejected.");

        // ClassReportExport: 27 lịch → cột điểm vượt Z; Top/Bottom; ô điểm trống khi không làm
        var users = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var end = new DateTime(2026, 11, 20, 3, 0, 0, DateTimeKind.Utc);
        var schedules = Enumerable.Range(1, 27).Select(i => new RankingSchedule(i, $"L{i}", "Đề", end, end.AddHours(i), true, true, null))
            .Append(new RankingSchedule(99, "Chưa", "Đề", end, end, true, false, "notEnded")).ToList();
        var attempts = users.SelectMany((u, k) => Enumerable.Range(1, 27).Where(i => !(k == 4 && i == 3))
            .Select(i => new RankingAttempt(u, i, 10 - k, 10, true))).ToList();
        var ranking = ClassRanking.Calculate(schedules.Where(s => s.Counted).Select(s => s.Id).ToList(), users, attempts);
        var members = users.Select((u, k) => new RankingMember(u, $"u{k}", $"Học viên {k}", $"u{k}@example.invalid")).ToList();
        var data = new ClassRankingData(schedules, members, ranking, end, ClassRanking.ChooseAttempts(attempts));
        var view = new ClassReportView("classExamSchedulesOnly", "rankingAttempt", "snapshotOrCurrentBank", "snapshot", true, end,
            new ReportThresholdsView(50, 5, 40, 10, 50), 27, 1, 134, 0, 5, 0, 3, 5,
            [new ReportChapterView(1, "Chương 1", false, 20, 8, 40m, 5, 1, [new ReportWeakStudent(users[4], "Học viên 4", "u4")]),
             new ReportChapterView(null, null, false, 2, 1, 50m, 1, 0, [])],
            [new ReportHardView(11, "Câu khó", true, "Chương 1", "Khó", 10, 2, 20m, "Nhiễu", 6)]) { Ranking = data };
        var sheets = ClassReportExport.Build(view, new ClassReportExportInfo("Lớp A", "LA-01", "Toán", "GV (gv)", end));
        Check(sheets.Select(s => s.Name).SequenceEqual(new[] { "Xếp hạng lớp", "Theo chương", "Câu hay sai", "Thông tin" }), "Four sheets.");
        var r = sheets[0];
        Check(r.Headers.Count == 7 + 27 + 3 && r.Headers[^3] == "Điểm tổng (thang 10)" && r.Headers[7].StartsWith("L1 (kết thúc 20/11/2026)"),
            "Ranking headers: fixed + one per counted schedule (not the not-counted one).");
        Check(r.Rows[0][1].Number == 1 && r.Rows[0][^1].Text == "Top" && r.Rows[4][^1].Text == "Bottom", "Rank and Top/Bottom.");
        Check(r.Rows[4][7 + 2].Number == null && r.Rows[4][7 + 2].Text == null, "Missing schedule → empty score cell.");
        Check(sheets[1].Rows[1][0].Text == "(Chưa gán chương)" && sheets[1].Rows[0][3].Number == 40m && sheets[1].Rows[0][6].Text == "Học viên 4 (u4)",
            "Chapter sheet: unchaptered label, rate number, weak names.");
        Check(sheets[2].Rows[0][1].Text == "Câu khó (đã ẩn)" && sheets[2].Rows[0][6].Number == 20m, "Hard sheet.");
        Check(sheets[3].Rows.Any(x => x[0].Text == "Thời điểm xuất (giờ Việt Nam, UTC+7)" && x[1].Text == "20/11/2026 10:00"), "Info: Vietnam time.");
        var file = Read(XlsxWriter.Write(sheets));
        Check(Cell(file.Sheets[0], "AH1") != null && !file.All.Contains("<f>") && !file.All.Contains("<f "), "Report workbook writes beyond Z, no formulas.");
        Check(ClassReportExport.FileName("LA 01/x", end) == "bao-cao-lop-LA-01-x-20261120-1000.xlsx", "Safe ASCII file name, Vietnam time.");
        // Review T13: ô > 32767 ký tự bị cắt (không tách emoji); surrogate lẻ bị loại; tên sheet có dấu nháy đầu/cuối bị từ chối
        var longText = new string('a', XlsxWriter.MaxCellLength - 2) + "😀" + new string('b', 100);
        var cut = XlsxWriter.Limit(longText);
        Check(cut.Length <= XlsxWriter.MaxCellLength && cut.EndsWith('…') && !char.IsHighSurrogate(cut[^2]), "Long cell cut ≤ 32767 without splitting a surrogate.");
        var longBook = Read(XlsxWriter.Write([new XlsxSheet("Dài", ["A", "B"], [[XlsxCell.Str(new string('x', 40000)), XlsxCell.Str("a\uD800b")]])]));
        Check(Cell(longBook.Sheets[0], "A2")!.Value.Length <= XlsxWriter.MaxCellLength && Cell(longBook.Sheets[0], "B2")!.Value == "ab",
            "Workbook cell limited; lone surrogate removed.");
        var quoted = 0;
        foreach (var name in new[] { "'abc", "abc'" })
            try { XlsxWriter.Write([new XlsxSheet(name, ["A"], [])]); } catch (ArgumentException) { quoted++; }
        Check(quoted == 2, "Sheet names starting/ending with an apostrophe rejected.");
        Check(r.Rows[0][^3].Number == 10m && r.Rows.All(x => x[^3].Text == null), "Total column numbers only (no text).");
        var blankSchedules = new List<RankingSchedule> { new(1, "  ", "Đề ABC", end, end, true, true, null) };
        var blankRanking = ClassRanking.Calculate([1], users, attempts.Where(a => a.ScheduleId == 1).ToList());
        ClassReportView blankView;
        blankView = new ClassReportView(view.Scope, view.AttemptsCounted, view.Classification, view.CorrectAnswersFrom, view.AnswersIncludeBlank,
            view.ServerNow, view.Thresholds, 1, 0, 5, 0, 5, 0, 0, 0, [], [])
            { Ranking = new ClassRankingData(blankSchedules, members, blankRanking, end, ClassRanking.ChooseAttempts(attempts)) };
        var blankSheets = ClassReportExport.Build(blankView, new ClassReportExportInfo("Lớp A", "LA-01", null, "GV", end));
        Check(blankSheets[0].Headers[7].StartsWith("Đề ABC (kết thúc ") && blankSheets[2].Rows.Count == 0, "Blank schedule title → exam name; empty hard sheet.");
        Console.WriteLine("XLSX_WRITER_MULTI_SHEET_NUMBERS_COLUMNS_REPORT_EXPORT=PASS (isolated; no SQL/API)");
    }

    private sealed record Book(IReadOnlyList<string> Names, IReadOnlyList<XElement> Sheets, string All);

    private static Book Read(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        string Text(string name) { using var reader = new StreamReader(zip.GetEntry(name)!.Open()); return reader.ReadToEnd(); }
        var workbook = XElement.Parse(Text("xl/workbook.xml"));
        var names = workbook.Descendants(S + "sheet").Select(x => x.Attribute("name")!.Value).ToList();
        var sheets = names.Select((_, i) => XElement.Parse(Text($"xl/worksheets/sheet{i + 1}.xml"))).ToList();
        _ = XElement.Parse(Text("xl/styles.xml"));
        return new Book(names, sheets, string.Concat(zip.Entries.Select(e => Text(e.FullName))));
    }

    private static XElement? Cell(XElement sheet, string reference) =>
        sheet.Descendants(S + "c").FirstOrDefault(c => c.Attribute("r")?.Value == reference);

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
