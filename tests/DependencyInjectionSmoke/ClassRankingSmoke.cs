using Project.Api.Services;
using Project.Application.Services;

// To-be T10: xếp hạng lớp — hàm thuần.
internal static class ClassRankingSmoke
{
    public static void Run()
    {
        Check(ClassRanking.TenScale(7, 8) == 8.75m && ClassRanking.TenScale(1, 3) == 3.33m && ClassRanking.TenScale(12, 10) == 10m,
            "Ten scale uses the attempt maximum, rounds to 2 digits, clamps to 10.");

        var users = Enumerable.Range(0, 7).Select(_ => Guid.NewGuid()).ToArray();
        int s1 = 11, s2 = 12;
        var attempts = new List<RankingAttempt>
        {
            // u0: 10 và 8 → 9 (lượt cao nhất của s1 là 10)
            new(users[0], s1, 5, 10, false), new(users[0], s1, 10, 10, true), new(users[0], s2, 8, 10, true),
            // u1: 9 và 9 → 9 (hòa với u0)
            new(users[1], s1, 9, 10, true), new(users[1], s2, 18, 20, true),
            // u2: chỉ s1 = 8 → 4 (lịch không làm tính 0)
            new(users[2], s1, 4, 5, true),
            // u3: 6,6 → 6; u4: 5,5 → 5; u5: 2,0 → 1
            new(users[3], s1, 6, 10, false), new(users[3], s2, 6, 10, false),
            new(users[4], s1, 5, 10, false), new(users[4], s2, 5, 10, false),
            new(users[5], s1, 2, 10, false), new(users[5], s2, 0, 10, false),
            // lượt có tổng điểm câu 0 bị bỏ; lượt của người ngoài lớp và lịch không tính bị bỏ qua
            new(users[6], s1, 0, 0, false), new(Guid.NewGuid(), s1, 10, 10, true), new(users[3], 99, 10, 10, true),
        };
        var r = ClassRanking.Calculate(new[] { s1, s2 }, users, attempts);
        RankingRow Row(int i) => r.Rows.Single(x => x.UserId == users[i]);
        Check(r.SkippedAttempts == 1 && r.ScoredCount == 6 && r.RankingVisible, "Counts and visibility.");
        Check(Row(0).Scores[s1] == 10m && Row(0).Total == 9m && Row(0).PassedCount == 2 && Row(0).AttemptedCount == 2, "Best attempt per schedule.");
        Check(Row(2).Total == 4m && Row(2).Scores[s2] == null && Row(2).AttemptedCount == 1, "Missing schedule counts as zero.");
        Check(Row(0).Rank == 1 && Row(1).Rank == 1 && Row(3).Rank == 3 && Row(4).Rank == 4 && Row(2).Rank == 5 && Row(5).Rank == 6,
            "Ties ranked 1-1-3.");
        Check(Row(6).Total == null && Row(6).Rank == null && Row(6).Group == null, "No score → not ranked.");
        // 6 có điểm → nhóm = ceil(1.2) = 2: Top = hạng ≤ 2 (u0, u1); Bottom = 2 thấp nhất (u5, u2).
        Check(Row(0).Group == ClassRanking.Top && Row(1).Group == ClassRanking.Top && Row(5).Group == ClassRanking.Bottom &&
            Row(2).Group == ClassRanking.Bottom && Row(3).Group == null && Row(4).Group == null, "Top/Bottom 20%.");

        var small = ClassRanking.Calculate(new[] { s1 }, users.Take(4).ToArray(),
            users.Take(4).Select(u => new RankingAttempt(u, s1, 5, 10, false)));
        Check(!small.RankingVisible && small.ScoredCount == 4 && small.Rows.All(x => x.Group == null && x.Rank == 1),
            "Below 5 scored: no groups; all tied rank 1.");
        var tied = ClassRanking.Calculate(new[] { s1 }, users.Take(5).ToArray(),
            users.Take(5).Select(u => new RankingAttempt(u, s1, 5, 10, false)));
        Check(tied.RankingVisible && tied.Rows.All(x => x.Group == ClassRanking.Top), "All tied: Top only, no overlap with Bottom.");
        var none = ClassRanking.Calculate(Array.Empty<int>(), users, attempts);
        Check(none.ScoredCount == 0 && none.Rows.All(x => x.Total == null && x.Rank == null), "No counted schedules: nobody scored.");
        Check(ClassRankingQuery.WithinLimits(200, 500) && !ClassRankingQuery.WithinLimits(201, 1) && !ClassRankingQuery.WithinLimits(1, 501),
            "Limits: 200 schedules / 500 members (beyond → 422 CLASS_TOO_LARGE).");
        Console.WriteLine("CLASS_RANKING_SCALE_BEST_ZERO_TIES_GROUPS_LIMITS=PASS (isolated; no SQL/API)");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
