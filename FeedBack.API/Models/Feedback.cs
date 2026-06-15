using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FeedBack.API.Models;

public enum FeedbackSource
{
    premises = 0,
    product_service = 1,
    website = 2
}

public enum FeedbackStatus
{
    Pending = 0,
    Reviewed = 1,
    Actioned = 2,
    Archived = 3
}

public class Feedback
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string TicketNumber { get; set; } = string.Empty;
    
    public FeedbackSource Source { get; set; }
    
    [Required]
    [Range(0, 10)]
    public int Rating { get; set; }
    
    [MaxLength(2000)]
    public string? Comment { get; set; }
    
    [MaxLength(200)]
    public string? CustomerName { get; set; }
    
    [MaxLength(255)]
    [EmailAddress]
    public string? CustomerEmail { get; set; }
    
    [MaxLength(200)]
    public string? Institution { get; set; }
    
    [MaxLength(100)]
    public string? Category { get; set; }
    
    [MaxLength(200)]
    public string? PersonnelName { get; set; }
    
    [MaxLength(500)]
    public string? UserAgent { get; set; }
    
    [MaxLength(45)]
    public string? IpAddress { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public FeedbackStatus Status { get; set; } = FeedbackStatus.Pending;
    
    public DateTime? ReviewedAt { get; set; }
    
    public int? ReviewedByUserId { get; set; }
    
    [MaxLength(100)]
    public string? Notes { get; set; }
    
    // Navigation property
    [ForeignKey("ReviewedByUserId")]
    public virtual User? ReviewedBy { get; set; }
}