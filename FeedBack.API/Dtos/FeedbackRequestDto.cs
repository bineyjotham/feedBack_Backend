using System.ComponentModel.DataAnnotations;

namespace FeedBack.API.Dtos;

public class FeedbackRequestDto
{
    [Required]
    [Range(0, 10)]
    public int Rating { get; set; }
    
    public string? Source { get; set; } = "website";
    
    [MaxLength(200)]
    public string? Comment { get; set; }
    
    [MaxLength(100)]
    public string? CustomerName { get; set; }
    
    [EmailAddress]
    [MaxLength(100)]
    public string? CustomerEmail { get; set; }
    
    [MaxLength(200)]
    public string? Institution { get; set; }
    
    [MaxLength(50)]
    public string? Category { get; set; }
    
    [MaxLength(100)]
    public string? PersonnelName { get; set; }
    
    public Dictionary<string, object>? Metadata { get; set; }
}

public class FeedbackResponseDto
{
    public bool Success { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}