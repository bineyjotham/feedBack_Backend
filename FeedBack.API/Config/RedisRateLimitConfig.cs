using AspNetCoreRateLimit;
// using Microsoft.Extensions.Caching.Distributed;
// using Microsoft.Extensions.Citoring.StackExchangeRedis;
using StackExchange.Redis;

namespace FeedBack.API.Config;

public static class RedisRateLimitConfig
{
    public static IServiceCollection AddRedisRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Redis
        var redisConnectionString = configuration.GetConnectionString("Redis");
        
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "FeedbackAPI_";
        });

        // Configure rate limiting with Redis
        services.Configure<IpRateLimitOptions>(options =>
        {
            options.GeneralRules = new List<RateLimitRule>
            {
                new RateLimitRule
                {
                    Endpoint = "POST:/api/feedback/submit",
                    Limit = configuration.GetValue<int>("RateLimiting:PermitLimit", 10),
                    Period = $"{configuration.GetValue<int>("RateLimiting:WindowInSeconds", 60)}s"
                },
                new RateLimitRule
                {
                    Endpoint = "POST:/api/auth/*",
                    Limit = 5,
                    Period = "60s"
                },
                new RateLimitRule
                {
                    Endpoint = "GET:/api/dashboard/*",
                    Limit = 100,
                    Period = "60s"
                }
            };
            options.EnableEndpointRateLimiting = true;
            options.StackBlockedRequests = true;
            options.HttpStatusCode = 429;
            options.RealIpHeader = "X-Real-IP";
            options.ClientIdHeader = "X-ClientId";
        });

        services.AddMemoryCache();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
        services.AddInMemoryRateLimiting();
        
        return services;
    }
}