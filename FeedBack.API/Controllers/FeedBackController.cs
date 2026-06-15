using System.Diagnostics;
using FeedBack.API.Data;
using FeedBack.API.Dtos;
using FeedBack.API.Models;
using FeedBack.API.Services;
using FeedBack.API.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FeedBack.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class FeedbackController : ControllerBase
{
    private readonly IMetricsService _metricsService;
    private readonly ApplicationDbContext _context;
    private readonly IEmailQueueService _emailQueueService;
    private readonly FeedbackRequestValidator _validator;
    private readonly ILogger<FeedbackController> _logger;

    public FeedbackController(
        ApplicationDbContext context,
        IEmailQueueService emailQueueService,
        FeedbackRequestValidator validator,
        ILogger<FeedbackController> logger,
        IMetricsService metricsService)
    {
        _context = context;
        _emailQueueService = emailQueueService;
        _validator = validator;
        _logger = logger;
        _metricsService = metricsService;
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitFeedback([FromBody] FeedbackRequestDto request)
    {
        _logger.LogInformation("Received feedback submission request");

        var validationResult = await _validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Validation failed: {Errors}", validationResult.Errors);
            return BadRequest(new
            {
                error = "Validation failed",
                details = validationResult.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
            });
        }

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var ticketNumber = GenerateTicketNumber();
            var source = ParseSource(request.Source);
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var userAgent = Request.Headers.UserAgent.ToString();

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
                CreatedAt = DateTime.UtcNow,
                Status = FeedbackStatus.Pending
            };

            await _context.Feedbacks.AddAsync(feedback);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Feedback saved to database. Ticket: {TicketNumber}", ticketNumber);

            // Queue emails in background
            await QueueFeedbackEmailsAsync(request, ticketNumber, feedback.CustomerName);

            stopwatch.Stop();
            _metricsService.RecordFeedbackSubmitted(request.Source ?? "website", request.Rating);
            _metricsService.RecordApiRequest("/api/feedback/submit", "POST", 200, stopwatch.ElapsedMilliseconds);

            return Ok(new FeedbackResponseDto
            {
                Success = true,
                TicketNumber = ticketNumber,
                Message = string.IsNullOrEmpty(request.Comment) ? "Rating submitted successfully" : "Feedback submitted successfully",
                CreatedAt = feedback.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting feedback");
            return StatusCode(500, new
            {
                error = "Failed to submit feedback",
                message = "An internal error occurred. Please try again later."
            });
        }
    }

    [HttpGet("personnel")]
    public async Task<IActionResult> GetPersonnel()
    {
        try
        {
            var personnel = await _context.Personnels
                .OrderBy(p => p.Name)
                .Select(p => p.Name)
                .ToListAsync();

            return Ok(personnel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting personnel list");
            return StatusCode(500, new { error = "Failed to retrieve personnel list" });
        }
    }

    private async Task QueueFeedbackEmailsAsync(FeedbackRequestDto feedback, string ticketNumber, string? customerName)
    {
        var teamEmail = _emailQueueService.GetType().Assembly.GetTypes()
            .FirstOrDefault(t => t.Name == "EmailQueueService")?
            .GetProperty("_teamEmail")?.GetValue(null) as string ?? "dispute@xdsdata.com";

        // Queue team email if there's feedback or low rating
        if (!string.IsNullOrEmpty(feedback.Comment) || feedback.Rating < 3)
        {
            var teamEmailHtml = CreateTeamEmailHtml(feedback, ticketNumber, customerName);
            await _emailQueueService.QueueEmailAsync(ticketNumber, teamEmail!, $"New Feedback from {GetDisplayName(feedback, customerName)} [Ticket: {ticketNumber}]", teamEmailHtml, feedback.CustomerEmail);
        }

        // Queue user confirmation email
        if (!string.IsNullOrEmpty(feedback.CustomerEmail) && IsValidEmail(feedback.CustomerEmail))
        {
            var userEmailHtml = CreateUserConfirmationHtml(feedback, ticketNumber, customerName);
            await _emailQueueService.QueueEmailAsync(ticketNumber, feedback.CustomerEmail, $"Thank You for Your Feedback [Ticket: {ticketNumber}]", userEmailHtml);
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

    private static string? Truncate(string? s, int max)
    {
        if (s == null) return null;
        return s.Length <= max ? s : s.Substring(0, max);
    }

    private string CreateTeamEmailHtml(FeedbackRequestDto feedback, string ticketNumber, string? customerName)
    {
        var ratingStars = new string('★', feedback.Rating) + new string('☆', 10 - feedback.Rating);
        var currentYear = DateTime.UtcNow.Year;

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>New Feedback Submission</title>
</head>
<body style='font-family: Arial, sans-serif;'>
    <h1 style='color: #54B847;'>New Feedback Submission</h1>
    <p><strong>Ticket Number:</strong> {ticketNumber}</p>
    <hr>
    <h2>Submission Details</h2>
    <p><strong>Source:</strong> {feedback.Source?.Replace("_", "/").ToUpper()}</p>
    <p><strong>Name:</strong> {GetDisplayName(feedback, customerName)}</p>
    <p><strong>Email:</strong> {feedback.CustomerEmail ?? "Not provided"}</p>
    {(!string.IsNullOrEmpty(feedback.Institution) ? $"<p><strong>Institution:</strong> {feedback.Institution}</p>" : "")}
    {(!string.IsNullOrEmpty(feedback.PersonnelName) ? $"<p><strong>Personnel:</strong> {feedback.PersonnelName}</p>" : "")}
    <p><strong>Rating:</strong> {ratingStars} ({feedback.Rating}/10)</p>
    {(feedback.Comment != null ? $"<h2>Feedback</h2><p>{feedback.Comment}</p>" : "")}
    <hr>
    <p style='font-size: 12px; color: #777;'>XDS Data Ghana &copy; {currentYear}</p>
</body>
</html>";
    }

    private string CreateUserConfirmationHtml(FeedbackRequestDto feedback, string ticketNumber, string? customerName)
    {
        var currentYear = DateTime.UtcNow.Year;

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Thank You for Your Feedback</title>
</head>
<body style='font-family: Arial, sans-serif;'>
    <h1 style='color: #54B847;'>Thank You!</h1>
    <p>Dear {GetDisplayName(feedback, customerName)},</p>
    <p>Thank you for sharing your rating with us. You rated us {feedback.Rating}/10.</p>
    {(feedback.Comment != null ? "<p>We have also received your feedback and truly appreciate your input.</p>" : "")}
    <p><strong>Reference Number:</strong> {ticketNumber}</p>
    <hr>
    <p style='font-size: 12px; color: #777;'>XDS Data Ghana &copy; {currentYear}</p>
</body>
</html>";
    }
}