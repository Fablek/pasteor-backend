using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using pasteor_backend.Data;
using pasteor_backend.Models;
using pasteor_backend.Services;
using System.Security.Claims;

namespace pasteor_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly JwtService _jwtService;

    public AuthController(ApplicationDbContext context, JwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }
    
    [HttpGet("google")]
    public IActionResult GoogleLogin()
    {
        var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GoogleCallback))
        };
        return Challenge(properties, "Google");
    }

    [HttpGet("google-callback")]
    public async Task<IActionResult> GoogleCallback()
    {
        try
        {
            var authenticateResult = await HttpContext.AuthenticateAsync("Google");
            
            if (!authenticateResult.Succeeded)
            {
                var errorMessage = authenticateResult.Failure?.Message ?? "Unknown error";
                Console.WriteLine($"Google auth failed: {errorMessage}");
                return BadRequest(new { error = "Google authentication failed", details = errorMessage });
            }
            
            var claims = authenticateResult.Principal!.Claims;
            var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var providerId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var avatarUrl = claims.FirstOrDefault(c => c.Type == "picture")?.Value;
            
            if (email == null || providerId == null)
            {
                return BadRequest(new { error = "Failed to get user info from Google" });
            }
            
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                user = new User
                {
                    Email = email,
                    Name = name,
                    Provider = "Google",
                    ProviderId = providerId,
                    AvatarUrl = avatarUrl
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            
            var token = _jwtService.GenerateToken(user);
            
            return Redirect($"http://localhost:3000/auth/callback?token={token}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in GoogleCallback: {ex.Message}");
            throw;
        }
    }

    [HttpGet("github")]
    public IActionResult GitHubLogin()
    {
        var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GitHubCallback))
        };
        return Challenge(properties, "GitHub");
    }
    
    [HttpGet("github-callback")]
    public async Task<IActionResult> GitHubCallback()
    {
        try
        {
            var authenticateResult = await HttpContext.AuthenticateAsync("GitHub");
        
            if (!authenticateResult.Succeeded)
                return BadRequest(new { error = "GitHub authentication failed" });

            var claims = authenticateResult.Principal!.Claims;
            var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var providerId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var avatarUrl = claims.FirstOrDefault(c => c.Type == "urn:github:avatar")?.Value;

            if (providerId == null)
                return BadRequest(new { error = "Failed to get user info from GitHub" });
            
            email ??= $"{providerId}@github.user";
            
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                user = new User
                {
                    Email = email,
                    Name = name ?? "GitHub User",
                    Provider = "GitHub",
                    ProviderId = providerId,
                    AvatarUrl = avatarUrl
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }

            var token = _jwtService.GenerateToken(user);

            return Redirect($"http://localhost:3000/auth/callback?token={token}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in GitHubCallback: {ex.Message}");
            throw;
        }
    }
    
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Unauthorized();

        var userId = int.Parse(userIdClaim.Value);
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
            return NotFound();

        return Ok(new
        {
            user.Id,
            user.Email,
            user.Name,
            user.AvatarUrl,
            user.Provider
        });
    }
}