namespace pasteor_backend.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
    public string Provider { get; set; } = null!; // "Google", "GitHub", "Local"
    public string? ProviderId { get; set; }
    public string? PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public ICollection<Paste> Pastes { get; set; } = new List<Paste>();
}