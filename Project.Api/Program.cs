
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
                });

                builder.Services.AddAuthorization();

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
