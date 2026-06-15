using System.ComponentModel.DataAnnotations;

namespace FeedBack.API.Models;

public enum EmailStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Retrying = 3
}

public class EmailQueue
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string TicketNumber { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string ToEmail { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;
    
    [Required]
    public string HtmlBody { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? FromEmail { get; set; }
    
    [MaxLength(100)]
    public string? ReplyTo { get; set; }
    
    public int AttemptCount { get; set; } = 0;
    
    public int MaxAttempts { get; set; } = 3;
    
    public EmailStatus Status { get; set; } = EmailStatus.Pending;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastAttemptAt { get; set; }
    
    public DateTime? SentAt { get; set; }
    
    public string? ErrorMessage { get; set; }
}