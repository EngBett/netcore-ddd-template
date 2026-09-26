using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Template.Common.Options;

namespace Template.Infrastructure;

internal sealed class RedisCacheOptionsConfigurator : IConfigureOptions<RedisCacheOptions>
{
    private readonly IOptions<RedisOptions> _redis;

    public RedisCacheOptionsConfigurator(IOptions<RedisOptions> redis)
    {
        _redis = redis;
    }

    public void Configure(RedisCacheOptions options)
    {
        options.Configuration = _redis.Value.ConnectionString;
        options.InstanceName = _redis.Value.InstanceName;
    }
}
