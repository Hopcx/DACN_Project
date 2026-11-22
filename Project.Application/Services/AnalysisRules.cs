namespace Project.Application.Services;

/// <summary>
/// To-be T11 (Q13a): ngưỡng xếp loại theo tỉ lệ đúng (%), cấu hình ở appsettings <c>Analysis:*</c>.
/// Yếu &lt; WeakBelow ≤ Cần cải thiện &lt; ImproveBelow ≤ Khá &lt; GoodFrom ≤ Tốt; dưới MinQuestions câu: chưa đủ dữ liệu.
/// </summary>
public sealed record AnalysisThresholds(decimal WeakBelow = 50, decimal ImproveBelow = 70, decimal GoodFrom = 85, int MinQuestions = 5,
    decimal HardBelow = 40, int HardMinAnswers = 10)
{
    /// <summary>Ném lỗi khi cấu hình sai (gọi lúc khởi động để dừng sớm).</summary>
    public AnalysisThresholds Validate()
    {
        if (!(WeakBelow > 0 && WeakBelow < ImproveBelow && ImproveBelow < GoodFrom && GoodFrom <= 100))
            throw new InvalidOperationException("Analysis thresholds must satisfy 0 < WeakBelow < ImproveBelow < GoodFrom <= 100.");
        if (MinQuestions is < 1 or > 1000) throw new InvalidOperationException("Analysis:MinQuestions must be 1..1000.");
        // To-be T12: "câu hay sai" — tỉ lệ đúng < HardBelow với ≥ HardMinAnswers lượt trả lời (Q13b)
        if (!(HardBelow > 0 && HardBelow <= 100)) throw new InvalidOperationException("Analysis:HardBelow must be in (0, 100].");
        if (HardMinAnswers is < 1 or > 100000) throw new InvalidOperationException("Analysis:HardMinAnswers must be 1..100000.");
        return this;
    }
}

public static class AnalysisRules
{
    public const string Insufficient = "insufficient";
    public const string Weak = "weak";
    public const string NeedsImprovement = "needsImprovement";
    public const string Fair = "fair";
    public const string Good = "good";

    /// <summary>Tỉ lệ đúng (%) làm tròn 1 chữ số; null khi chưa có câu.</summary>
    public static decimal? Rate(int correct, int total) =>
        total <= 0 ? null : decimal.Round(100m * correct / total, 1, MidpointRounding.ToEven);

    /// <summary>Xếp loại trên tỉ lệ chưa làm tròn (tránh làm tròn đẩy qua ngưỡng).</summary>
    public static string Classify(int correct, int total, AnalysisThresholds t)
    {
        if (total < t.MinQuestions || total <= 0) return Insufficient;
        var rate = 100m * correct / total;
        return rate < t.WeakBelow ? Weak : rate < t.ImproveBelow ? NeedsImprovement : rate < t.GoodFrom ? Fair : Good;
    }
}
