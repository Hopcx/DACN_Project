
using Microsoft.EntityFrameworkCore;
using Project.Infrastructure.Persistence;
using Project.Application;
using Project.Infrastructure;
using System;
using Project.Api.Middlewares;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using Serilog.Events;
using Project.Api.Extensions;
using Project.Api.Filters;
using Project.Api.Services;
using System.Threading.RateLimiting;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Project.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // ===== CÁCH CŨ: Logging mặc định (đã comment) =====
            //var builder = WebApplication.CreateBuilder(args);

            // ===== CÁCH MỚI: Serilog logging có cấu trúc =====
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                Log.Information("Bắt đầu khởi chạy ứng dụng web");

                var builder = WebApplication.CreateBuilder(args);

                // Thay thế logging mặc định bằng Serilog
                builder.Host.UseSerilog();

                // Đăng ký services
                builder.Services.AddControllers();

                // ===== CÁCH CŨ: Không có FluentValidation và AutoMapper (đã comment) =====
                //builder.Services.AddApplication();
                //builder.Services.AddInfrastructure(builder.Configuration);

                // ===== CÁCH MỚI: Có FluentValidation và AutoMapper =====
                builder.Services.AddApplication();
                builder.Services.AddInfrastructure(builder.Configuration);
                builder.Services.AddSingleton<IVerificationEmailSender, VerificationEmailSender>();
                builder.Services.AddHostedService<ExpiredAccessTokenCleanupService>();
                builder.Services.AddScoped<AuthCsrfFilter>();
                builder.Services.AddAntiforgery(options =>
                {
                    options.HeaderName = "X-CSRF-TOKEN";
                    options.Cookie.Name = "__Secure-dacn-csrf";
                    options.Cookie.Path = "/web/auth";
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.Cookie.HttpOnly = true;
                });
                builder.Services.AddRateLimiter(options =>
                {
                    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                    options.AddPolicy("auth-public", context => RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
                    options.AddPolicy("auth-session", context => RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
                });

                // ===== CÁCH CŨ: Không có JWT Authentication (đã comment) =====
                //builder.Services.AddEndpointsApiExplorer();
                //builder.Services.AddSwaggerGen();

                // ===== CÁCH MỚI: JWT Authentication =====
                var jwtSettings = builder.Configuration.GetSection("JwtSettings");
                var secretKey = jwtSettings["SecretKey"]
                    ?? throw new InvalidOperationException("Chưa cấu hình JWT SecretKey");

                builder.Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                        ValidateIssuer = true,
                        ValidIssuer = jwtSettings["Issuer"],
                        ValidateAudience = true,
                        ValidAudience = jwtSettings["Audience"],
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = async context =>
                        {
                            var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                            var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                            if (string.IsNullOrWhiteSpace(jti) || !Guid.TryParse(userId, out var id))
                            {
                                context.Fail("JWT thiếu định danh phiên");
                                return;
                            }
                            var db = context.HttpContext.RequestServices.GetRequiredService<ProjectDACNDbContext>();
                            if (await db.BlackListTokens.AnyAsync(x => x.Token == jti) ||
                                !await db.Users.AnyAsync(x => x.Id == id && x.Status == 1 && x.EmailVerifiedAt != null))
                                context.Fail("Phiên không còn hợp lệ");
                        }
                    };
                });

                // Đăng ký Authorization theo Policy-based:
                // - Mỗi PermissionId sẽ map sang 1 Policy
                // - Khi gắn [Authorize(Policy = "ExamManagement")] framework sẽ tự kiểm tra claim "permission"
                // Tách cấu hình Authorization (policy-based) ra file Extension:
                // - Xem chi tiết tại: Project.Api/Extensions/AuthorizationExtensions.cs
                // - Tại đây chỉ cần gọi 1 dòng cho gọn Program.cs
                builder.Services.AddProjectAuthorization();

                // Đăng ký HttpContextAccessor + CurrentUserService để có hàm chung đọc thông tin user hiện tại
                builder.Services.AddHttpContextAccessor();
                builder.Services.AddScoped<Project.Application.Common.ICurrentUserService, Project.Api.Services.CurrentUserService>();

                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                    {
                        Title = "DACN Project API",
                        Version = "v1",
                        Description = "API cho dự án DACN sử dụng xác thực JWT"
                    });

                    // Cấu hình JWT Bearer cho Swagger
                    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Description = "Header Authorization theo chuẩn JWT. Nhập: 'Bearer {token}'",
                        Name = "Authorization",
                        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                        Scheme = "Bearer"
                    });

                    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
                });

                var app = builder.Build();
                if (!app.Environment.IsDevelopment())
                {
                    var allowedOrigins = app.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>();
                    if (allowedOrigins == null || allowedOrigins.Length == 0 ||
                        allowedOrigins.Any(x => !Uri.TryCreate(x, UriKind.Absolute, out var uri) || uri.Scheme != "https"))
                        throw new InvalidOperationException("Configure HTTPS Security:AllowedOrigins before startup");
                    var frontendUrl = app.Configuration["Mail:PublicFrontendUrl"];
                    if (!Uri.TryCreate(frontendUrl, UriKind.Absolute, out var frontendUri) || frontendUri.Scheme != "https")
                        throw new InvalidOperationException("Configure HTTPS Mail:PublicFrontendUrl before startup");
                    _ = app.Services.GetRequiredService<IVerificationEmailSender>();
                }

                // Cấu hình HTTP request pipeline
                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }

                // ===== CÁCH CŨ: Exception middleware đơn giản (đã comment) =====
                //app.UseMiddleware<ExceptionMiddleware>();
                //app.UseHttpsRedirection();
                //app.UseAuthorization();

                // ===== CÁCH MỚI: Exception middleware với custom exception và logging =====
                app.UseMiddleware<ExceptionMiddleware>();
                app.UseHttpsRedirection();
                app.UseRouting();
                app.UseRateLimiter();
                // Middleware xác thực và phân quyền
                app.UseAuthentication();
                app.UseAuthorization();

                app.MapControllers();

                Log.Information("Ứng dụng đã khởi động thành công");
                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Ứng dụng khởi động thất bại");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

    }
}
