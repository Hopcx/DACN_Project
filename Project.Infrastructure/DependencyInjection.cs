using Microsoft.Extensions.DependencyInjection;
using Project.Domain.Interfaces.Repositories;
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
            this IServiceCollection services)
        {
            services.AddScoped<ILevelRepository, LevelRepository>();
            return services;
        }
    }

}
