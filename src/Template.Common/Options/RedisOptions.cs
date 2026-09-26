namespace Template.Common.Options;

/// <summary>
/// Binds the <c>RedisOptions</c> section (distributed cache via StackExchange.Redis).
/// </summary>
public class RedisOptions
{
    /// <summary>
    /// StackExchange.Redis configuration (e.g. <c>localhost:6379</c> or a full connection string).
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Key prefix for entries stored via <c>AddStackExchangeRedisCache</c>.
    /// </summary>
    public string InstanceName { get; set; } = "Template.Api";
}
