using FeedBack.API.Data;
using FeedBack.API.Dtos;
using FeedBack.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FeedBack.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var feedbacks = await _context.Feedbacks.ToListAsync();
            
            var total = feedbacks.Count;
            var avgRating = total > 0 ? feedbacks.Average(f => f.Rating) : 0;
            var promoters = feedbacks.Count(f => f.Rating >= 9);
            var passives = feedbacks.Count(f => f.Rating >= 7 && f.Rating <= 8);
            var detractors = feedbacks.Count(f => f.Rating <= 6);
            var nps = total > 0 ? (int)Math.Round(((promoters - detractors) / (double)total) * 100) : 0;

            var bySource = new Dictionary<string, SourceStatsDto>();
            foreach (FeedbackSource source in Enum.GetValues(typeof(FeedbackSource)))
            {
                var sourceFeedbacks = feedbacks.Where(f => f.Source == source).ToList();
                bySource[source.ToString()] = new SourceStatsDto
                {
                    Count = sourceFeedbacks.Count,
                    AverageRating = sourceFeedbacks.Any() ? sourceFeedbacks.Average(f => f.Rating) : 0
                };
            }

            var trendData = new List<TrendDataDto>();
            for (int i = 29; i >= 0; i--)
            {
                var date = DateTime.UtcNow.AddDays(-i).Date;
                var dayFeedbacks = feedbacks.Where(f => f.CreatedAt.Date == date).ToList();
                trendData.Add(new TrendDataDto
                {
                    Date = date.ToString("MMM d"),
                    AverageRating = dayFeedbacks.Any() ? dayFeedbacks.Average(f => f.Rating) : null,
                    Count = dayFeedbacks.Count
                });
            }

            var ratingDistribution = Enumerable.Range(0, 11)
                .Select(r => new RatingDistributionDto
                {
                    Rating = r,
                    Count = feedbacks.Count(f => f.Rating == r)
                })
                .ToList();

            var personnelStats = feedbacks
                .Where(f => !string.IsNullOrEmpty(f.PersonnelName))
                .GroupBy(f => f.PersonnelName!)
                .Select(g => new PersonnelStatsDto
                {
                    Name = g.Key,
                    Count = g.Count(),
                    AverageRating = g.Average(f => f.Rating),
                    Promoters = g.Count(f => f.Rating >= 9),
                    Detractors = g.Count(f => f.Rating <= 6)
                })
                .Select(p => new PersonnelStatsDto
                {
                    Name = p.Name,
                    Count = p.Count,
                    AverageRating = Math.Round(p.AverageRating, 1),
                    Promoters = p.Promoters,
                    Detractors = p.Detractors,
                    NPS = (int)Math.Round(((p.Promoters - p.Detractors) / (double)p.Count) * 100)
                })
                .OrderByDescending(p => p.AverageRating)
                .ThenByDescending(p => p.Count)
                .ToList();

            return Ok(new DashboardStatsDto
            {
                TotalFeedbacks = total,
                AverageRating = Math.Round(avgRating, 1),
                NPS = nps,
                Promoters = promoters,
                Passives = passives,
                Detractors = detractors,
                BySource = bySource,
                ThirtyDayTrend = trendData,
                RatingDistribution = ratingDistribution,
                PersonnelStats = personnelStats
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard stats");
            return StatusCode(500, new { error = "Failed to retrieve dashboard stats" });
        }
    }

    [HttpGet("feedbacks")]
    public async Task<IActionResult> GetFeedbacks(
        [FromQuery] string? source = null,
        [FromQuery] string? personnel = null,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 200)
    {
        try
        {
            var query = _context.Feedbacks.AsQueryable();

            if (!string.IsNullOrEmpty(source) && Enum.TryParse<FeedbackSource>(source, out var sourceEnum))
            {
                query = query.Where(f => f.Source == sourceEnum);
            }

            if (!string.IsNullOrEmpty(personnel))
            {
                query = query.Where(f => f.PersonnelName != null && f.PersonnelName.Contains(personnel));
            }

            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(f => 
                    (f.Comment != null && f.Comment.ToLower().Contains(searchLower)) ||
                    (f.CustomerName != null && f.CustomerName.ToLower().Contains(searchLower)) ||
                    (f.CustomerEmail != null && f.CustomerEmail.ToLower().Contains(searchLower)) ||
                    (f.Institution != null && f.Institution.ToLower().Contains(searchLower)) ||
                    (f.Category != null && f.Category.ToLower().Contains(searchLower)));
            }

            var feedbacks = await query
                .OrderByDescending(f => f.CreatedAt)
                .Take(limit)
                .Select(f => new
                {
                    f.Id,
                    f.TicketNumber,
                    Source = f.Source.ToString(),
                    f.Rating,
                    f.Comment,
                    f.CustomerName,
                    f.CustomerEmail,
                    f.Institution,
                    f.Category,
                    f.PersonnelName,
                    CreatedAt = f.CreatedAt
                })
                .ToListAsync();

            return Ok(feedbacks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting feedbacks");
            return StatusCode(500, new { error = "Failed to retrieve feedbacks" });
        }
    }

    [HttpGet("personnel")]
    public async Task<IActionResult> GetPersonnel()
    {
        try
        {
            var personnel = await _context.Feedbacks
                .Where(f => !string.IsNullOrEmpty(f.PersonnelName))
                .Select(f => f.PersonnelName!)
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync();

            return Ok(personnel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting personnel list");
            return StatusCode(500, new { error = "Failed to retrieve personnel list" });
        }
    }
}