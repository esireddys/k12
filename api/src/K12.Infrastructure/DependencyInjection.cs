using K12.Application.Abstractions;
using K12.Domain.Content;
using K12.Infrastructure.Persistence;
using K12.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace K12.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Uses SQL Server when ConnectionStrings:K12 is set, otherwise an in-memory store
    /// (handy for a quick run with no database).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();

        var connectionString = configuration.GetConnectionString("K12");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IQuestionRepository, InMemoryQuestionRepository>();
            services.AddSingleton<IUnitOfWork, InMemoryUnitOfWork>();
            return services;
        }

        services.AddDbContext<K12DbContext>(o => o.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<K12DbContext>());
        services.AddScoped<IQuestionRepository, EfQuestionRepository>();
        return services;
    }
}
