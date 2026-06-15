using FeedBack.API.Data;
using FeedBack.API.Models;
using Microsoft.EntityFrameworkCore;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace FeedBack.API.Services;

public class EmailQueueService : IEmailQueueService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailQueueService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _apiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public EmailQueueService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<EmailQueueService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _apiKey = configuration["SendGrid:ApiKey"] ?? throw new ArgumentNullException("SendGrid API key not configured");
        _fromEmail = configuration["SendGrid:FromEmail"] ?? "data.alert@xdsdatagh.com";
        _fromName = configuration["SendGrid:FromName"] ?? "XDS Data Ghana";
    }

    public async Task QueueEmailAsync(string ticketNumber, string toEmail, string subject, string htmlBody, string? replyTo = null)
    {
        var emailQueue = new EmailQueue
        {
            TicketNumber = ticketNumber,
            ToEmail = toEmail,
            Subject = subject,
            HtmlBody = htmlBody,
            ReplyTo = replyTo,
            FromEmail = _fromEmail,
            CreatedAt = DateTime.UtcNow,
            Status = EmailStatus.Pending,   
            MaxAttempts = _configuration.GetValue<int>("EmailRetry:MaxAttempts", 3)
        };

        await _context.EmailQueues.AddAsync(emailQueue);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Email queued for {ToEmail} with ticket {TicketNumber}", toEmail, ticketNumber);
    }

    public async Task ProcessEmailQueueAsync(CancellationToken cancellationToken = default)
    {
        var pendingEmails = await _context.EmailQueues
            .Where(e => e.Status == EmailStatus.Pending || e.Status == EmailStatus.Retrying)
            .Where(e => e.AttemptCount < e.MaxAttempts)
            .OrderBy(e => e.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        // Apply retry delay filter in memory (GetRetryDelay cannot be translated to SQL)
        pendingEmails = pendingEmails
            .Where(e => e.LastAttemptAt == null || e.LastAttemptAt < DateTime.UtcNow.AddSeconds(-GetRetryDelay(e.AttemptCount)))
            .ToList();

        if (!pendingEmails.Any())
        {
            return;
        }

        _logger.LogInformation("Processing {Count} pending emails", pendingEmails.Count);

        var client = new SendGridClient(_apiKey);

        foreach (var email in pendingEmails)
        {
            await ProcessSingleEmailAsync(client, email, cancellationToken);
        }
    }

    private async Task ProcessSingleEmailAsync(SendGridClient client, EmailQueue email, CancellationToken cancellationToken)
    {
        email.AttemptCount++;
        email.LastAttemptAt = DateTime.UtcNow;

        try
        {
            var from = new EmailAddress(email.FromEmail!, _fromName);
            var to = new EmailAddress(email.ToEmail);
            var msg = MailHelper.CreateSingleEmail(from, to, email.Subject, null, email.HtmlBody);
            
            if (!string.IsNullOrEmpty(email.ReplyTo))
            {
                msg.ReplyTo = new EmailAddress(email.ReplyTo);
            }

            var response = await client.SendEmailAsync(msg, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                email.Status = EmailStatus.Sent;
                email.SentAt = DateTime.UtcNow;
                email.ErrorMessage = null;
                _logger.LogInformation("Email sent successfully to {ToEmail} for ticket {TicketNumber}", 
                    email.ToEmail, email.TicketNumber);
            }
            else
            {
                var errorBody = await response.Body.ReadAsStringAsync(cancellationToken);
                email.Status = email.AttemptCount >= email.MaxAttempts ? EmailStatus.Failed : EmailStatus.Retrying;
                email.ErrorMessage = $"HTTP {response.StatusCode}: {errorBody}";
                _logger.LogWarning("Failed to send email to {ToEmail}, attempt {AttemptCount}/{MaxAttempts}, Error: {Error}", 
                    email.ToEmail, email.AttemptCount, email.MaxAttempts, email.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            email.Status = email.AttemptCount >= email.MaxAttempts ? EmailStatus.Failed : EmailStatus.Retrying;
            email.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Exception while sending email to {ToEmail}, attempt {AttemptCount}", 
                email.ToEmail, email.AttemptCount);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static int GetRetryDelay(int attemptCount)
    {
        // Exponential backoff: 60s, 120s, 240s, etc.
        return attemptCount == 0 ? 60 : (int)Math.Pow(2, attemptCount) * 60;
    }

    public async Task<int> GetPendingEmailCountAsync()
    {
        return await _context.EmailQueues
            .Where(e => e.Status == EmailStatus.Pending || e.Status == EmailStatus.Retrying)
            .CountAsync();
    }
}