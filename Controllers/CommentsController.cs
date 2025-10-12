using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pasteor_backend.Data;
using pasteor_backend.Models;
using System.Security.Claims;

namespace pasteor_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CommentsController(ApplicationDbContext context)
    {
        _context = context;
    }
    
    // GET: api/comments/paste/{pasteId}
    [HttpGet("paste/{pasteId}")]
    public async Task<ActionResult<List<CommentResponse>>> GetComments(string pasteId)
    {
        var comments = await _context.Comments
            .Include(c => c.User)
            .Where(c => c.PasteId == pasteId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CommentResponse
            {
                Id = c.Id,
                Content = c.Content,
                CreatedAt = c.CreatedAt,
                AuthorName = c.User != null ? c.User.Name ?? "Anonymous" : (c.AuthorName ?? "Anonymous"),
                AuthorAvatar = c.User != null ? c.User.AvatarUrl : null,
                IsOwner = false
            })
            .ToListAsync();
        
        // Check if current user is owner of each comment
        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                var userCommentIds = await _context.Comments
                    .Where(c => c.PasteId == pasteId && c.UserId == userId)
                    .Select(c => c.Id)
                    .ToListAsync();

                foreach (var comment in comments)
                {
                    comment.IsOwner = userCommentIds.Contains(comment.Id);
                }
            }
        }
        
        return Ok(comments);
    }
    
    // POST: api/comments
    [HttpPost]
    public async Task<ActionResult<CommentResponse>> CreateComment([FromBody] CreateCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "Content is required" });
        
        if (request.Content.Length > 2000)
            return BadRequest(new { error = "Comment too long (max 2000 characters)" });
        
        // Check if paste exists
        var paste = await _context.Pastes.FindAsync(request.PasteId);
        if (paste == null)
            return NotFound(new { error = "Paste not found" });
        
        // Get user if authenticated
        int? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int parsedUserId))
            {
                userId = parsedUserId;
            }
        }
        
        var comment = new Comment
        {
            PasteId = request.PasteId,
            Content = request.Content,
            UserId = userId,
            AuthorName = userId == null ? request.AuthorName ?? "Anonymous" : null
        };
        
        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();
        
        // Load user data
        await _context.Entry(comment).Reference(c => c.User).LoadAsync();
        
        var response = new CommentResponse
        {
            Id = comment.Id,
            Content = comment.Content,
            CreatedAt = comment.CreatedAt,
            AuthorName = comment.User?.Name ?? comment.AuthorName ?? "Anonymous",
            AuthorAvatar = comment.User?.AvatarUrl,
            IsOwner = userId.HasValue
        };
        
        return CreatedAtAction(nameof(GetComments), new { pasteId = request.PasteId }, response);
    }
    
    // DELETE: api/comments/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteComment(int id)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "Authentication required" });

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out int userId))
            return Unauthorized(new { error = "Invalid user" });
        
        var comment = await _context.Comments.FindAsync(id);
        
        if (comment == null)
            return NotFound(new { error = "Comment not found" });
        
        if (comment.UserId != userId)
            return Forbid();
        
        _context.Comments.Remove(comment);
        await _context.SaveChangesAsync();
        
        return NoContent();
    }
}

// DTOs
public record CreateCommentRequest
{
    public string PasteId { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string? AuthorName { get; init; }
}

public record CommentResponse
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatar { get; set; }
    public bool IsOwner { get; set; }
}