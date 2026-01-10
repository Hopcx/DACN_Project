using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            services.AddScoped<ILevelRepository, LevelRepository>();
            services.AddScoped<IRoomRepository, RoomRepository>();
            services.AddScoped<IPermissionRepository, PermissionRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            return services;
        }
    }

}
