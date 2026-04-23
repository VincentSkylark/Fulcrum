using Microsoft.EntityFrameworkCore;

namespace Fulcrum.API.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserProfile>(e =>
        {
            e.ToTable("user_profiles", "auth");
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.KratosIdentityId).IsUnique();
            e.HasIndex(p => p.Email).IsUnique();
            e.HasIndex(p => p.Username).IsUnique();
        });
    }
}
