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
    private const string CorsPolicy = "CorsPolicy";

    public static void AddApiDependencies(this IServiceCollection services, IConfiguration config)
    {
        
        services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

        services.AddControllers(opt => { opt.Filters.Add<GlobalExceptionFilter>(); });

        services.AddHttpContextAccessor();
        
        services.AddSingleton<ICurrentUserService, CurrentUserService>();
        
        services.AddHealthChecks();

        // Cross-origin access is opt-in per origin. Reflecting every origin back while also
        // allowing credentials would let any website make authenticated calls on a signed-in
        // user's behalf. With no origins configured the policy grants nothing, so only
        // same-origin callers (and non-browser clients) get through.
        var allowedOrigins = config.GetSection(nameof(ApplicationOptions)).Get<ApplicationOptions>()?.AllowedOrigins ?? [];
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicy, builder =>
            {
                if (allowedOrigins.Length > 0)
                    builder.WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
            });
        });

        services.Configure<ApiBehaviorOptions>(options => { options.SuppressModelStateInvalidFilter = true; });
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
    }

    public static void ConfigureMiddleware(this WebApplication app)
    {
        // GlobalExceptionFilter only runs for controllers, so the Minimal API and
        // FastEndpoints styles need this to answer with the same 400 / 500 bodies.
        // First in the pipeline so it wraps the endpoints of every style.
        app.UseExceptionResponses();

        app.UseHealthChecks("/_health");
        var appsettings = app.Configuration.GetSection(nameof(ApplicationOptions)).Get<ApplicationOptions>();
        // Always on in Development, where the launch profile opens /swagger; ShowSwagger
        // turns it on elsewhere.
        if (app.Environment.IsDevelopment() || appsettings is { ShowSwagger: true })
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseRouting();
        app.UseHttpMetrics();
        
        app.UseHttpsRedirection();
        app.UseCors(CorsPolicy);
        app.UseAuthorization();

        app.MapControllers();
        app.MapMetrics();
    }
}
