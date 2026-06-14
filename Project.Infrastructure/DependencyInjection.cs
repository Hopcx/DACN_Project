using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Project.Domain.Interfaces.ADO;
using Project.Domain.Interfaces.Repositories;
using Project.Infrastructure.Persistence;
using Project.Infrastructure.Persistence.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ProjectDACNDbContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString("Default"));
            });
            services.AddScoped<IADO, ProjectHopADO>();
            services.AddScoped<ILevelRepository, LevelRepository>();
            services.AddScoped<IRoomRepository, RoomRepository>();
            services.AddScoped<IPermissionRepository, PermissionRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IAnswerRepository, AnswerRepository>();
            services.AddScoped<ISubjectRepository, SubjectRepository>();
            services.AddScoped<IQuestionTypeRepository, QuestionTypeRepository>();
            services.AddScoped<IQuestionLevelRepository, QuestionLevelRepository>();
            services.AddScoped<IExamScheduleRepository, ExamScheduleRepository>();
            services.AddScoped<IAnswerSubmissionRepository, AnswerSubmissionRepository>();
            services.AddScoped<IClassRepository, ClassRepository>();
            services.AddScoped<IClassExamScheduleRepository, ClassExamScheduleRepository>();
            services.AddScoped<IClassUserRepository, ClassUserRepository>();
            services.AddScoped<IExamRepository, ExamRepository>();
            services.AddScoped<IExamDetailRepository, ExamDetailRepository>();
            services.AddScoped<IExamDetailQuestionRepository, ExamDetailQuestionRepository>();
            services.AddScoped<IExamActivityLogRepository, ExamActivityLogRepository>();
            services.AddScoped<IQuestionRepository, QuestionRepository>();
            services.AddScoped<ISubmissionRepository, SubmissionRepository>();
            services.AddScoped<ILogRepository, LogRepository>();
            services.AddScoped<IUserPermissionRepository, UserPermissionRepository>();
            return services;
        }
    }

}
