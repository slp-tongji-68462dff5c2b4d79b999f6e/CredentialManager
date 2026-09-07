using Microsoft.EntityFrameworkCore;

namespace Tjslp.CredentialManager.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<CredentialOwnership> CredentialOwnerships => Set<CredentialOwnership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CredentialOwnership>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Owner);
        });
    }
}
