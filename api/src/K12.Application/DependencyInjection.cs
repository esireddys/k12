using K12.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace K12.Application;

public static class DependencyInjection
{
    /// <summary>Registers every command and query handler in this assembly.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var handlerInterfaces = new[] { typeof(ICommandHandler<,>), typeof(IQueryHandler<,>) };
        var types = typeof(DependencyInjection).Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false });

        foreach (var type in types)
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && handlerInterfaces.Contains(iface.GetGenericTypeDefinition()))
                services.AddScoped(iface, type);
        }

        return services;
    }
}
