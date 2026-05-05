namespace Template.Common.Options;

/// <summary>
/// Binds the <c>RabbitMQOptions</c> section in configuration (used by MassTransit).
/// </summary>
public class RabbitMQOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    /// <summary>
    /// RabbitMQ virtual host (often <c>/</c> for the default vhost).
    /// </summary>
    public string VirtualHost { get; set; } = "/";
}
