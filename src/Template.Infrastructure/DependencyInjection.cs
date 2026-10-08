using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using JasperFx;
using Template.Application.Interfaces;
using Template.Common.Options;
using Template.Domain.Models;
using Template.Infrastructure.DataAccess;
using Wolverine;
using Wolverine.EntityFrameworkCore;
//#if (usePostgres)
using Wolverine.Postgresql;
//#endif
//#if (useMssql)
using Wolverine.SqlServer;
//#endif

namespace Template.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RedisOptions>(configuration.GetSection(nameof(RedisOptions)));
        services.AddSingleton<IConfigureOptions<RedisCacheOptions>, RedisCacheOptionsConfigurator>();
        services.AddStackExchangeRedisCache(_ => { });

        services.AddApplicationContext<ApplicationContext>(configuration);
        services.AddAuthentication(configuration);
        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TContext"/> as the service's <c>DbContext</c>, aliases
    /// <see cref="IApplicationContext"/> to it, and, when <c>DurableMessagingOptions:Enabled</c> is
    /// set, wires Wolverine's transactional outbox and inbox to the same database.
    /// </summary>
    /// <remarks>
    /// Generic so a test (or a service) can register a subclass that adds entities, while
    /// still going through exactly this wiring.
    /// </remarks>
    public static IServiceCollection AddApplicationContext<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : ApplicationContext
    {
        var connectionString = configuration["DATABASE_CON"]
                               ?? throw new InvalidOperationException("DATABASE_CON must be set in configuration.");
        var databaseKind = configuration["DatabaseKind"] ?? "mssql";

        var durableSection = configuration.GetSection(nameof(DurableMessagingOptions));
        services.Configure<DurableMessagingOptions>(durableSection);
        var durable = durableSection.Get<DurableMessagingOptions>() ?? new DurableMessagingOptions();

        void ConfigureProvider(DbContextOptionsBuilder options)
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
        }

        if (durable.Enabled)
        {
            // Registers the DbContext so Wolverine can enlist it in a handler's transaction and
            // write the outbox rows in the same commit.
            services.AddDbContextWithWolverineIntegration<TContext>(ConfigureProvider);
        }
        else
        {
            services.AddDbContext<TContext>((_, options) => ConfigureProvider(options));
        }

        services.AddScoped<IApplicationContext>(sp => sp.GetRequiredService<TContext>());

        if (durable.Enabled)
            services.AddHostedService<DurableMessagingGuard>();

        return services;
    }

    /// <summary>
    /// Wires Wolverine's transactional outbox, inbox and EF Core transactions to the service's
    /// database. Does nothing unless <c>DurableMessagingOptions:Enabled</c> is true.
    /// </summary>
    /// <remarks>
    /// Call it from the callback passed to <c>AddApplicationDependencies</c>, so it runs inside
    /// <c>AddWolverine</c>. <see cref="AddApplicationContext{TContext}"/> must use the same
    /// <typeparamref name="TContext"/>.
    /// </remarks>
    public static void AddDurableMessaging<TContext>(this WolverineOptions opts, IConfiguration configuration)
        where TContext : ApplicationContext
    {
        var durable = configuration.GetSection(nameof(DurableMessagingOptions)).Get<DurableMessagingOptions>()
                      ?? new DurableMessagingOptions();
        if (!durable.Enabled)
            return;

        var connectionString = configuration["DATABASE_CON"]
                               ?? throw new InvalidOperationException("DATABASE_CON must be set in configuration.");
        var databaseKind = configuration["DatabaseKind"] ?? "mssql";
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"];

        // Wolverine creating its own tables needs DDL rights. That is fine on a laptop and
        // wrong in production, where the application role should not hold them. Same stance as
        // ApplicationOptions.EnableAutoMigration: a hard failure, not a silent override.
        if (durable.AutoBuildStorage && !string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "DurableMessagingOptions:AutoBuildStorage is true outside Development. "
                + "Create the Wolverine schema as a deliberate step (`dotnet run --project src/Template.Api -- resources setup`) "
                + "and set this to false.");
        }

        switch (databaseKind.ToLowerInvariant())
        {
            //#if (usePostgres)
            case "postgres":
                opts.PersistMessagesWithPostgresql(connectionString, durable.SchemaName);
                break;
            //#endif
            //#if (useMssql)
            case "mssql":
                opts.PersistMessagesWithSqlServer(connectionString, durable.SchemaName);
                break;
            //#endif
            default:
                throw new InvalidOperationException(
                    $"Durable messaging is not available for DatabaseKind '{databaseKind}'. "
                    + "It supports PostgreSQL and SQL Server; set DurableMessagingOptions:Enabled to false.");
        }

        // Without CreateOrUpdate the host will not touch the schema, and fails fast with a
        // clear message if it is missing, rather than failing later on a missing table.
        opts.AutoBuildMessageStorageOnStartup = durable.AutoBuildStorage ? AutoCreate.CreateOrUpdate : AutoCreate.None;

        // Handlers depend on IApplicationContext, an interface Wolverine cannot see through
        // to a DbContext. Declaring the pairing is what makes the transaction middleware
        // apply to them at all; without it a handler that throws after SaveChangesAsync
        // leaves the row committed and its messages unpublished.
        opts.UseEntityFrameworkCoreTransactions().WithDbContextAbstraction<IApplicationContext, TContext>();
        opts.Policies.AutoApplyTransactions();

        opts.Policies.UseDurableLocalQueues();
        opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
        opts.Policies.UseDurableInboxOnAllListeners();

        // Domain events leave the entity inside the same transaction as the data.
        opts.PublishDomainEventsFromEntityFrameworkCore<BaseEntity>(entity => entity.DomainEvents);
    }

    private static void AddAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var appSettingsSection = configuration.GetSection("ApplicationOptions");
        services.Configure<ApplicationOptions>(appSettingsSection);
        var appSettings = appSettingsSection.Get<ApplicationOptions>() ?? new ApplicationOptions();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = appSettings.Authority;
                options.RequireHttpsMetadata = appSettings.RequireHttpsMetadata;
                // name of the API resource
                options.Audience = appSettings.Audience;
                // options.MetadataAddress = appSettings.MetadataAddress;
                // No custom BackchannelHttpHandler: the signing keys are fetched from the
                // authority over this channel, so accepting any certificate would let whoever
                // can intercept that call mint tokens this API trusts. For an identity
                // provider on a self-signed certificate, trust its CA on the host instead.
            });
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build());
    }
}