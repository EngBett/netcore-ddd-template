using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Template.Common.Options;
using Wolverine;
using Wolverine.Persistence.Durability;
using Wolverine.Runtime;

namespace Template.Infrastructure.DataAccess;

/// <summary>
/// Fails the host at startup if durable messaging is switched on in configuration but the
/// Wolverine side was never wired up.
/// </summary>
/// <remarks>
/// The DbContext registration and the Wolverine registration are made in two places, because
/// the second must happen inside <c>AddWolverine</c>. Forgetting the second would leave a
/// service that believes its messages are in an outbox when they are not, which is the
/// quietest possible way to lose a payment event.
/// </remarks>
internal sealed class DurableMessagingGuard(IOptions<DurableMessagingOptions> options, IWolverineRuntime runtime)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.Value.Enabled && runtime.Storage is NullMessageStore)
        {
            throw new InvalidOperationException(
                "DurableMessagingOptions:Enabled is true but Wolverine has no message store. "
                + "Pass `opts => opts.AddDurableMessaging<ApplicationContext>(configuration)` as the "
                + "third argument of AddApplicationDependencies.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
