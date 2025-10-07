using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using pasteor_backend.Models;

namespace pasteor_backend.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) {}
    
    public DbSet<Paste> Pastes { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Paste>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Language).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.HasIndex(e => e.CreatedAt);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Pastes)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Provider).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProviderId).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => new { e.Provider, e.ProviderId }).IsUnique();
            entity.HasIndex(e => e.Email);
        });
    }
}

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=pasteordb;Username=pasteor;Password=pasteor123");

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}