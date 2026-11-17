using System.Globalization;
using System.Text;

namespace Project.Application.Common;

/// <summary>Chuẩn hóa tên tag để so trùng (To-be T2): trim, gộp khoảng trắng, chữ thường, bỏ dấu tiếng Việt (gồm đ → d).
/// Không đổi ký tự khác (vd. '-' giữ nguyên), nên "hay sai" và "hay-sai" là hai tag khác nhau.</summary>
public static class TagName
{
    public const int MaxLength = 50;

    public static string Clean(string? name) =>
        string.Join(' ', (name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string Normalize(string? name)
    {
        var decomposed = Clean(name).ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) builder.Append(ch);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
