using FeedBack.API.Data;
using FeedBack.API.Dtos;
using FeedBack.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FeedBack.API.Services;

public class FeedbackService : IFeedbackService
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailQueueService _emailQueueService;
    private readonly ILogger<FeedbackService> _logger;

    public FeedbackService(
        ApplicationDbContext context,
        IEmailQueueService emailQueueService,
        ILogger<FeedbackService> logger)
    {
        _context = context;
        _emailQueueService = emailQueueService;
        _logger = logger;
    }

    public async Task<FeedbackResponseDto> SubmitFeedbackAsync(FeedbackRequestDto request, string ipAddress, string userAgent)
    {
        try
        {
            // Generate ticket number
            var ticketNumber = GenerateTicketNumber();
            _logger.LogInformation("Processing feedback submission. Ticket: {TicketNumber}", ticketNumber);

            // Parse source
            var source = ParseSource(request.Source);

            // Create feedback entity
            var feedback = new Feedback
            {
                TicketNumber = ticketNumber,
                Source = source,
                Rating = request.Rating,
                Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment?.Trim(),
                CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? null : request.CustomerName?.Trim(),
                CustomerEmail = string.IsNullOrWhiteSpace(request.CustomerEmail) ? null : request.CustomerEmail?.Trim().ToLower(),
                Institution = string.IsNullOrWhiteSpace(request.Institution) ? null : request.Institution?.Trim(),
                Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category?.Trim(),
                PersonnelName = string.IsNullOrWhiteSpace(request.PersonnelName) ? null : request.PersonnelName?.Trim(),
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Status = FeedbackStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            // Save to database
            await _context.Feedbacks.AddAsync(feedback);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Feedback saved to database. Ticket: {TicketNumber}, Id: {Id}", ticketNumber, feedback.Id);

            // Queue emails (fire and forget - don't block response)
            _ = Task.Run(async () =>
            {
                try
                {
                    await QueueFeedbackEmailsAsync(request, ticketNumber, feedback.CustomerName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background email queuing failed for ticket: {TicketNumber}", ticketNumber);
                }
            });

            return new FeedbackResponseDto
            {
                Success = true,
                TicketNumber = ticketNumber,
                Message = string.IsNullOrEmpty(request.Comment) 
                    ? "Rating submitted successfully" 
                    : "Feedback submitted successfully",
                CreatedAt = feedback.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit feedback");
            throw;
        }
    }

    private static string GenerateTicketNumber()
    {
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var randomPart = new Random().Next(1, 9999).ToString("D4");
        return $"XDS-FB-{datePart}-{randomPart}";
    }

    private static FeedbackSource ParseSource(string? source)
    {
        return source?.ToLower() switch
        {
            "premises" => FeedbackSource.premises,
            "product_service" => FeedbackSource.product_service,
            _ => FeedbackSource.website
        };
    }

    private async Task QueueFeedbackEmailsAsync(FeedbackRequestDto feedback, string ticketNumber, string? customerName)
    {
        // Get team email from configuration (you can move this to appsettings)
        var teamEmail = "dispute@xdsdata.com"; // TODO: Move to configuration

        // Queue team email if there's feedback or low rating (below 3 out of 10)
        if (!string.IsNullOrEmpty(feedback.Comment) || feedback.Rating < 3)
        {
            var teamEmailHtml = CreateTeamEmailHtml(feedback, ticketNumber, customerName);
            await _emailQueueService.QueueEmailAsync(
                ticketNumber, 
                teamEmail, 
                $"New {(!string.IsNullOrEmpty(feedback.Comment) ? "Feedback" : "Rating")} from {GetDisplayName(feedback, customerName)} [Ticket: {ticketNumber}]", 
                teamEmailHtml, 
                feedback.CustomerEmail);
            _logger.LogInformation("Team email queued for ticket: {TicketNumber}", ticketNumber);
        }
        else
        {
            _logger.LogInformation("Skipping team email - rating {Rating} with no comment", feedback.Rating);
        }

        // Queue user confirmation email if email provided
        if (!string.IsNullOrEmpty(feedback.CustomerEmail) && IsValidEmail(feedback.CustomerEmail))
        {
            var userEmailHtml = CreateUserConfirmationHtml(feedback, ticketNumber, customerName);
            await _emailQueueService.QueueEmailAsync(
                ticketNumber, 
                feedback.CustomerEmail, 
                $"Thank You for Your Feedback [Ticket: {ticketNumber}]", 
                userEmailHtml);
            _logger.LogInformation("User confirmation email queued for: {Email}", feedback.CustomerEmail);
        }
    }

    private static string GetDisplayName(FeedbackRequestDto feedback, string? customerName)
    {
        return !string.IsNullOrEmpty(customerName) ? customerName : "Valued Customer";
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrEmpty(email)) return false;
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private string CreateTeamEmailHtml(FeedbackRequestDto feedback, string ticketNumber, string? customerName)
    {
        var ratingStars = new string('★', feedback.Rating) + new string('☆', 10 - feedback.Rating);
        var currentYear = DateTime.UtcNow.Year;

        // Use string builder for cleaner HTML construction
        var html = new System.Text.StringBuilder();
        
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset='utf-8'>");
        html.AppendLine("<title>New Feedback Submission</title>");
        html.AppendLine("</head>");
        html.AppendLine("<body style='font-family: Arial, sans-serif;'>");
        html.AppendLine($"<h1 style='color: #54B847;'>New Feedback Submission</h1>");
        html.AppendLine($"<p><strong>Ticket Number:</strong> {ticketNumber}</p>");
        html.AppendLine("<hr>");
        html.AppendLine("<h2>Submission Details</h2>");
        html.AppendLine($"<p><strong>Source:</strong> {(feedback.Source?.Replace("_", "/").ToUpper() ?? "WEBSITE")}</p>");
        html.AppendLine($"<p><strong>Name:</strong> {GetDisplayName(feedback, customerName)}</p>");
        html.AppendLine($"<p><strong>Email:</strong> {feedback.CustomerEmail ?? "Not provided"}</p>");
        
        if (!string.IsNullOrEmpty(feedback.Institution))
        {
            html.AppendLine($"<p><strong>Institution:</strong> {System.Net.WebUtility.HtmlEncode(feedback.Institution)}</p>");
        }
        
        if (!string.IsNullOrEmpty(feedback.PersonnelName))
        {
            html.AppendLine($"<p><strong>Personnel:</strong> {System.Net.WebUtility.HtmlEncode(feedback.PersonnelName)}</p>");
        }
        
        html.AppendLine($"<p><strong>Rating:</strong> {ratingStars} ({feedback.Rating}/10)</p>");
        
        if (!string.IsNullOrEmpty(feedback.Comment))
        {
            html.AppendLine("<h2>Feedback</h2>");
            html.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(feedback.Comment)}</p>");
        }
        
        html.AppendLine("<hr>");
        html.AppendLine($"<p style='font-size: 12px; color: #777;'>XDS Data Ghana &copy; {currentYear}</p>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        
        return html.ToString();
    }

    private string CreateUserConfirmationHtml(FeedbackRequestDto feedback, string ticketNumber, string? customerName)
    {
        var currentYear = DateTime.UtcNow.Year;

        // Use string builder for cleaner HTML construction
        var html = new System.Text.StringBuilder();
        
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset='utf-8'>");
        html.AppendLine("<title>Thank You for Your Feedback</title>");
        html.AppendLine("</head>");
        html.AppendLine("<body style='font-family: Arial, sans-serif;'>");
        html.AppendLine($"<h1 style='color: #54B847;'>Thank You!</h1>");
        html.AppendLine($"<p>Dear {GetDisplayName(feedback, customerName)},</p>");
        html.AppendLine($"<p>Thank you for sharing your rating with us. You rated us {feedback.Rating}/10.</p>");
        
        if (!string.IsNullOrEmpty(feedback.Comment))
        {
            html.AppendLine("<p>We have also received your feedback and truly appreciate your input.</p>");
        }
        
        html.AppendLine($"<p><strong>Reference Number:</strong> {ticketNumber}</p>");
        
        if (!string.IsNullOrEmpty(feedback.Comment))
        {
            html.AppendLine("<h3>What happens next?</h3>");
            html.AppendLine("<ul>");
            html.AppendLine("<li>Our team will review your feedback within 24-48 hours</li>");
            html.AppendLine("<li>If action is required, we will reach out to you via email</li>");
            html.AppendLine("<li>Your insights contribute to making XDS Data better for everyone</li>");
            html.AppendLine("</ul>");
        }
        
        html.AppendLine("<hr>");
        html.AppendLine("<p><strong>Need immediate assistance?</strong></p>");
        html.AppendLine("<p>Call our support team: +233 26 983 1092</p>");
        html.AppendLine("<p>Email: support@xdsdata.com</p>");
        html.AppendLine("<hr>");
        html.AppendLine($"<p style='font-size: 12px; color: #777;'>XDS Data Ghana &copy; {currentYear}</p>");
        html.AppendLine("<p style='font-size: 12px; color: #777;'>This is an automated message. Please do not reply to this email.</p>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        
        return html.ToString();
    }
}