using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Project.Api.Controllers;
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
services.AddApplication();
services.AddInfrastructure(configuration);

using var provider = services.BuildServiceProvider(validateScopes: true);
using var firstScope = provider.CreateScope();
var first = firstScope.ServiceProvider;

var serviceTypes = new[]
{
    typeof(IClassExamScheduleService), typeof(IClassUserService),
    typeof(IExamActivityLogService), typeof(IExamDetailService),
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
    typeof(RoomController), typeof(SubmissionController)
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

Console.WriteLine($"DI smoke passed: {serviceTypes.Length} services, {controllerTypes.Length} controllers, scoped IADO/repository. No SQL connection attempted.");
