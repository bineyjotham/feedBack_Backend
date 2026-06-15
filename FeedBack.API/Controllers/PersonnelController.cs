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
public class PersonnelController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PersonnelController> _logger;

    public PersonnelController(ApplicationDbContext context, ILogger<PersonnelController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var personnel = await _context.Personnels
                .OrderBy(p => p.Name)
                .Select(p => new PersonnelDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            return Ok(personnel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting personnel list");
            return StatusCode(500, new { error = "Failed to retrieve personnel list" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddPersonnelDto request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { error = "Name is required" });
            }

            // Check if personnel with same name already exists
            var exists = await _context.Personnels
                .AnyAsync(p => p.Name.ToLower() == request.Name.Trim().ToLower());

            if (exists)
            {
                return BadRequest(new { error = "Personnel with this name already exists" });
            }

            var personnel = new Personnel
            {
                Name = request.Name.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await _context.Personnels.AddAsync(personnel);
            await _context.SaveChangesAsync();

            return Ok(new PersonnelDto
            {
                Id = personnel.Id,
                Name = personnel.Name,
                CreatedAt = personnel.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding personnel");
            return StatusCode(500, new { error = "Failed to add personnel" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var personnel = await _context.Personnels.FindAsync(id);
            if (personnel == null)
            {
                return NotFound(new { error = "Personnel not found" });
            }

            _context.Personnels.Remove(personnel);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Personnel deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting personnel with id {Id}", id);
            return StatusCode(500, new { error = "Failed to delete personnel" });
        }
    }
}
