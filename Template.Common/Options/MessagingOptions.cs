namespace Template.Common.Options;

/// <summary>
/// Binds the <c>MessagingOptions</c> section: Wolverine's failure handling for messages
/// consumed from the broker.
/// </summary>
public class MessagingOptions
{
    /// <summary>
    /// Immediate in-process retry delays (milliseconds) applied before a message is
    /// escalated. Empty disables immediate retries.
    /// </summary>
    public int[] RetryIntervalsMilliseconds { get; set; } = [100, 500, 1000];

    /// <summary>
    /// When true, messages that exhaust <see cref="RetryIntervalsMilliseconds"/> are
    /// rescheduled for later attempts instead of failing straight away.
    /// </summary>
    public bool EnableDelayedRedelivery { get; set; } = true;

    /// <summary>
    /// Scheduled retry delays (seconds). Ignored if <see cref="EnableDelayedRedelivery"/> is false.
    /// </summary>
    public int[] RedeliveryIntervalsSeconds { get; set; } = [1, 5, 15];
}
