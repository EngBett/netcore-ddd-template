namespace Template.Common.Options;

/// <summary>
/// Binds the <c>DurableMessagingOptions</c> section: whether Wolverine persists its transactional
/// outbox and inbox in the service's own database.
/// </summary>
/// <remarks>
/// Durable messaging is available on PostgreSQL and SQL Server. When <see cref="Enabled"/> is
/// true, every handler that depends on <c>IApplicationContext</c> runs inside a database
/// transaction that also stores the messages it publishes and the domain events its entities
/// raised, so a commit and its messages succeed or fail together.
/// </remarks>
public class DurableMessagingOptions
{
    /// <summary>
    /// Turns durable messaging on. Off by default so that a bare configuration (and the
    /// SQLite test host) behaves exactly as it did before this feature existed; the shipped
    /// PostgreSQL and SQL Server appsettings turn it on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The schema Wolverine keeps its envelope, node and dead-letter tables in. Keep it apart
    /// from the application schema so grants can be reasoned about separately.
    /// </summary>
    public string SchemaName { get; set; } = "wolverine";

    /// <summary>
    /// Lets Wolverine create and update its own tables when the host starts. This needs DDL
    /// rights, so it is a Development convenience only: outside Development it must be false
    /// and the schema is created as a deliberate step (see AGENTS.md, "Durable messaging").
    /// </summary>
    public bool AutoBuildStorage { get; set; }
}
