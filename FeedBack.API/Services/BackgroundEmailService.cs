using System.Threading;
using Microsoft.Extensions.Hosting;

namespace FeedBack.API.Services;

public class BackgroundEmailService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundEmailService> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(30);

    public BackgroundEmailService(IServiceProvider serviceProvider, ILogger<BackgroundEmailService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Email Service is starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var emailQueueService = scope.ServiceProvider.GetRequiredService<IEmailQueueService>();
                
                var pendingCount = await emailQueueService.GetPendingEmailCountAsync();
                
                if (pendingCount > 0)
                {
                    _logger.LogInformation("Found {PendingCount} pending emails to process", pendingCount);
                    await emailQueueService.ProcessEmailQueueAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing email queue");
            }

            await Task.Delay(_processingInterval, stoppingToken);
        }

        _logger.LogInformation("Background Email Service is stopping");
    }
}