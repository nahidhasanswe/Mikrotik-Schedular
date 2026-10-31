using Microsoft.EntityFrameworkCore;
using MikrotikScheduler.Application.Common;
using MikrotikScheduler.Domain.Entities;

namespace MikrotikScheduler.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<MikrotikRouter> Routers => Set<MikrotikRouter>();
    public DbSet<PppoeUser> PppoeUsers => Set<PppoeUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.Property(x => x.Username).HasMaxLength(64).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<MikrotikRouter>(e =>
        {
            e.ToTable("MikrotikRouters");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Host).HasMaxLength(255).IsRequired();
            e.Property(x => x.ApiUsername).HasMaxLength(100).IsRequired();
            e.Property(x => x.ApiPasswordEncrypted).HasMaxLength(2048).IsRequired();
            e.HasOne(x => x.User).WithMany(u => u.Routers).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
        });

        b.Entity<PppoeUser>(e =>
        {
            e.ToTable("PppoeUsers");
            e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastError).HasMaxLength(1000);
            e.HasOne(x => x.Router).WithMany(r => r.PppoeUsers).HasForeignKey(x => x.RouterId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.RouterId, x.Username }).IsUnique();
            e.HasIndex(x => new { x.RouterId, x.IsActive, x.ExpiresAt });
        });

        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("RefreshTokens");
            e.Property(x => x.Token).HasMaxLength(200).IsRequired();
            e.Ignore(x => x.IsActive);
            e.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Token).IsUnique();
        });

        b.Entity<SyncLog>(e =>
        {
            e.ToTable("SyncLogs");
            e.Property(x => x.Action).HasMaxLength(50).IsRequired();
            e.Property(x => x.Message).HasMaxLength(1000);
            e.HasOne(x => x.Router).WithMany().HasForeignKey(x => x.RouterId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.RouterId, x.Id });
        });
    }
}

/// <summary>Lets `dotnet ef` create migrations without needing a live database.</summary>
public class DesignTimeFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                 ?? "Server=localhost;Database=MikrotikScheduler;User Id=sa;Password=x;TrustServerCertificate=True";
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cs).Options;
        return new AppDbContext(opts);
    }
}
