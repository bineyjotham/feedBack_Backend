using FeedBack.API.Models;

namespace FeedBack.API.Services;

public interface IEmailQueueService
{
    Task QueueEmailAsync(string ticketNumber, string toEmail, string subject, string htmlBody, string? replyTo = null);
    Task ProcessEmailQueueAsync(CancellationToken cancellationToken = default);
    Task<int> GetPendingEmailCountAsync();
}