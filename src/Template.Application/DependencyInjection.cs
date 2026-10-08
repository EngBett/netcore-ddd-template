using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Template.Common.Messages.Todos;
using Template.Common.Options;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.FluentValidation;
using Wolverine.RabbitMQ;
using Wolverine.Runtime.Handlers;

namespace Template.Application
{
    public static class DependencyInjection
    {
        /// <summary>
        /// The message types that travel over RabbitMQ, paired with the queue each uses.
        /// </summary>
        /// <remarks>
        /// Wolverine is this template's mediator <em>and</em> its message bus, so every
        /// command, query and domain event in this assembly is a "message" to it. Broker
        /// behaviour is therefore opt-in per contract: this one list drives both the routing
        /// in <see cref="ConfigureBrokerContracts"/> and the retry policy in
        /// <see cref="BrokerResiliencePolicy"/>, so adding a contract here gets both.
        /// A message type that is absent stays on an in-process local queue, which is what
        /// CQRS requests and domain events should do.
        /// </remarks>
        private static readonly (Type Contract, string Queue)[] BrokerContracts =
        [
            (typeof(TodoMessage), "todo-message")
        ];

        /// <param name="services">The service collection.</param>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="configureWolverine">
        /// Lets the composition root add Wolverine configuration that this layer cannot own,
        /// because it needs types from Infrastructure (durable messaging is the case in point).
        /// It has to be a callback rather than <c>ConfigureWolverine</c>: Wolverine forbids
        /// extensions registered in the container from changing service registrations, which
        /// persistence and EF Core transaction support both do.
        /// </param>
        public static IServiceCollection AddApplicationDependencies(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<WolverineOptions>? configureWolverine = null)
        {
            services.RegisterWolverineDependencies(configuration, configureWolverine);
            return services;
        }

        private static IServiceCollection RegisterWolverineDependencies(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<WolverineOptions>? configureWolverine)
        {
            services.Configure<RabbitMQOptions>(configuration.GetSection(nameof(RabbitMQOptions)));
            services.Configure<MessagingOptions>(configuration.GetSection(nameof(MessagingOptions)));

            // Wolverine is configured off IConfiguration rather than IOptions: AddWolverine
            // runs while the service collection is still being built, so there is no
            // provider to resolve IOptions<T> from yet.
            var rmq = configuration.GetSection(nameof(RabbitMQOptions)).Get<RabbitMQOptions>() ?? new RabbitMQOptions();
            var messaging = configuration.GetSection(nameof(MessagingOptions)).Get<MessagingOptions>() ?? new MessagingOptions();

            services.AddWolverine(opts =>
            {
                // Handlers, consumers and domain-event handlers all live in this assembly,
                // not in the API host that owns the Wolverine runtime, so scanning has to
                // be pointed at it explicitly.
                opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());

                // Discovers the AbstractValidator types in the scanned assemblies, registers
                // them, and runs them as middleware in front of any handler whose message has
                // one, throwing FluentValidation.ValidationException on failure. This replaces
                // the hand-written MediatR ValidatorBehavior.
                //
                // Note: this is the only validator registration. Calling
                // AddValidatorsFromAssembly as well registers each validator a second time,
                // and FluentValidation then runs every rule twice.
                opts.UseFluentValidation();

                // Wolverine generates handler code that constructs dependencies inline, and
                // by default refuses to fall back on resolving them from the container.
                // Infrastructure registers IApplicationContext as an alias
                // (AddScoped<IApplicationContext>(sp => sp.GetRequiredService<ApplicationContext>()))
                // so that one DbContext instance serves both the interface and the concrete
                // type. Wolverine cannot see through that lambda, so without this every
                // handler taking IApplicationContext fails codegen with
                // InvalidServiceLocationException.
                //
                // The alternative - registering IApplicationContext against the concrete type
                // - would hand out a *second* DbContext per scope, with its own change tracker,
                // which is a far worse and much quieter bug than one container lookup per
                // handler invocation. This is a deliberate trade, hence AlwaysAllowed rather
                // than AllowedButWarn: the warning would fire on every start for a state that
                // is intended.
                opts.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;

                // Wolverine's RabbitMQ Uri overload takes an *amqp://* URI and rejects
                // MassTransit's `rabbitmq://` scheme outright. Configuring the
                // ConnectionFactory directly avoids building a URI only to have it
                // reparsed, and keeps the virtual host free of the leading-slash
                // quirk a URI path needs.
                opts.UseRabbitMq(factory =>
                    {
                        factory.HostName = rmq.HostName;
                        factory.Port = rmq.Port > 0 ? rmq.Port : 5672;
                        factory.UserName = rmq.UserName;
                        factory.Password = rmq.Password;
                        factory.VirtualHost = string.IsNullOrWhiteSpace(rmq.VirtualHost) ? "/" : rmq.VirtualHost;
                    })
                    // The MassTransit bus declared its own topology on start; AutoProvision
                    // keeps that behaviour so a fresh broker needs no manual setup.
                    .AutoProvision();

                ConfigureBrokerContracts(opts);

                // Retries are scoped to the broker contracts, never applied globally. See
                // BrokerResiliencePolicy for why that distinction matters.
                opts.Policies.Add(new BrokerResiliencePolicy(messaging, BrokerContracts.Select(c => c.Contract)));

                configureWolverine?.Invoke(opts);
            });

            return services;
        }

        /// <summary>
        /// Points each broker contract at its queue for both sending and listening.
        /// </summary>
        /// <remarks>
        /// <c>UseConventionalRouting()</c> is deliberately not used. It looks like the
        /// equivalent of MassTransit's <c>ConfigureEndpoints</c>, but a message type that has
        /// a local handler - as every contract here does, since the consumer lives in this
        /// assembly - resolves to its local queue in preference to the broker, so published
        /// messages would quietly never leave the process.
        /// </remarks>
        private static void ConfigureBrokerContracts(WolverineOptions opts)
        {
            foreach (var (contract, queue) in BrokerContracts)
            {
                opts.PublishMessage(contract).ToRabbitQueue(queue);
                opts.ListenToRabbitQueue(queue);
            }
        }

        /// <summary>
        /// Applies <see cref="MessagingOptions"/> retry and redelivery to the handler chains
        /// of the broker contracts only.
        /// </summary>
        /// <remarks>
        /// MassTransit's <c>UseMessageRetry</c> / <c>UseDelayedRedelivery</c> were configured
        /// on receive endpoints, so they governed messages arriving from RabbitMQ and never
        /// touched in-process MediatR dispatch. Wolverine's equivalent
        /// (<c>opts.Policies.OnAnyException()</c>) is global and covers
        /// <c>IMessageBus.InvokeAsync</c> as well, which is the wrong behaviour for a CQRS
        /// request: a command that fails validation gets retried on every cooldown before the
        /// caller ever sees the error, turning an immediate 400 into a multi-second hang.
        /// Scoping the rules to the broker contracts' chains restores the MassTransit split.
        /// </remarks>
        private sealed class BrokerResiliencePolicy : IHandlerPolicy
        {
            private readonly MessagingOptions _options;
            private readonly HashSet<Type> _contracts;

            public BrokerResiliencePolicy(MessagingOptions options, IEnumerable<Type> contracts)
            {
                _options = options;
                _contracts = [.. contracts];
            }

            public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
            {
                var retries = ToTimeSpans(_options.RetryIntervalsMilliseconds, TimeSpan.FromMilliseconds);
                var redeliveries = _options.EnableDelayedRedelivery
                    ? ToTimeSpans(_options.RedeliveryIntervalsSeconds, TimeSpan.FromSeconds)
                    : [];

                if (retries.Length == 0 && redeliveries.Length == 0)
                    return;

                foreach (var chain in chains.Where(c => _contracts.Contains(c.MessageType)))
                {
                    // Wolverine has no direct equivalent of MassTransit's separate
                    // UseMessageRetry / UseDelayedRedelivery stages, but the two map onto one
                    // chained rule: retry in-process first, then hand what is still failing
                    // to the scheduler for a slower second round.
                    if (retries.Length == 0)
                    {
                        chain.OnAnyException().ScheduleRetry(redeliveries);
                        continue;
                    }

                    var rule = chain.OnAnyException().RetryWithCooldown(retries);

                    if (redeliveries.Length > 0)
                        rule.Then.ScheduleRetry(redeliveries);
                }
            }
        }

        private static TimeSpan[] ToTimeSpans(int[]? values, Func<double, TimeSpan> convert) =>
            values is { Length: > 0 }
                ? values.Where(v => v > 0).Select(v => convert(v)).ToArray()
                : [];
    }
}
