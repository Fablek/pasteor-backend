namespace pasteor_backend.Models;

public class Paste
{
    public string Id { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string? Title { get; set; }
    public string Language { get; set; } = "plaintext";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public int Views { get; set; } = 0;
    public string? CreatedByIp { get; set; }
}