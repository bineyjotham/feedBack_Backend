using System.ComponentModel.DataAnnotations;

namespace FeedBack.API.Dtos;

public class DashboardStatsDto
{
    public int TotalFeedbacks { get; set; }
    public double AverageRating { get; set; }
    public int NPS { get; set; }
    public int Promoters { get; set; }
    public int Passives { get; set; }
    public int Detractors { get; set; }
    public Dictionary<string, SourceStatsDto> BySource { get; set; } = new();
    public List<TrendDataDto> ThirtyDayTrend { get; set; } = new();
    public List<RatingDistributionDto> RatingDistribution { get; set; } = new();
    public List<PersonnelStatsDto> PersonnelStats { get; set; } = new();
}

public class SourceStatsDto
{
    public int Count { get; set; }
    public double AverageRating { get; set; }
}

public class TrendDataDto
{
    public string Date { get; set; } = string.Empty;
    public double? AverageRating { get; set; }
    public int Count { get; set; }
}

public class RatingDistributionDto
{
    public int Rating { get; set; }
    public int Count { get; set; }
}

public class PersonnelStatsDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public double AverageRating { get; set; }
    public int Promoters { get; set; }
    public int Detractors { get; set; }
    public int NPS { get; set; }
}

public class PersonnelDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AddPersonnelDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
}