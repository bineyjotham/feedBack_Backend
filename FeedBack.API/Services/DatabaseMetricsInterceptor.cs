using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FeedBack.API.Services;

public class DatabaseMetricsInterceptor : DbCommandInterceptor
{
    private readonly IMetricsService _metricsService;
    private readonly ILogger<DatabaseMetricsInterceptor> _logger;

    public DatabaseMetricsInterceptor(IMetricsService metricsService, ILogger<DatabaseMetricsInterceptor> logger)
    {
        _metricsService = metricsService;
        _logger = logger;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, 
        CommandEventData eventData, 
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var readerResult = await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
            stopwatch.Stop();
            
            var operation = GetOperationName(command);
            _metricsService.RecordDatabaseQuery(operation, stopwatch.Elapsed.TotalMilliseconds);
            
            if (stopwatch.ElapsedMilliseconds > 500)
            {
                _logger.LogWarning("Slow database query ({Operation}): {Duration}ms\nCommand: {Command}", 
                    operation, stopwatch.ElapsedMilliseconds, command.CommandText);
            }
            
            return readerResult;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Database query failed after {Duration}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, 
        CommandEventData eventData, 
        InterceptionResult<DbDataReader> result)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var readerResult = base.ReaderExecuting(command, eventData, result);
            stopwatch.Stop();
            
            var operation = GetOperationName(command);
            _metricsService.RecordDatabaseQuery(operation, stopwatch.Elapsed.TotalMilliseconds);
            
            return readerResult;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Database query failed after {Duration}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private static string GetOperationName(DbCommand command)
    {
        return command.CommandText?.Trim().Split(' ', '\n', '\t')[0].ToUpperInvariant() ?? "UNKNOWN";
    }
}