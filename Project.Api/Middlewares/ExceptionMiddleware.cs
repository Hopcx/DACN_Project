using Project.Application.Common;
using Project.Application.Exceptions;
using System.Net;
using System.Text.Json;

namespace Project.Api.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // ===== CÁCH CŨ: Xử lý exception đơn giản (đã comment) =====
                //context.Response.StatusCode = 500;
                //context.Response.ContentType = "application/json";
                //var response = ApiResponse<string>.Fail($"Internal server error - {ex.Message}");
                //await context.Response.WriteAsJsonAsync(response);

                // ===== CÁCH MỚI: Xử lý exception với custom exceptions và logging =====
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            var response = context.Response;

            var responseModel = ApiResponse<string>.Fail(exception.Message);

            switch (exception)
            {
                case ValidationException ex:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    responseModel = ApiResponse<string>.Fail("Dữ liệu không hợp lệ");
                    // Có thể thêm chi tiết lỗi validation vào responseModel
                    _logger.LogWarning(ex, "Xảy ra lỗi validation");
                    break;

                case BadRequestException ex:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    responseModel = ApiResponse<string>.Fail(ex.Message);
                    _logger.LogWarning(ex, "Yêu cầu không hợp lệ");
                    break;

                case NotFoundException ex:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    responseModel = ApiResponse<string>.Fail(ex.Message);
                    _logger.LogWarning(ex, "Không tìm thấy tài nguyên");
                    break;

                default:
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    responseModel = ApiResponse<string>.Fail("Đã xảy ra lỗi trong quá trình xử lý yêu cầu");
                    _logger.LogError(exception, "Xảy ra lỗi không xác định");
                    break;
            }

            var result = JsonSerializer.Serialize(responseModel);
            await response.WriteAsync(result);
        }

    }

}
