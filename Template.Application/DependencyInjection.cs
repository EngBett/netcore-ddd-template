using System;
using System.Linq;
using System.Reflection;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Template.Application.Behaviors;
using Template.Application.Consumers;
using Template.Common.Options;

namespace Template.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationDependencies(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddMediatRDependency();
            services.RegisterMassTransitDependencies(configuration);
            return services;
        }

        private static IServiceCollection AddMediatRDependency(this IServiceCollection services)
        {
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidatorBehavior<,>));
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
            services.AddMediatR(conf =>
            {
                conf.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
                conf.AddOpenBehavior(typeof(ValidatorBehavior<,>));
            });

            return services;
        }

        private static IServiceCollection RegisterMassTransitDependencies(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<RabbitMQOptions>(configuration.GetSection(nameof(RabbitMQOptions)));
            services.Configure<MassTransitOptions>(configuration.GetSection(nameof(MassTransitOptions)));

            services.AddMassTransit(x =>
            {
                x.AddConsumer<TodoMessageConsumer>();

                x.AddConfigureEndpointsCallback((regContext, _, endpointCfg) =>
                {
                    var mt = regContext.GetRequiredService<IOptions<MassTransitOptions>>().Value;
                    if (mt.EnableInMemoryOutbox)
                        endpointCfg.UseInMemoryOutbox(regContext);
                });

                x.UsingRabbitMq((context, cfg) =>
                {
                    var rmq = context.GetRequiredService<IOptions<RabbitMQOptions>>().Value;
                    var mt = context.GetRequiredService<IOptions<MassTransitOptions>>().Value;

                    cfg.Host(RabbitMqConnectionUri(rmq), h =>
                    {
                        h.Username(rmq.UserName);
                        h.Password(rmq.Password);
                    });

                    ApplyMassTransitResilience(cfg, mt);

                    cfg.ConfigureEndpoints(context);
                });
            });

            return services;
        }

        private static Uri RabbitMqConnectionUri(RabbitMQOptions rmq)
        {
            var port = rmq.Port > 0 ? rmq.Port : 5672;
            var vh = string.IsNullOrWhiteSpace(rmq.VirtualHost) ? "/" : rmq.VirtualHost;
            if (!vh.StartsWith('/'))
                vh = "/" + vh;
            return new UriBuilder("rabbitmq", rmq.HostName, port, vh).Uri;
        }

        private static void ApplyMassTransitResilience(IRabbitMqBusFactoryConfigurator cfg, MassTransitOptions o)
        {
            if (o.RetryIntervalsMilliseconds is { Length: > 0 })
                cfg.UseMessageRetry(r => r.Intervals(o.RetryIntervalsMilliseconds));

            if (o.EnableDelayedRedelivery && o.RedeliveryIntervalsSeconds is { Length: > 0 })
            {
                cfg.UseDelayedRedelivery(r =>
                {
                    var spans = o.RedeliveryIntervalsSeconds.Select(s => TimeSpan.FromSeconds(s)).ToArray();
                    r.Intervals(spans);
                });
            }
        }
    }
}
