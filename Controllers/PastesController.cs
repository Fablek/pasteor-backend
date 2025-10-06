using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pasteor_backend.Data;
using pasteor_backend.Models;

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

        var paste = new Paste
        {
            Id = id,
            Content = request.Content,
            Title = request.Title,
            Language = request.Language ?? "plaintext",
            ExpiresAt = expiresAt,
            CreatedByIp = clientIP
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
            Url = $"{Request.Scheme}://{Request.Host}/api/pastes/{paste.Id}"
        };
        
        return CreatedAtAction(nameof(GetPaste), new { id = paste.Id }, response);
    }
    
    // GET: api/pastes/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Paste>> GetPaste(string id)
    {
        var paste = await _context.Pastes.FindAsync(id);

        if (paste == null)
        {
            return NotFound(new { error = "Paste not found" });
        }
        
        // Check if paste expired
        if (paste.ExpiresAt.HasValue && paste.ExpiresAt.Value < DateTime.UtcNow)
        {
            return NotFound(new { error = "Paste has expired" });
        }
        
        // Increment views
        paste.Views++;
        await _context.SaveChangesAsync();
        
        return Ok(paste);
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
}