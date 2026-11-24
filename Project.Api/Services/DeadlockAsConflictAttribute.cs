using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Project.Application.Common;

namespace Project.Api.Services;

/// <summary>
/// Review T18: deadlock SQL Server (1205) giữa các transaction Serializable chạy đồng thời (lưu đoạn dùng chung ↔ lưu mã đề)
/// trả 409 CONCURRENCY_CONFLICT để người dùng thử lại, thay vì 500. Dữ liệu vẫn nhất quán (transaction bị hủy).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DeadlockAsConflictAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (!IsDeadlock(context.Exception)) return;
        context.Result = new ConflictObjectResult(ApiResponse<string>.Fail("CONCURRENCY_CONFLICT"));
        context.ExceptionHandled = true;
    }

    public static bool IsDeadlock(Exception? exception)
    {
        for (var e = exception; e != null; e = e.InnerException)
            if (e is SqlException { Number: 1205 }) return true;
        return false;
    }
}
