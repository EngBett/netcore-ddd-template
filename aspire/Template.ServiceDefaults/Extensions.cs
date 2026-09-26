using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// The namespace is Microsoft.Extensions.Hosting on purpose, so AddServiceDefaults() is in
// scope wherever a host builder is: it is the convention Aspire's own template uses.
namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Cross-cutting host configuration shared by every service in this solution:
/// OpenTelemetry, health checks and HttpClient resilience.
/// </summary>
public static class Extensions
{
    private const string AlivenessEndpointPath = "/alive";

    /// <summary>
    /// Adds telemetry, health checks and HttpClient resilience.
    /// </summary>
    /// <remarks>
    /// Note what is <em>not</em> here: <c>AddServiceDiscovery()</c>. Service discovery
    /// resolves logical names like <c>https://api</c> from configuration Aspire injects,
    /// which makes the service's HTTP targets depend on how it was launched — the one
    /// thing this solution's configuration rules rule out, because it cannot be mirrored
    /// in production without reproducing Aspire's scheme. Everything kept here is plain,
    /// portable configuration: OTLP is switched on by the standard
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> variable, and resilience needs no configuration
    /// at all. If you later add a second service and want to call it by name, prefer
    /// giving the caller an explicit base-address option over reintroducing discovery.
    /// </remarks>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Retries, circuit breaker and timeouts for every HttpClient.
            http.AddStandardResilienceHandler();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    // Wolverine's own counters: messages sent/received/succeeded/failed.
                    .AddMeter("Wolverine");
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(builder.Environment.ApplicationName)
                    // Wolverine names its ActivitySource "Wolverine". Without this, command
                    // and message handling is invisible in traces, which is most of what
                    // this service actually does.
                    .AddSource("Wolverine")
                    .AddAspNetCoreInstrumentation(options =>
                        // Health probes would otherwise dominate the trace list.
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(AlivenessEndpointPath) &&
                            !context.Request.Path.StartsWithSegments("/_health"))
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // OTEL_EXPORTER_OTLP_ENDPOINT is the standard OpenTelemetry variable, which the
        // Aspire AppHost sets for us and any other collector can set just as easily. When
        // it is absent — running the service on its own — telemetry is simply not exported
        // and nothing fails.
        var useOtlpExporter = !string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
            builder.Services.AddOpenTelemetry().UseOtlpExporter();

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// Maps the liveness endpoint. The readiness endpoint stays <c>/_health</c>, mapped by
    /// <c>Template.Api</c>'s own middleware configuration.
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Liveness answers "is the process up", so it must not depend on the database or
        // the broker being reachable: only checks tagged "live" run.
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live")
        });

        return app;
    }
}
