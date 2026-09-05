using Template.Application;
using Template.Application.Interfaces;
using Template.Infrastructure.DataAccess;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using StackExchange.Redis;
using Template.Api.Filters;
using Template.Api.Services;
using Template.Common.Options;

namespace Template.Api;

public static class DependencyInjection
{
    public static void AddApiDependencies(this IServiceCollection services, IConfiguration config)
    {
        
        services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

        services.AddControllers(opt => { opt.Filters.Add<GlobalExceptionFilter>(); });

        services.AddHttpContextAccessor();
        
        services.AddSingleton<ICurrentUserService, CurrentUserService>();
        
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
}
