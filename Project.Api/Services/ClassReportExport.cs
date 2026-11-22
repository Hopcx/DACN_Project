using System.Globalization;
using Project.Application.Services;

namespace Project.Api.Services;

public sealed record ClassReportExportInfo(string ClassName, string ClassCode, string? SubjectName, string ExporterName, DateTime ExportedAtUtc);

/// <summary>
/// To-be T13 (Q14): dựng 4 sheet "Xếp hạng lớp", "Theo chương", "Câu hay sai", "Thông tin" từ báo cáo lớp (T10 + T12).
/// Hàm thuần. Tỉ lệ đúng là số 0–100 (QT13-2); điểm thang 10 là số. Không có đáp án đúng.
/// </summary>
public static class ClassReportExport
{
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    public const string RankingSheet = "Xếp hạng lớp";
    public const string ChapterSheet = "Theo chương";
    public const string HardSheet = "Câu hay sai";
    public const string InfoSheet = "Thông tin";

    public static string Vietnam(DateTime utc, string format = "dd/MM/yyyy HH:mm") =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(VietnamOffset).ToString(format, CultureInfo.InvariantCulture);

    public static string FileName(string classCode, DateTime exportedAtUtc)
    {
        var safe = new string(classCode.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray()).Trim('-');
        return $"bao-cao-lop-{(safe.Length == 0 ? "lop" : safe)}-{Vietnam(exportedAtUtc, "yyyyMMdd-HHmm")}.xlsx";
    }

    public static IReadOnlyList<XlsxSheet> Build(ClassReportView report, ClassReportExportInfo info)
    {
        var data = report.Ranking;
        var counted = data.Schedules.Where(s => s.Counted).ToList();
        var byUser = data.Result.Rows.ToDictionary(x => x.UserId);

        // 1. Xếp hạng lớp — cùng thứ tự với API ranking
        var rankingHeaders = new List<string> { "STT", "Hạng", "Username", "Họ tên", "Email", "Số lịch đã làm", "Tổng số lịch" };
        rankingHeaders.AddRange(counted.Select(s => $"{Label(s)} (kết thúc {Vietnam(s.EndTime, "dd/MM/yyyy")})"));
        rankingHeaders.AddRange(["Điểm tổng (thang 10)", "Số lần đạt", "Nhóm"]);
        var rankingRows = data.Members.Select(m => (m, r: byUser[m.UserId]))
            .OrderBy(x => x.r.Rank ?? int.MaxValue).ThenBy(x => x.m.FullName).ThenBy(x => x.m.UserName)
            .Select((x, i) =>
            {
                var row = new List<XlsxCell>
                {
                    XlsxCell.Num(i + 1), XlsxCell.Num(x.r.Rank), XlsxCell.Str(x.m.UserName), XlsxCell.Str(x.m.FullName),
                    XlsxCell.Str(x.m.Email), XlsxCell.Num(x.r.AttemptedCount), XlsxCell.Num(counted.Count)
                };
                row.AddRange(counted.Select(s => XlsxCell.Num(x.r.Scores[s.Id])));
                row.Add(XlsxCell.Num(x.r.Total));   // Review T13: chưa có điểm → ô trống (giống API), cột chỉ chứa số
                row.Add(XlsxCell.Num(x.r.PassedCount));
                row.Add(XlsxCell.Str(x.r.Group switch { ClassRanking.Top => "Top", ClassRanking.Bottom => "Bottom", _ => "—" }));
                return (IReadOnlyList<XlsxCell>)row;
            }).ToList();
        var rankingWidths = new List<double> { 6, 7, 22, 26, 30, 14, 12 };
        rankingWidths.AddRange(counted.Select(_ => 18d));
        rankingWidths.AddRange([18, 11, 9]);

        // 2. Theo chương
        var chapterRows = report.Chapters.Select(c => (IReadOnlyList<XlsxCell>)new[]
        {
            XlsxCell.Str(c.ChapterId == null ? "(Chưa gán chương)" : c.Hidden ? $"{c.Name} (đã ẩn)" : c.Name),
            XlsxCell.Num(c.Total), XlsxCell.Num(c.Correct), XlsxCell.Num(c.Rate), XlsxCell.Num(c.StudentCount), XlsxCell.Num(c.WeakCount),
            XlsxCell.Str(string.Join("; ", c.WeakStudents.Select(s => $"{s.FullName} ({s.UserName})")))
        }).ToList();

        // 3. Câu hay sai — không có đáp án đúng
        var hardRows = report.HardQuestions.Select(h => (IReadOnlyList<XlsxCell>)new[]
        {
            XlsxCell.Num(h.QuestionId), XlsxCell.Str(h.Hidden ? $"{h.Content} (đã ẩn)" : h.Content), XlsxCell.Str(h.ChapterName ?? "—"),
            XlsxCell.Str(h.Level ?? "—"), XlsxCell.Num(h.Answers), XlsxCell.Num(h.Correct), XlsxCell.Num(h.Rate),
            XlsxCell.Str(h.Distractor ?? "—"), XlsxCell.Num(h.DistractorCount)
        }).ToList();

        // 4. Thông tin
        var t = report.Thresholds;
        var notCounted = data.Schedules.Where(s => !s.Counted).ToList();
        var hardNote = report.HardQuestions.Count > 0 ? $"{report.HardQuestions.Count} câu"
            : report.MaxAnswersPerQuestion < t.HardMinAnswers
                ? $"Chưa có câu nào đủ {t.HardMinAnswers} lượt trả lời (nhiều nhất hiện có: {report.MaxAnswersPerQuestion})."
                : $"Không có câu nào có tỉ lệ đúng dưới {t.HardBelow.ToString(CultureInfo.InvariantCulture)}%.";
        var infoRows = new (string, string)[]
        {
            ("Lớp", info.ClassName), ("Mã lớp", info.ClassCode), ("Môn", info.SubjectName ?? "—"),
            ("Thời điểm xuất (giờ Việt Nam, UTC+7)", Vietnam(info.ExportedAtUtc)), ("Người xuất", info.ExporterName),
            ("Phạm vi dữ liệu", "Chỉ bài thi của lớp (không gồm Luyện tập/Thi thử); thành viên đã duyệt tại thời điểm xuất."),
            ("Lịch được tính", $"{counted.Count} lịch đã kết thúc và đã công bố kết quả" +
                (counted.Count == 0 ? "" : ": " + string.Join("; ", counted.Select(s => Label(s))))),
            ("Lịch chưa tính", notCounted.Count == 0 ? "0" : $"{notCounted.Count}: " + string.Join("; ", notCounted.Select(s => $"{Label(s)} ({Reason(s.NotCountedReason)})"))),
            ("Cách tính điểm", "Mỗi lịch lấy lượt có điểm cao nhất (hòa: lượt nộp sau), quy về thang 10 theo tổng điểm câu của đề đã làm; " +
                "điểm tổng = trung bình mọi lịch được tính, lịch không làm tính 0 (ô trống = chưa có điểm); đồng hạng 1-1-3; Top/Bottom = 20% (làm tròn lên) khi lớp có ≥ " +
                $"{ClassRanking.MinScoredForRanking} học viên có điểm."),
            ("Học viên \"Yếu\" theo chương", $"Tỉ lệ đúng dưới {t.WeakBelow.ToString(CultureInfo.InvariantCulture)}% với ít nhất {t.MinQuestions} câu của chương (lượt được tính)."),
            ("Câu hay sai", $"Tỉ lệ đúng dưới {t.HardBelow.ToString(CultureInfo.InvariantCulture)}% với ít nhất {t.HardMinAnswers} lượt (tính cả câu bỏ trống); " +
                $"tối đa {t.MaxHardQuestions} câu. Đáp án nhiễu = đáp án sai (theo đề lúc thi) được chọn nhiều nhất. Kết quả: {hardNote}"),
            ("Ghi chú dữ liệu", $"Chương theo phân loại lúc thi; nội dung câu theo ngân hàng hiện tại. " +
                $"{report.CurrentClassificationCount} câu trả lời phân loại theo ngân hàng hiện tại; {report.AttemptsWithoutOutcomes} lượt chưa có kết quả từng câu."),
            ("Bảo mật", "File chứa thông tin cá nhân học viên (họ tên, email, điểm) — không chia sẻ ngoài phạm vi lớp. Không chứa đáp án đúng."),
        };

        return
        [
            new XlsxSheet(RankingSheet, rankingHeaders, rankingRows, rankingWidths),
            new XlsxSheet(ChapterSheet, ["Chương", "Số câu đã làm", "Số câu đúng", "Tỉ lệ đúng (%)", "Học viên có dữ liệu", "Số học viên \"Yếu\"", "Học viên \"Yếu\""],
                chapterRows, [30, 14, 12, 14, 18, 18, 60]),
            new XlsxSheet(HardSheet, ["Mã câu", "Nội dung (≤ 200 ký tự)", "Chương", "Mức độ", "Số lượt", "Số đúng", "Tỉ lệ đúng (%)", "Đáp án nhiễu chọn nhiều nhất", "Số lần chọn đáp án nhiễu"],
                hardRows, [9, 60, 24, 12, 9, 9, 14, 36, 14]),
            new XlsxSheet(InfoSheet, ["Mục", "Giá trị"],
                infoRows.Select(x => (IReadOnlyList<XlsxCell>)new[] { XlsxCell.Str(x.Item1), XlsxCell.Str(x.Item2) }).ToList(), [34, 110]),
        ];
    }

    // Review T13: tiêu đề rỗng/khoảng trắng → tên đề
    private static string Label(RankingSchedule s) => string.IsNullOrWhiteSpace(s.Title) ? s.ExamName : s.Title.Trim();

    private static string Reason(string? reason) => reason switch
    {
        "notEnded" => "chưa kết thúc", "unknownTimeZone" => "chưa rõ múi giờ", "resultNotReleased" => "chưa công bố kết quả",
        "inactive" => "lịch đã tắt", _ => "không tính"
    };
}
