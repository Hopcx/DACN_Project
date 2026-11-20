namespace Project.Application.Services;

/// <summary>Một lượt đã chốt, chưa hủy. <see cref="MaxMark"/> = tổng điểm câu trong snapshot của lượt (QT10-2).</summary>
public sealed record RankingAttempt(Guid UserId, int ScheduleId, decimal TotalMark, decimal MaxMark, bool IsPassed);

public sealed record RankingRow(Guid UserId, IReadOnlyDictionary<int, decimal?> Scores, int AttemptedCount,
    int PassedCount, decimal? Total, int? Rank, string? Group);

public sealed record RankingResult(IReadOnlyList<RankingRow> Rows, int ScoredCount, bool RankingVisible, int SkippedAttempts);

/// <summary>
/// To-be T10 (Q8, Q13b): xếp hạng học viên trong một lớp, chỉ dữ liệu bài thi. Hàm thuần, không I/O.
/// - Điểm một lịch = lượt cao nhất quy về thang 10 (làm tròn 2 chữ số).
/// - Điểm tổng = trung bình trên mọi lịch được tính; lịch không làm tính 0 (Q8a).
/// - Ai chưa có lượt nào trong các lịch được tính: "chưa có điểm", không xếp hạng (QT10-3).
/// - Đồng hạng 1-1-3, so trên điểm tổng đã làm tròn (Q8e).
/// - Top/Bottom 20% (làm tròn lên, tối thiểu 1), chỉ khi ≥ 5 học viên có điểm; không chồng nhau (Q13b).
/// </summary>
public static class ClassRanking
{
    public const int MinScoredForRanking = 5;
    public const string Top = "top";
    public const string Bottom = "bottom";

    public static decimal TenScale(decimal totalMark, decimal maxMark) =>
        decimal.Round(Math.Clamp(totalMark * 10m / maxMark, 0m, 10m), 2, MidpointRounding.ToEven);

    public static RankingResult Calculate(IReadOnlyList<int> scheduleIds, IReadOnlyList<Guid> memberIds,
        IEnumerable<RankingAttempt> attempts)
    {
        var schedules = scheduleIds.Distinct().ToList();
        var scheduleSet = schedules.ToHashSet();
        var members = memberIds.Distinct().ToList();
        var memberSet = members.ToHashSet();
        var skipped = 0;
        var best = new Dictionary<(Guid, int), decimal>();
        var passed = new HashSet<(Guid, int)>();
        foreach (var a in attempts)
        {
            if (!memberSet.Contains(a.UserId) || !scheduleSet.Contains(a.ScheduleId)) continue;
            if (a.MaxMark <= 0m) { skipped++; continue; }
            var key = (a.UserId, a.ScheduleId);
            var score = TenScale(a.TotalMark, a.MaxMark);
            if (!best.TryGetValue(key, out var current) || score > current) best[key] = score;
            if (a.IsPassed) passed.Add(key);
        }

        var rows = members.Select(userId =>
        {
            var scores = schedules.ToDictionary(id => id,
                id => best.TryGetValue((userId, id), out var s) ? (decimal?)s : null);
            var attempted = scores.Values.Count(v => v.HasValue);
            decimal? total = attempted == 0 || schedules.Count == 0 ? null
                : decimal.Round(scores.Values.Sum(v => v ?? 0m) / schedules.Count, 2, MidpointRounding.ToEven);
            return (userId, scores, attempted, passedCount: schedules.Count(id => passed.Contains((userId, id))), total);
        }).ToList();

        var totals = rows.Where(r => r.total.HasValue).Select(r => r.total!.Value).ToList();
        var scoredCount = totals.Count;
        var visible = scoredCount >= MinScoredForRanking;
        var groupSize = (int)Math.Ceiling(scoredCount * 0.2m);
        var result = rows.Select(r =>
        {
            int? rank = r.total.HasValue ? 1 + totals.Count(t => t > r.total.Value) : null;
            string? group = null;
            if (visible && r.total.HasValue)
            {
                if (rank <= groupSize) group = Top;
                else if (1 + totals.Count(t => t < r.total.Value) <= groupSize) group = Bottom;
            }
            return new RankingRow(r.userId, r.scores, r.attempted, r.passedCount, r.total, rank, group);
        }).ToList();
        return new RankingResult(result, scoredCount, visible, skipped);
    }
}
