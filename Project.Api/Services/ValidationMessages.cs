using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Project.Api.Services;

/// <summary>Gom lỗi ModelState thành danh sách thông báo tiếng Việt, dịch các thông báo mặc định tiếng Anh.</summary>
public static class ValidationMessages
{
    private static readonly Regex Vietnamese = new(
        "[ạảãàáâấầẩẫậắằẳẵặẹẻẽèéêếềểễệỉĩịọỏõòóôốồổỗộơớờởỡợụủũùúưứừửữựỳỷỹýđă]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, string> Fields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fullName"] = "họ và tên", ["userName"] = "tên đăng nhập", ["email"] = "email", ["password"] = "mật khẩu",
        ["phoneNumber"] = "số điện thoại", ["address"] = "địa chỉ", ["dateOfBirth"] = "ngày sinh", ["sex"] = "giới tính",
        ["keyword"] = "tên đăng nhập/email", ["token"] = "mã xác minh"
    };

    public static string[] Collect(ModelStateDictionary state)
    {
        var result = new List<string>();
        foreach (var (key, entry) in state)
        {
            foreach (var error in entry.Errors)
            {
                var text = error.ErrorMessage;
                if (string.IsNullOrWhiteSpace(text) || !Vietnamese.IsMatch(text)) text = Translate(key, text);
                if (!result.Contains(text)) result.Add(text);
            }
        }
        const string generic = "Dữ liệu gửi lên không hợp lệ. Vui lòng kiểm tra lại.";
        if (result.Count > 1) result.Remove(generic);
        if (result.Count == 0) result.Add("Dữ liệu gửi lên không hợp lệ. Vui lòng kiểm tra lại.");
        return result.ToArray();
    }

    private static string Translate(string key, string english)
    {
        var name = Fields.TryGetValue(key.TrimStart('$', '.'), out var friendly) ? friendly : null;
        if (english.Contains("required", StringComparison.OrdinalIgnoreCase) && name != null) return $"Vui lòng nhập {name}.";
        if (english.Contains("could not be converted", StringComparison.OrdinalIgnoreCase) && name != null)
            return $"Giá trị {name} không đúng định dạng.";
        return name != null ? $"Giá trị {name} không hợp lệ." : "Dữ liệu gửi lên không hợp lệ. Vui lòng kiểm tra lại.";
    }
}
