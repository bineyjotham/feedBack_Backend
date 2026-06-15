using FeedBack.API.Data;
using FeedBack.API.Dtos;
using FeedBack.API.Models;
using FeedBack.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
// using BCrypt.Net;

namespace FeedBack.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(ApplicationDbContext context, IJwtService jwtService, ILogger<AuthController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower());

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Failed login attempt for email: {Email}", request.Email);
                return Unauthorized(new AuthResponseDto
                {
                    Success = false,
                    Message = "Invalid email or password"
                });
            }

            if (!user.IsActive)
            {
                return Unauthorized(new AuthResponseDto
                {
                    Success = false,
                    Message = "Account is deactivated"
                });
            }

            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var token = _jwtService.GenerateToken(user);

            return Ok(new AuthResponseDto
            {
                Success = true,
                Token = token,
                User = new UserInfoDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    Role = user.Role
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for {Email}", request.Email);
            return StatusCode(500, new AuthResponseDto
            {
                Success = false,
                Message = "An error occurred during login"
            });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        try
        {
            // Check if user exists
            var existingUser = await _context.Users
                .AnyAsync(u => u.Email == request.Email.ToLower());

            if (existingUser)
            {
                return BadRequest(new AuthResponseDto
                {
                    Success = false,
                    Message = "User already exists"
                });
            }

            var user = new User
            {
                Email = request.Email.ToLower(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FullName = request.FullName,
                Role = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            var token = _jwtService.GenerateToken(user);

            return Ok(new AuthResponseDto
            {
                Success = true,
                Token = token,
                User = new UserInfoDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    Role = user.Role
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for {Email}", request.Email);
            return StatusCode(500, new AuthResponseDto
            {
                Success = false,
                Message = "An error occurred during registration"
            });
        }
    }
}

// using FeedBack.API.Data;
// using FeedBack.API.Dtos;
// using FeedBack.API.Models;
// using FeedBack.API.Services;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.EntityFrameworkCore;

// namespace FeedBack.API.Controllers;

// [ApiController]
// [Route("api/[controller]")]
// public class AuthController : ControllerBase
// {
//     private readonly ApplicationDbContext _context;
//     private readonly IJwtService _jwtService;
//     private readonly ILogger<AuthController> _logger;

//     public AuthController(ApplicationDbContext context, IJwtService jwtService, ILogger<AuthController> logger)
//     {
//         _context = context;
//         _jwtService = jwtService;
//         _logger = logger;
//     }

//     [HttpPost("login")]
//     public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
//     {
//         try
//         {
//             var user = await _context.Users
//                 .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower());

//             if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
//             {
//                 _logger.LogWarning("Failed login attempt for email: {Email}", request.Email);
//                 return Unauthorized(new AuthResponseDto
//                 {
//                     Success = false,
//                     Message = "Invalid email or password",
//                     Token = null,
//                     User = null
//                 });
//             }

//             if (!user.IsActive)
//             {
//                 return Unauthorized(new AuthResponseDto
//                 {
//                     Success = false,
//                     Message = "Account is deactivated",
//                     Token = null,
//                     User = null
//                 });
//             }

//             user.LastLoginAt = DateTime.UtcNow;
//             await _context.SaveChangesAsync();

//             var token = _jwtService.GenerateToken(user);

//             return Ok(new AuthResponseDto
//             {
//                 Success = true,
//                 Token = token,
//                 Message = "Login successful",
//                 User = new UserInfoDto
//                 {
//                     Id = user.Id,
//                     Email = user.Email,
//                     FullName = user.FullName,
//                     Role = user.Role
//                 }
//             });
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex, "Error during login for {Email}", request.Email);
//             return StatusCode(500, new AuthResponseDto
//             {
//                 Success = false,
//                 Message = "An error occurred during login",
//                 Token = null,
//                 User = null
//             });
//         }
//     }

//     [HttpPost("register")]
//     public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
//     {
//         try
//         {
//             // Check if user exists
//             var existingUser = await _context.Users
//                 .AnyAsync(u => u.Email == request.Email.ToLower());

//             if (existingUser)
//             {
//                 return BadRequest(new AuthResponseDto
//                 {
//                     Success = false,
//                     Message = "User already exists",
//                     Token = null,
//                     User = null
//                 });
//             }

//             var user = new User
//             {
//                 Email = request.Email.ToLower(),
//                 PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
//                 FullName = request.FullName,
//                 Role = "User",
//                 IsActive = true,
//                 CreatedAt = DateTime.UtcNow
//             };

//             await _context.Users.AddAsync(user);
//             await _context.SaveChangesAsync();

//             var token = _jwtService.GenerateToken(user);

//             return Ok(new AuthResponseDto
//             {
//                 Success = true,
//                 Token = token,
//                 Message = "Registration successful",
//                 User = new UserInfoDto
//                 {
//                     Id = user.Id,
//                     Email = user.Email,
//                     FullName = user.FullName,
//                     Role = user.Role
//                 }
//             });
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex, "Error during registration for {Email}", request.Email);
//             return StatusCode(500, new AuthResponseDto
//             {
//                 Success = false,
//                 Message = "An error occurred during registration",
//                 Token = null,
//                 User = null
//             });
//         }
//     }
// }