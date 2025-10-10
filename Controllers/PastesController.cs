using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pasteor_backend.Data;
using pasteor_backend.Models;
using System.Security.Claims;

namespace pasteor_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PastesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PastesController(ApplicationDbContext context)
    {
        _context = context;
    }
    
    // POST: api/pastes
    [HttpPost]
    public async Task<ActionResult<PasteResponse>> CreatePaste([FromBody] CreatePasteRequest request)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "Content is required" });
        }

        if (request.Content.Length > 524288) // 512KB limit
        {
            return BadRequest(new { error = "Content too large (max 512KB)" });
        }
        
        // Generate short unique ID
        var id = GenerateShortId();
        
        // Check if ID already exist
        while (await _context.Pastes.AnyAsync(p => p.Id == id))
        {
            id = GenerateShortId();
        }
        
        // Calculate date of expires
        DateTime? expiresAt = request.ExpiresIn switch
        {
            "1h"  => DateTime.UtcNow.AddHours(1),
            "24h" => DateTime.UtcNow.AddHours(24),
            "7d"  => DateTime.UtcNow.AddDays(7),
            "30d" => DateTime.UtcNow.AddDays(30),
            _     => null // never
        };
        
        // Get user IP
        var clientIP = HttpContext.Connection.RemoteIpAddress?.ToString();
        
        // Check if user is authenticated
        int? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int parsedUserId))
            {
                userId = parsedUserId;
            }
        }

        var paste = new Paste
        {
            Id = id,
            Content = request.Content,
            Title = request.Title,
            Language = request.Language ?? "plaintext",
            ExpiresAt = expiresAt,
            CreatedByIp = clientIP,
            UserId = userId
        };

        _context.Pastes.Add(paste);
        await _context.SaveChangesAsync();

        var response = new PasteResponse
        {
            Id = paste.Id,
            Title = paste.Title,
            Language = paste.Language,
            CreatedAt = paste.CreatedAt,
            ExpiresAt = paste.ExpiresAt,
            Url = $"{Request.Scheme}://{Request.Host}/api/pastes/{paste.Id}",
            IsOwner = userId.HasValue
        };
        
        return CreatedAtAction(nameof(GetPaste), new { id = paste.Id }, response);
    }
    
    // GET: api/pastes/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<PasteDetailResponse>> GetPaste(string id)
    {
        var paste = await _context.Pastes
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (paste == null)
        {
            return NotFound(new { error = "Paste not found" });
        }
    
        // Check if paste expired
        if (paste.ExpiresAt.HasValue && paste.ExpiresAt.Value < DateTime.UtcNow)
        {
            return NotFound(new { error = "Paste has expired" });
        }

        // Check if current user is owner
        bool isOwner = false;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                isOwner = paste.UserId == userId;
            }
        }
        
        if (!isOwner)
        {
            paste.Views++;
            await _context.SaveChangesAsync();
        }

        // Return DTO instead of raw model
        var response = new PasteDetailResponse
        {
            Id = paste.Id,
            Content = paste.Content,
            Title = paste.Title,
            Language = paste.Language,
            CreatedAt = paste.CreatedAt,
            ExpiresAt = paste.ExpiresAt,
            Views = paste.Views,
            IsOwner = isOwner,
            Author = paste.User != null ? new AuthorInfo
            {
                Name = paste.User.Name ?? "Anonymous",
                AvatarUrl = paste.User.AvatarUrl
            } : null
        };
    
        return Ok(response);
    }
    
    // GET: api/pastes/recent
    [HttpGet("recent")]
    public async Task<ActionResult<List<RecentPasteItem>>> GetRecentPastes([FromQuery] int limit = 10)
    {
        if (limit < 1 || limit > 50) limit = 50;

        var pastes = await _context.Pastes
            .Include(p => p.User)
            .Where(p => !p.ExpiresAt.HasValue || p.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .Select(p => new RecentPasteItem
            {
                Id = p.Id,
                Title = p.Title,
                Language = p.Language,
                CreatedAt = p.CreatedAt,
                Views = p.Views,
                AuthorName = p.User != null ? p.User.Name ?? "Anonymous" : "Anonymous",
                Preview = p.Content.Length > 100 ? p.Content.Substring(0, 100) + "..." : p.Content
            })
            .ToListAsync();
        
        return Ok(pastes);
    }
    
    // GET: api/pastes/public-stats
    [HttpGet("public-stats")]
    public async Task<ActionResult<PublicStatsResponse>> GetPublicStats()
    {
        var totalPastes = await _context.Pastes.CountAsync();
        var totalUsers = await _context.Users.CountAsync();
        
        var languageStats = await _context.Pastes
            .GroupBy(p => p.Language)
            .Select(g => new LanguageStats
            {
                Language = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync();
        
        var popularPastes = await _context.Pastes
            .Include(p => p.User)
            .Where(p => !p.ExpiresAt.HasValue || p.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(p => p.Views)
            .Take(5)
            .Select(p => new PopularPasteItem
            {
                Id = p.Id,
                Title = p.Title,
                Language = p.Language,
                Views = p.Views,
                CreatedAt = p.CreatedAt,
                AuthorName = p.User != null ? p.User.Name ?? "Anonymous" : "Anonymous"
            })
            .ToListAsync();

        return Ok(new PublicStatsResponse
        {
            TotalPastes = totalPastes,
            TotalUsers = totalUsers,
            TopLanguages = languageStats,
            PopularPastes = popularPastes
        });
    }
    
    // GET: api/pastes/my
    [HttpGet("my")]
    public async Task<ActionResult<MyPastesResponse>> GetMyPastes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? language = null,
        [FromQuery] string? sortBy = "date")
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "Authentication required" });

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out int userId))
            return Unauthorized(new { error = "Invalid user" });

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _context.Pastes
            .Where(p => p.UserId == userId);
        
        // Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => 
                (p.Title != null && p.Title.Contains(search)) ||
                p.Content.Contains(search));
        }
        
        // Language filter
        if (!string.IsNullOrWhiteSpace(language) && language != "all")
        {
            query = query.Where(p => p.Language == language);
        }
        
        // Sorting
        query = sortBy?.ToLower() switch
        {
            "views" => query.OrderByDescending(p => p.Views),
            "title" => query.OrderBy(p => p.Title ?? p.Id),
            _ => query.OrderByDescending(p => p.CreatedAt) // default: date
        };

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var pastes = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PasteListItem
            {
                Id = p.Id,
                Title = p.Title,
                Language = p.Language,
                CreatedAt = p.CreatedAt,
                ExpiresAt = p.ExpiresAt,
                Views = p.Views,
                Preview = p.Content.Length > 100 ? p.Content.Substring(0, 100) + "..." : p.Content
            })
            .ToListAsync();

        return Ok(new MyPastesResponse
        {
            Pastes = pastes,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }
    
    // PUT: api/pastes/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<PasteResponse>> UpdatePaste(string id, [FromBody] UpdatePasteRequest request)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "Authentication required" });

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out int userId))
            return Unauthorized(new { error = "Invalid user" });

        var paste = await _context.Pastes.FindAsync(id);

        if (paste == null)
            return NotFound(new { error = "Paste not found" });
        
        if (paste.UserId != userId)
            return Forbid();

        // Validation
        if (!string.IsNullOrWhiteSpace(request.Content))
        {
            if (request.Content.Length > 524288)
                return BadRequest(new { error = "Content too large (max 512KB)" });

            paste.Content = request.Content;
        }
        
        if (request.Title != null)
        {
            paste.Title = request.Title;
        }
        
        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            paste.Language = request.Language;
        }
        
        await _context.SaveChangesAsync();
        
        return Ok(new PasteResponse
        {
            Id = paste.Id,
            Title = paste.Title,
            Language = paste.Language,
            CreatedAt = paste.CreatedAt,
            ExpiresAt = paste.ExpiresAt,
            Url = $"{Request.Scheme}://{Request.Host}/api/pastes/{paste.Id}",
            IsOwner = true
        });
    }

    // DELETE: api/pastes/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePaste(string id)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "Authentication required" });

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out int userId))
            return Unauthorized(new { error = "Invalid user" });

        var paste = await _context.Pastes.FindAsync(id);
        
        if (paste == null)
            return NotFound(new { error = "Paste not found" });

        if (paste.UserId != userId)
            return Forbid();

        _context.Pastes.Remove(paste);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // GET: api/pastes/stats
    [HttpGet("stats")]
    public async Task<ActionResult<UserStatsResponse>> GetUserStats()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "Authentication required" });

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out int userId))
            return Unauthorized(new { error = "Invalid user" });

        var pastes = await _context.Pastes
            .Where(p => p.UserId == userId)
            .ToListAsync();

        var stats = new UserStatsResponse
        {
            TotalPastes = pastes.Count,
            TotalViews = pastes.Sum(p => p.Views),
            ActivePastes = pastes.Count(p => !p.ExpiresAt.HasValue || p.ExpiresAt > DateTime.UtcNow),
            MostViewedPaste = pastes.OrderByDescending(p => p.Views).FirstOrDefault()?.Id
        };

        return Ok(stats);
    }
    
    // GET: api/pastes/languages
    [HttpGet("languages")]
    public async Task<ActionResult<List<string>>> GetUserLanguages()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "Authentication required" });
        
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out int userId))
            return Unauthorized(new { error = "Invalid user" });
        
        var languages = await _context.Pastes
            .Where(p => p.UserId == userId)
            .Select(p => p.Language)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync();

        return Ok(languages);
    }
    
    // GET: api/pastes/{id}/raw
    [HttpGet("{id}/raw")]
    public async Task<IActionResult> GetPasteRaw(string id)
    {
        var paste = await _context.Pastes.FindAsync(id);

        if (paste == null)
        {
            return NotFound(new { error = "Paste not found" });
        }
        
        // Check if paste expired
        if (paste.ExpiresAt.HasValue && paste.ExpiresAt.Value < DateTime.UtcNow)
        {
            return NotFound("Paste has expired");
        }
        
        // Return plain text
        return Content(paste.Content, "text/plain; charset=utf-8");
    }

    private string GenerateShortId()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

// DTOs
public record CreatePasteRequest
{
    public string Content { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string? Language { get; init; }
    public string ExpiresIn { get; init; } = "never"; // "1h", "24h", "7d", "30d", "never"
}

public record PasteResponse
{
    public string Id { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string? Language { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string Url { get; init; } = string.Empty;
    public bool IsOwner { get; init; }
}

public record PasteListItem
{
    public string Id { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string Language { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public int Views { get; init; }
    public string Preview { get; init; } = string.Empty;
}

public record MyPastesResponse
{
    public List<PasteListItem> Pastes { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public record UpdatePasteRequest
{
    public string? Content { get; init; }
    public string? Title { get; init; }
    public string? Language { get; init; }
}

public record UserStatsResponse
{
    public int TotalPastes { get; init; }
    public int TotalViews { get; init; }
    public int ActivePastes { get; init; }
    public string? MostViewedPaste { get; init; }
}

public record PasteDetailResponse
{
    public string Id { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string Language { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public int Views { get; init; }
    public bool IsOwner { get; init; }
    public AuthorInfo? Author { get; init; }
}

public record AuthorInfo
{
    public string Name { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
}

public record RecentPasteItem
{
    public string Id { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string Language { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public int Views { get; init; }
    public string AuthorName { get; init; } = string.Empty;
    public string Preview { get; init; } = string.Empty;
}

public record PublicStatsResponse
{
    public int TotalPastes { get; init; }
    public int TotalUsers { get; init; }
    public List<LanguageStats> TopLanguages { get; init; } = new();
    public List<PopularPasteItem> PopularPastes { get; init; } = new();
}

public record LanguageStats
{
    public string Language { get; init; } = string.Empty;
    public int Count { get; init; }
}

public record PopularPasteItem
{
    public string Id { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string Language { get; init; } = string.Empty;
    public int Views { get; init; }
    public DateTime CreatedAt { get; init; }
    public string AuthorName { get; init; } = string.Empty;
}