using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Project.Api.Extensions;
using Project.Application.DTOs.UserDTO;
using Project.Application.DTOs.AnswerCreateDto;
using System.Security.Claims;
using System.Text.Json;
using Project.Api.Controllers;
using Project.Api.Services;
using Project.Application;
using Project.Application.Interfaces.Services;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using Project.Infrastructure;

var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:Default"] = "Server=localhost;Database=DiSmoke;Trusted_Connection=True;TrustServerCertificate=True"
    })
    .Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddLogging();
services.AddSingleton<IVerificationEmailSender, SmokeEmailSender>();
services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment, SmokeWebHostEnvironment>();
services.AddProjectAuthorization();
services.AddApplication();
services.AddInfrastructure(configuration);
services.AddScoped<IExamVariantService, ExamVariantStore>();

using var provider = services.BuildServiceProvider(validateScopes: true);
using var firstScope = provider.CreateScope();
var first = firstScope.ServiceProvider;

var serviceTypes = new[]
{
    typeof(IClassExamScheduleService), typeof(IClassUserService),
    typeof(IExamActivityLogService), typeof(IExamDetailService),
    typeof(IExamVariantService),
    typeof(IExamDetailQuestionService), typeof(ILogService),
    typeof(IUserPermissionService)
};

foreach (var serviceType in serviceTypes)
    _ = first.GetRequiredService(serviceType);

var controllerTypes = new[]
{
    typeof(ClassExamScheduleController), typeof(ClassUserController),
    typeof(ExamActivityLogController), typeof(ExamDetailController),
    typeof(ExamDetailQuestionController), typeof(LogController),
    typeof(UserPermissionController), typeof(UserController),
    typeof(RoomController), typeof(SubmissionController),
    typeof(ExamScheduleController), typeof(StudentScheduleController)
    , typeof(QuestionController), typeof(AnswerController), typeof(ExamController)
};

foreach (var controllerType in controllerTypes)
    _ = ActivatorUtilities.CreateInstance(first, controllerType);

var ado = first.GetRequiredService<IADO>();
if (!ReferenceEquals(ado, first.GetRequiredService<IADO>()))
    throw new Exception("IADO must be reused within one scope.");

var userRepository = first.GetRequiredService<IUserRepository>();
if (!ReferenceEquals(userRepository, first.GetRequiredService<IUserRepository>()))
    throw new Exception("Repository must be reused within one scope.");

using var secondScope = provider.CreateScope();
if (ReferenceEquals(ado, secondScope.ServiceProvider.GetRequiredService<IADO>()))
    throw new Exception("IADO must differ between scopes.");
if (ReferenceEquals(userRepository, secondScope.ServiceProvider.GetRequiredService<IUserRepository>()))
    throw new Exception("Repository must differ between scopes.");

var authorization = first.GetRequiredService<IAuthorizationService>();
var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
var student = Principal("4");
var admin = Principal("1");
var examiner = Principal("2", "1");
if ((await authorization.AuthorizeAsync(anonymous, null, "AdminManagement")).Succeeded ||
    (await authorization.AuthorizeAsync(student, null, "AdminManagement")).Succeeded ||
    !(await authorization.AuthorizeAsync(admin, null, "AdminManagement")).Succeeded ||
    (await authorization.AuthorizeAsync(student, null, "ExamManagement")).Succeeded ||
    !(await authorization.AuthorizeAsync(examiner, null, "ExamManagement")).Succeeded)
    throw new Exception("Authorization policy contract failed.");

var options = first.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthorizationOptions>>().Value;
if (options.FallbackPolicy is null ||
    (await authorization.AuthorizeAsync(anonymous, null, options.FallbackPolicy)).Succeeded)
    throw new Exception("Anonymous fallback authorization failed.");
var evaluator = first.GetRequiredService<IPolicyEvaluator>();
var adminPolicy = options.GetPolicy("AdminManagement")!;
var unauthenticatedResult = await evaluator.AuthorizeAsync(
    adminPolicy, AuthenticateResult.NoResult(), new DefaultHttpContext(), null);
var studentContext = new DefaultHttpContext { User = student };
var studentTicket = new AuthenticationTicket(student, "test");
var forbiddenResult = await evaluator.AuthorizeAsync(
    adminPolicy, AuthenticateResult.Success(studentTicket), studentContext, null);
if (!unauthenticatedResult.Challenged || !forbiddenResult.Forbidden)
    throw new Exception("Expected 401 challenge and 403 forbid decisions.");

var protectedControllers = typeof(UserController).Assembly.GetTypes()
    .Where(t => t.IsSubclassOf(typeof(ControllerBase)) && t != typeof(AuthController) && t != typeof(RegistrationController));
foreach (var controller in protectedControllers)
    if (controller != typeof(WeatherForecastController) &&
        controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Length == 0)
        throw new Exception($"Controller lacks explicit policy: {controller.Name}");

foreach (var method in new[] { "Create", "Update", "Delete" })
    foreach (var controller in new[] { typeof(SubmissionController), typeof(AnswerSubmissionController) })
        if (controller.GetMethod(method)?.GetCustomAttributes(typeof(NonActionAttribute), true).Length != 1)
            throw new Exception($"Unsafe write route remains active: {controller.Name}.{method}");

if (JsonSerializer.Serialize(new UserResponseDto()).Contains("PasswordHash", StringComparison.OrdinalIgnoreCase))
    throw new Exception("Password hash leaked from public user DTO.");
var attemptAnswer = AnswerForAttemptDto.Projection.Compile()(new Project.Domain.Entities.Answer
{
    Id = 5, QuestionId = 6, Content = "Option", IsCorrect = true
});
if (JsonSerializer.Serialize(attemptAnswer).Contains("IsCorrect", StringComparison.OrdinalIgnoreCase))
    throw new Exception("Student answer projection leaked correctness.");

if ((await authorization.AuthorizeAsync(student, null, "QuestionManagement")).Succeeded ||
    !(await authorization.AuthorizeAsync(Principal("2", "2"), null, "QuestionManagement")).Succeeded)
    throw new Exception("Question permission contract failed.");
await QuestionValidationSmoke.Run();

Console.WriteLine($"DI smoke passed: {serviceTypes.Length} services, {controllerTypes.Length} controllers, scoped IADO/repository. No SQL connection attempted.");
Console.WriteLine("Security contract passed: fallback, admin and exam permissions, controller policies, closed submission writes, user DTO.");

static ClaimsPrincipal Principal(string level, params string[] permissions)
{
    var claims = new List<Claim> { new("level_id", level) };
    claims.AddRange(permissions.Select(p => new Claim("permission", p)));
    return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
}

sealed class SmokeEmailSender : IVerificationEmailSender
{
    public Task SendAsync(string recipient, string verificationUrl) => Task.CompletedTask;
}

sealed class SmokeWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "DependencyInjectionSmoke";
    public string ContentRootPath { get; set; } = ".";
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    public string WebRootPath { get; set; } = ".";
    public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
}
