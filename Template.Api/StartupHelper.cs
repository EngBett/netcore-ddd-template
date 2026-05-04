using Template.Application;
using Template.Application.Interfaces;
using Template.Infrastructure.DataAccess;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.Infrastructure;
using Prometheus;
using StackExchange.Redis;
using Template.Common.Models;
using Template.Api.Filters;
using Template.Api.Services;
using Template.Common.Options;

namespace Template.Api;

public static class StartupHelper
{
    private static IConnectionMultiplexer? _connectionMultiplexer = null;
    public static void ConfigureServices(this IServiceCollection services, IConfiguration config)
    {
        _connectionMultiplexer = ConnectionMultiplexer.Connect(config.GetValue<string>("Redis")!);
        
        services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

        services.AddControllers(opt => { opt.Filters.Add(typeof(GlobalExceptionFilter)); });

        services.AddHttpContextAccessor();
        services.AddHealthChecks();
        services.AddCors(options =>
        {
            options.AddPolicy("CorsPolicy",
                builder => builder
                    .SetIsOriginAllowed((host) => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
        });

        services.Configure<ApiBehaviorOptions>(options => { options.SuppressModelStateInvalidFilter = true; });

        services.AddDbContext(config);
        services.AddMediatRDependency();
        services.AddApplicationDependencies(config);
        services.AddAuthentication(config);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
    }

    public static void ConfigureMiddleware(this WebApplication app)
    {
        app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
        app.UseHealthChecks("/_health");
        var appsettings = app.Configuration.GetSection(nameof(ApplicationOptions)).Get<ApplicationOptions>();
        if (appsettings is { ShowSwagger: true })
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseRouting();
        app.UseHttpMetrics();
        
        app.UseHttpsRedirection();
        app.UseCors("CorsPolicy");
        app.UseAuthorization();

        app.MapControllers();
        app.MapMetrics();
    }

    public static void AddAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection appSettingsSection = configuration.GetSection("ApplicationOptions");
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
        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();
        });

    }

    public static void AddDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["DATABASE_CON"]
            ?? throw new InvalidOperationException("DATABASE_CON must be set in configuration.");
        var databaseKind = configuration["DatabaseKind"] ?? "mssql";

        services.AddDbContext<ApplicationContext>((_, options) =>
        {
            switch (databaseKind.ToLowerInvariant())
            {
                case "mssql":
                    options.UseSqlServer(connectionString);
                    break;
                case "postgres":
                    options.UseNpgsql(connectionString);
                    break;
                case "sqlite":
                    options.UseSqlite(connectionString);
                    break;
                case "mysql":
                    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)));
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown DatabaseKind '{databaseKind}'. Use mssql, postgres, sqlite, or mysql.");
            }
        });
        services.AddScoped<IApplicationContext>(sp => sp.GetRequiredService<ApplicationContext>());
    }

    public static void AddApplicationDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ICurrentUserService, CurrentUserService>();

        services.AddStackExchangeRedisCache(options =>
        {
            options.ConnectionMultiplexerFactory = () => Task.FromResult(_connectionMultiplexer);
            options.InstanceName = "Template.Api";
        });
    }
}
