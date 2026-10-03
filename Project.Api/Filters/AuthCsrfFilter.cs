using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Project.Application.Common;

namespace Project.Api.Filters;

public class AuthCsrfFilter : IAsyncActionFilter
{
    private readonly IAntiforgery _antiforgery;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public AuthCsrfFilter(IAntiforgery antiforgery, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _antiforgery = antiforgery;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!HttpMethods.IsPost(context.HttpContext.Request.Method))
        {
            await next();
            return;
        }

        var request = context.HttpContext.Request;
        var origin = request.Headers.Origin.ToString();
        var allowed = _configuration.GetSection("Security:AllowedOrigins").Get<string[]>() ?? [];
        if (_environment.IsDevelopment())
            allowed = allowed.Concat(["https://localhost:5173"]).ToArray();
        if (string.IsNullOrWhiteSpace(origin) || !allowed.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            context.Result = new ObjectResult(ApiResponse<string>.Fail("Origin không hợp lệ")) { StatusCode = 403 };
            return;
        }

        try { await _antiforgery.ValidateRequestAsync(context.HttpContext); }
        catch (AntiforgeryValidationException)
        {
            context.Result = new ObjectResult(ApiResponse<string>.Fail("CSRF token không hợp lệ")) { StatusCode = 403 };
            return;
        }
        await next();
    }
}
