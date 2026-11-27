using K12.Application.Abstractions;
using K12.Application.Accounts;
using K12.Application.ContentReview;
using K12.Infrastructure.Content;
using K12.Domain.Accounts;
using K12.Domain.Content;
using K12.Domain.Curriculum;
using K12.Domain.Progress;
using K12.Infrastructure.Identity;
using K12.Infrastructure.Persistence;
using K12.Infrastructure.Security;
using K12.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        services.AddSingleton<IPinHasher, Pbkdf2PinHasher>();

        // Item pictures: a local folder for now (the pipeline's data/out); Blob Storage when the cloud store is added.
        var imageRoot = configuration["Content:ImageRoot"];
        if (string.IsNullOrWhiteSpace(imageRoot)) services.AddSingleton<IItemImageStore, NoItemImageStore>();
        else services.AddSingleton<IItemImageStore>(new FileSystemItemImageStore(imageRoot));

        // Sign-in providers. Google needs only its public client id (Auth:Google:ClientId, set with user-secrets locally).
        services.AddSingleton<IExternalLoginVerifier>(sp => new GoogleLoginVerifier(
            configuration["Auth:Google:ClientId"], sp.GetRequiredService<ILogger<GoogleLoginVerifier>>()));

        var connectionString = configuration.GetConnectionString("K12");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IQuestionRepository, InMemoryQuestionRepository>();
            services.AddSingleton<ICourseRepository, InMemoryCourseRepository>();
            services.AddSingleton<ISkillRepository, InMemorySkillRepository>();
            services.AddSingleton<IExamFormRepository, InMemoryExamFormRepository>();
            services.AddSingleton<IPassageRepository, InMemoryPassageRepository>();
            services.AddSingleton<IParentAccountRepository, InMemoryParentAccountRepository>();
            services.AddSingleton<IKidProgressRepository, InMemoryKidProgressRepository>();
            services.AddSingleton<IPracticeSessionRepository, InMemoryPracticeSessionRepository>();
            services.AddSingleton<IUnitOfWork, InMemoryUnitOfWork>();
            return services;
        }

        services.AddDbContext<K12DbContext>(o => o.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<K12DbContext>());
        services.AddScoped<IQuestionRepository, EfQuestionRepository>();
        services.AddScoped<ICourseRepository, EfCourseRepository>();
        services.AddScoped<ISkillRepository, EfSkillRepository>();
        services.AddScoped<IExamFormRepository, EfExamFormRepository>();
        services.AddScoped<IPassageRepository, EfPassageRepository>();
        services.AddScoped<IParentAccountRepository, EfParentAccountRepository>();
        services.AddScoped<IKidProgressRepository, EfKidProgressRepository>();
        services.AddScoped<IPracticeSessionRepository, EfPracticeSessionRepository>();
        return services;
    }
}
