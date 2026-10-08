using Template.Infrastructure.DataAccess.Extension;
using Wolverine;

namespace Template.DurableTests;

/// <summary>
/// The reference saga. It lives here, not in Template.Application, because a saga type in
/// Application is discovered by every scaffold, including SQLite and MySQL ones that have no
/// saga storage, and would add a table to every service.
/// </summary>
/// <remarks>
/// The three things a saga needs: <see cref="JasperFx.IRevisioned"/> (the base class has the
/// <c>Version</c> property but does not implement the interface), the concurrency-token
/// mapping from <c>ConfigureSaga</c>, and a static <c>NotFound</c>.
/// </remarks>
public class OrderSaga : Saga, JasperFx.IRevisioned
{
    public Guid Id { get; set; }

    public string State { get; set; } = "Started";

    public int Payments { get; set; }

    public static (OrderSaga, OrderTimeout) Start(StartOrder command) =>
        (new OrderSaga { Id = command.Id }, new OrderTimeout(command.Id));

    public void Handle(PayOrder command)
    {
        State = "Paid";
        Payments++;
    }

    public void Handle(OrderTimeout timeout) => MarkCompleted();

    // Without this, a message for a saga that does not exist (already completed, or never
    // started) throws. For an out-of-order or duplicated callback, ignoring it is the answer.
    public static void NotFound(PayOrder command) => NotFoundCalls.Add(command.Id);

    public static readonly System.Collections.Concurrent.ConcurrentBag<Guid> NotFoundCalls = new();
}

public record StartOrder(Guid Id);

public record PayOrder(Guid Id);

public record OrderTimeout(Guid Id) : TimeoutMessage(TimeSpan.FromSeconds(1));

public class OrderSagaConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<OrderSaga>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<OrderSaga> builder)
    {
        builder.HasKey(order => order.Id);
    }
}
