using K12.Application.Abstractions;
using K12.Domain.Content;
using K12.Infrastructure.Persistence;
using K12.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;

namespace K12.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IQuestionRepository, InMemoryQuestionRepository>();
        return services;
    }
}
