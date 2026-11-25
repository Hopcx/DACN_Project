using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Project.Api.Controllers;

/// <summary>
/// Đợt tổng kiểm (QTK-2): mọi route [Http*] của các controller To-be T1–T18 phải có dòng `METHOD /web/...` trong
/// docs/agent-rules/04-contracts/07-tobe-endpoints.md — thêm route mà quên tài liệu thì smoke này báo; ngược lại dòng
/// "- `VERB /web...`" không còn route cũng báo. Danh sách controller viết cứng: controller To-be mới phải thêm vào đây.
/// </summary>
internal static class EndpointDocSmoke
{
    private static readonly Type[] Controllers =
    [
        typeof(QuestionController), typeof(QuestionImportController), typeof(QuestionExportController), typeof(QuestionTypeController),
        typeof(ChapterController), typeof(TagController), typeof(MediaController), typeof(QuestionMediaController), typeof(AdminMediaController),
        typeof(QuestionGroupController), typeof(ExamController), typeof(StudentAttemptController), typeof(ReviewFlagController),
        typeof(PracticeController), typeof(StudentClassController), typeof(StudentAnalysisController), typeof(TeacherClassController),
    ];

    public static void Run()
    {
        var doc = File.ReadAllText(FindDoc());
        var missing = new List<string>();
        var documented = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;
        foreach (var controller in Controllers)
        {
            // Chỉ hỗ trợ đúng các dạng route đang dùng: một [Route] tương đối trên controller, template trên [Http*]
            // (tương đối hoặc bắt đầu bằng "/"). Gặp dạng khác thì báo để sửa smoke thay vì ghép sai path.
            var routes = controller.GetCustomAttributes<RouteAttribute>(inherit: true).ToList();
            if (routes.Count > 1 || controller.BaseType != typeof(ControllerBase))
                throw new Exception($"TOBE_ENDPOINT_DOC: {controller.Name} có nhiều [Route] hoặc kế thừa controller khác — smoke chưa hỗ trợ.");
            var prefix = Normalize(routes.FirstOrDefault()?.Template ?? "", controller.Name);
            foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (method.GetCustomAttributes<RouteAttribute>().Any())
                    throw new Exception($"TOBE_ENDPOINT_DOC: {controller.Name}.{method.Name} có [Route] trên action — smoke chưa hỗ trợ.");
                foreach (var http in method.GetCustomAttributes<HttpMethodAttribute>())
                {
                    var template = http.Template ?? "";
                    var path = template.StartsWith('/') || template.StartsWith("~/")
                        ? "/" + Normalize(template, controller.Name)
                        : "/" + prefix + (template.Length > 0 ? "/" + Normalize(template, controller.Name) : "");
                    foreach (var verb in http.HttpMethods)
                    {
                        count++;
                        var line = $"`{verb} {path}`";
                        documented.Add(line);
                        if (!doc.Contains(line, StringComparison.Ordinal)) missing.Add(line);
                    }
                }
            }
        }
        if (missing.Count > 0) throw new Exception("TOBE_ENDPOINT_DOC: thiếu trong 07-tobe-endpoints.md: " + string.Join(", ", missing));
        // Chiều ngược lại: mọi dòng "- `VERB /web...`" trong tài liệu phải ứng với một route thật (bắt dòng tài liệu cũ)
        var stale = System.Text.RegularExpressions.Regex.Matches(doc, @"(?m)^- (`(?:GET|POST|PUT|DELETE|PATCH) /web[^`]*`)")
            .Select(m => m.Groups[1].Value).Where(x => !documented.Contains(x)).ToList();
        if (stale.Count > 0) throw new Exception("TOBE_ENDPOINT_DOC: tài liệu có route không còn trong code: " + string.Join(", ", stale));
        if (count < 80) throw new Exception($"TOBE_ENDPOINT_DOC: chỉ thấy {count} route — danh sách controller có vẻ sai.");
        Console.WriteLine($"TOBE_ENDPOINT_DOC={count}_ROUTES_DOCUMENTED=PASS (isolated; no SQL/API)");
    }

    // Bỏ "~", "/" ở hai đầu; token [controller]/[action] chưa hỗ trợ
    private static string Normalize(string template, string controller)
    {
        if (template.Contains('[')) throw new Exception($"TOBE_ENDPOINT_DOC: {controller} dùng token route '{template}' — smoke chưa hỗ trợ.");
        return template.TrimStart('~').Trim('/');
    }

    private static string FindDoc()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                var path = Path.Combine(dir.FullName, "docs", "agent-rules", "04-contracts", "07-tobe-endpoints.md");
                if (File.Exists(path)) return path;
            }
        throw new Exception("TOBE_ENDPOINT_DOC: không tìm thấy docs/agent-rules/04-contracts/07-tobe-endpoints.md");
    }
}
