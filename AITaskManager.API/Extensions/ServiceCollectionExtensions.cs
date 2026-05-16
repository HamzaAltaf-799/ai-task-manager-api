using System.Text;
using AITaskManager.API.Auth;
using AITaskManager.API.Configurations;
using AITaskManager.API.Data;
using AITaskManager.API.Interfaces;
using AITaskManager.API.Repositories;
using AITaskManager.API.Services;

namespace AITaskManager.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration config)
    {
        // Settings
        services.Configure<JwtSettings>(config.GetSection("JwtSettings"));
        services.Configure<OpenAiSettings>(config.GetSection("OpenAI"));

        // Data
        services.AddSingleton<InMemoryDatabase>(sp =>
        {
            var db = new InMemoryDatabase();
            db.Seed();
            return db;
        });

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();

        // Auth
        services.AddScoped<JwtTokenService>();

        // AI — use real service only when API key is configured
        var openAiKey = config["OpenAI:ApiKey"];
        if (!string.IsNullOrWhiteSpace(openAiKey) && openAiKey != "your-openai-key-here")
            services.AddScoped<IAiService, OpenAiService>();
        else
            services.AddScoped<IAiService, StubAiService>();

        services.AddHttpClient("openai");

        // App services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddSingleton<ICacheService, CacheService>();

        return services;
    }

    public static IServiceCollection AddOpenApiDocs(this IServiceCollection services)
    {
        // Built-in minimal OpenAPI — available in ASP.NET Core 8 without Swashbuckle
        services.AddEndpointsApiExplorer();
        return services;
    }
}
