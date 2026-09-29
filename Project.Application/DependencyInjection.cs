using Project.Application.Interfaces.Services;
using Project.Application.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Project.Domain.Interfaces.Repositories;
using FluentValidation;
using FluentValidation.AspNetCore;
using System.Reflection;
using Project.Domain.Interfaces.ADO;
// ===== CÁCH CŨ: Có thể gây conflict (đã comment) =====
//using AutoMapper;

namespace Project.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(
            this IServiceCollection services)
        {
            // Register services
            services.AddScoped<ILevelService, LevelService>();
            services.AddScoped<IRoomService, RoomService>();
            services.AddScoped<IPermissionService, PermissionService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<IAnswerService, AnswerService>();
            services.AddScoped<ISubjectService, SubjectService>();
            services.AddScoped<IQuestionTypeService, QuestionTypeService>();
            services.AddScoped<IQuestionLevelService, QuestionLevelService>();
            services.AddScoped<IExamScheduleService, ExamScheduleService>();
            services.AddScoped<IAnswerSubmissionService, AnswerSubmissionService>();
            services.AddScoped<IClassService, ClassService>();
            services.AddScoped<IExamService, ExamService>();
            services.AddScoped<IQuestionService, QuestionService>();
            services.AddScoped<ISubmissionService, SubmissionService>();
            services.AddScoped<IClassExamScheduleService, ClassExamScheduleService>();
            services.AddScoped<IClassUserService, ClassUserService>();
            services.AddScoped<IExamActivityLogService, ExamActivityLogService>();
            services.AddScoped<IExamDetailService, ExamDetailService>();
            services.AddScoped<IExamDetailQuestionService, ExamDetailQuestionService>();
            services.AddScoped<ILogService, LogService>();
            services.AddScoped<IUserPermissionService, UserPermissionService>();

            // Register FluentValidation
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
            services.AddFluentValidationAutoValidation();
            services.AddFluentValidationClientsideAdapters();

            // ===== CÁCH CŨ: Có thể gây ambiguous call error (đã comment) =====
            //services.AddAutoMapper(Assembly.GetExecutingAssembly());

            // ===== CÁCH MỚI: Register AutoMapper với type cụ thể để tránh ambiguous call =====
            services.AddAutoMapper(typeof(Mappings.MappingProfile));

            return services;
        }
    }

}
