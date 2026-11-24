using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Project.Application.DTOs.AttemptDTO;
using Project.Domain.ValueObjects;

namespace Project.Application.Services;

/// <summary>
/// To-be T16 (Q10a, QT16-3): so khớp câu điền ngắn. Hàm thuần.
/// Luôn: Unicode NFC, trim, gộp khoảng trắng, không phân biệt hoa/thường. Tùy chọn theo câu: bỏ dấu tiếng Việt (đ = d);
/// so số (dấu thập phân "," hoặc ".", không nhận phân cách nghìn/phân số/biểu thức; |a − b| ≤ sai số).
/// Đáp án chấp nhận không phải số thì luôn so như chữ. Bài làm khớp một đáp án bất kỳ là đúng.
/// </summary>
public static partial class ShortAnswerMatcher
{
    public const int QuestionTypeId = QuestionKinds.ShortAnswer;   // review T17: một nguồn cho Id loại câu
    public const int MaxTextLength = 200;
    public const int MaxAccepted = 10;
    public const decimal MaxTolerance = 1_000_000_000m;

    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();
    [GeneratedRegex(@"^[+-]?\d{1,18}(?:[.,]\d{1,9})?$")] private static partial Regex NumberShape();

    public static string Normalize(string? text, bool ignoreDiacritics)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var value = Spaces().Replace(text.Normalize(NormalizationForm.FormC).Trim(), " ").ToLowerInvariant();
        if (!ignoreDiacritics) return value;
        value = value.Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static bool TryParseNumber(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        return NumberShape().IsMatch(trimmed) &&
            decimal.TryParse(trimmed.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Review T16: khóa gộp bài làm cho thống kê (outcome, báo cáo lớp): bật so số và đọc được số → số dạng chuẩn ("3,50" = "3.5");
    /// còn lại → chữ đã chuẩn hóa. Tối đa <see cref="MaxTextLength"/> ký tự, không cắt đôi cặp surrogate.
    /// </summary>
    public static string? StatsKey(AttemptShortAnswerSnapshot? rule, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var key = rule?.Numeric == true && TryParseNumber(text, out var number)
            ? number.ToString("0.#########", CultureInfo.InvariantCulture)
            : Normalize(text, rule?.IgnoreDiacritics == true);
        if (key.Length <= MaxTextLength) return key;
        var cut = char.IsHighSurrogate(key[MaxTextLength - 1]) ? MaxTextLength - 1 : MaxTextLength;
        return key[..cut];
    }

    public static bool Matches(AttemptShortAnswerSnapshot? rule, IEnumerable<string> accepted, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        rule ??= new AttemptShortAnswerSnapshot(false, false, 0);
        var list = accepted.ToList();
        if (rule.Numeric && TryParseNumber(text, out var given))
            foreach (var item in list)
                if (TryParseNumber(item, out var expected) && Math.Abs(given - expected) <= rule.Tolerance) return true;
        var normalized = Normalize(text, rule.IgnoreDiacritics);
        return list.Any(item => Normalize(item, rule.IgnoreDiacritics) == normalized);
    }
}

/// <summary>
/// To-be T16: một quy tắc đúng/sai cho mọi nơi chấm (điểm, kiểm tra từng câu, outcome, báo cáo) — tất cả-hoặc-không (Q3, Q10b).
/// To-be T17: ghép cặp (mọi vế trái ghép đúng vế phải) và nhiều mệnh đề (mọi mệnh đề chọn đúng Đúng/Sai); thiếu mục = sai.
/// Câu trả lời đã qua <see cref="AttemptSnapshotFactory.ValidateDraft"/>.
/// </summary>
public static class AnswerGrading
{
    public static bool IsShortAnswer(AttemptQuestionSnapshot question) => question.QuestionTypeId == ShortAnswerMatcher.QuestionTypeId;
    public static bool HasItems(AttemptQuestionSnapshot question) => QuestionKinds.HasItems(question.QuestionTypeId);

    public static bool IsAnswered(AttemptQuestionSnapshot question, AttemptAnswerDto? answer) => answer != null &&
        (IsShortAnswer(question) ? !string.IsNullOrWhiteSpace(answer.Text)
            : HasItems(question) ? (answer.Items?.Count ?? 0) > 0
            : answer.SelectedAnswerIds.Count > 0);

    public static bool IsCorrect(AttemptQuestionSnapshot question, AttemptAnswerDto? answer)
    {
        if (!IsAnswered(question, answer)) return false;
        if (IsShortAnswer(question))
            return ShortAnswerMatcher.Matches(question.ShortAnswer, question.Options.Where(x => x.IsCorrect).Select(x => x.Content), answer!.Text);
        if (HasItems(question))
        {
            var items = answer!.Items!.GroupBy(x => x.OptionId).ToDictionary(g => g.Key, g => g.First());
            return items.Count == question.Options.Count && question.Options.All(option => items.TryGetValue(option.Id, out var item) &&
                (question.QuestionTypeId == QuestionKinds.Matching
                    ? item.MatchId != null && item.MatchId == option.MatchId
                    : item.Value != null && item.Value == option.IsCorrect));
        }
        var correct = question.Options.Where(x => x.IsCorrect).Select(x => x.Id).ToHashSet();
        return answer!.SelectedAnswerIds.ToHashSet().SetEquals(correct);
    }

    /// <summary>Đáp án chấp nhận của câu điền ngắn (chỉ dùng khi được phép xem đáp án).</summary>
    public static IReadOnlyList<string> AcceptedAnswers(AttemptQuestionSnapshot question) =>
        IsShortAnswer(question) ? Array.AsReadOnly(question.Options.Where(x => x.IsCorrect).Select(x => x.Content).ToArray()) : [];

    /// <summary>T17: câu trả lời đúng từng mục (ghép cặp: MatchId; nhiều mệnh đề: Value) — chỉ dùng khi được phép xem đáp án.</summary>
    public static IReadOnlyList<AttemptItemDto>? CorrectItems(AttemptQuestionSnapshot question) => question.QuestionTypeId switch
    {
        QuestionKinds.Matching => Array.AsReadOnly(question.Options.Select(x => new AttemptItemDto(x.Id, x.MatchId)).ToArray()),
        QuestionKinds.Statements => Array.AsReadOnly(question.Options.Select(x => new AttemptItemDto(x.Id, null, x.IsCorrect)).ToArray()),
        _ => null
    };
}
