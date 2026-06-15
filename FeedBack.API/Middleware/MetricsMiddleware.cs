using System.Diagnostics;
using FeedBack.API.Services;

namespace FeedBack.API.Middleware;

public class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MetricsMiddleware> _logger;

    public MetricsMiddleware(RequestDelegate next, ILogger<MetricsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IMetricsService metricsService)
    {
        var stopwatch = Stopwatch.StartNew();
        var endpoint = context.Request.Path.Value ?? "unknown";
        var method = context.Request.Method;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;
            
            metricsService.RecordApiRequest(endpoint, method, statusCode, stopwatch.Elapsed.TotalMilliseconds);
            
            // Log slow requests
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                _logger.LogWarning("Slow request: {Method} {Endpoint} took {Duration}ms", 
                    method, endpoint, stopwatch.ElapsedMilliseconds);
            }
        }
    }
}