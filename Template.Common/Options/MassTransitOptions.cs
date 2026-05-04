namespace Template.Common.Options;

/// <summary>
/// Binds the <c>MassTransitOptions</c> section: retries, delayed redelivery, and outbox behavior.
/// </summary>
public class MassTransitOptions
{
    /// <summary>
    /// When true, registers the in-memory outbox so sends participate in consumer retry behavior.
    /// </summary>
    public bool EnableInMemoryOutbox { get; set; } = true;

    /// <summary>
    /// Immediate retry intervals (milliseconds) before the message faults. Empty disables message retry.
    /// </summary>
    public int[] RetryIntervalsMilliseconds { get; set; } = [100, 500, 1000];

    /// <summary>
    /// When true, schedules additional delayed redelivery attempts after retries are exhausted.
    /// </summary>
    public bool EnableDelayedRedelivery { get; set; } = true;

    /// <summary>
    /// Delayed redelivery schedule (seconds). Ignored if <see cref="EnableDelayedRedelivery"/> is false.
    /// </summary>
    public int[] RedeliveryIntervalsSeconds { get; set; } = [1, 5, 15];
}
