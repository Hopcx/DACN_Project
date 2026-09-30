using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;

namespace Project.Api.Extensions
{
    /// <summary>
    /// Extension method tách riêng cấu hình Authorization (policy-based).
    /// 
    /// CÁCH SỬ DỤNG:
    /// - B1: Trong Program.cs gọi: builder.Services.AddProjectAuthorization();
    /// - B2: Trên controller/action gắn [Authorize(Policy = "...")] theo tên policy tương ứng.
    /// 
    /// Ví dụ:
    ///     [Authorize(Policy = "ExamManagement")]       // cần PermissionId = 1 (Quản lý bài thi)
    ///     [Authorize(Policy = "QuestionManagement")]   // cần PermissionId = 2 (Quản lý câu hỏi & đáp án)
    ///     [Authorize(Policy = "SubjectManagement")]    // cần PermissionId = 3 (Quản lý môn học)
    ///     [Authorize(Policy = "ScheduleManagement")]   // cần PermissionId = 4 (Quản lý lịch thi)
    /// 
    /// Lưu ý:
    /// - Các policy này dựa trên claim "permission" bên trong JWT.
    /// - Claim "permission" được sinh trong JwtService.GenerateToken(...) với từng PermissionId của user.
    /// </summary>
    public static class AuthorizationExtensions
    {
        /// <summary>
        /// Đăng ký toàn bộ policy Authorization của hệ thống.
        /// Gọi một lần ở Program.cs: builder.Services.AddProjectAuthorization();
        /// </summary>
        public static IServiceCollection AddProjectAuthorization(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
                options.AddPolicy("AdminManagement", policy =>
                    policy.RequireAuthenticatedUser().RequireClaim("level_id", "1"));
                // 1: Quản lý bài thi
                options.AddPolicy("ExamManagement", policy =>
                    policy.RequireClaim("permission", "1"));

                // 2: Quản lý câu hỏi và đáp án
                options.AddPolicy("QuestionManagement", policy =>
                    policy.RequireClaim("permission", "2"));

                // 3: Quản lý môn học
                options.AddPolicy("SubjectManagement", policy =>
                    policy.RequireClaim("permission", "3"));

                // 4: Quản lý lịch thi
                options.AddPolicy("ScheduleManagement", policy =>
                    policy.RequireClaim("permission", "4"));
            });

            return services;
        }
    }
}
