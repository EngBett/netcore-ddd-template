using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Template.Application.Interfaces;
using Template.Common.Options;
using Template.Infrastructure.DataAccess;

namespace Template.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisOptions>(configuration.GetSection(nameof(RedisOptions)));
        services.AddSingleton<IConfigureOptions<RedisCacheOptions>, RedisCacheOptionsConfigurator>();
        services.AddStackExchangeRedisCache(_ => { });

        services.AddDbContext(configuration);
        services.AddAuthentication(configuration);
        return services;
    }

    private static void AddDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["DATABASE_CON"]
                               ?? throw new InvalidOperationException("DATABASE_CON must be set in configuration.");
        var databaseKind = configuration["DatabaseKind"] ?? "mssql";

        services.AddDbContext<ApplicationContext>((_, options) =>
        {
            switch (databaseKind.ToLowerInvariant())
            {
                //#if (useMssql)
                case "mssql":
                    options.UseSqlServer(connectionString);
                    break;
                //#endif
                //#if (usePostgres)
                case "postgres":
                    options.UseNpgsql(connectionString);
                    break;
                //#endif
                //#if (useSqlite)
                case "sqlite":
                    options.UseSqlite(connectionString);
                    break;
                //#endif
                //#if (useMysql)
                case "mysql":
                    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)));
                    break;
                //#endif
                default:
                    throw new InvalidOperationException(
                        $"DatabaseKind '{databaseKind}' is not available in this service. "
                        + "Only the provider selected when scaffolding is referenced.");
            }
        });

        services.AddScoped<IApplicationContext>(sp => sp.GetRequiredService<ApplicationContext>());
    }
    
    private static void AddAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var appSettingsSection = configuration.GetSection("ApplicationOptions");
        services.Configure<ApplicationOptions>(appSettingsSection);
        var appSettings = appSettingsSection.Get<ApplicationOptions>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = appSettings!.Authority;
                // options.RequireHttpsMetadata = true;
                // name of the API resource
                options.Audience = appSettings.Audience;
                // options.MetadataAddress = appSettings.MetadataAddress;
                options.BackchannelHttpHandler = new HttpClientHandler { ServerCertificateCustomValidationCallback = delegate { return true; } };
            });
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build());
    }
}