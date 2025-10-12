namespace pasteor_backend.Models;

public class Comment
{
    public int Id { get; set; }
    public string PasteId { get; set; } = null!;
    public Paste Paste { get; set; } = null!;
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string Content { get; set; } = null!;
    public string? AuthorName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}