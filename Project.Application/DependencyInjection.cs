using Project.Application.Interfaces.Services;
using Project.Application.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Project.Domain.Interfaces.Repositories;

namespace Project.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(
            this IServiceCollection services)
        {
            services.AddScoped<ILevelService, LevelService>();
            services.AddScoped<IRoomService, RoomService>();
            services.AddScoped<IPermissionService, PermissionService>();
            return services;
        }
    }

}
