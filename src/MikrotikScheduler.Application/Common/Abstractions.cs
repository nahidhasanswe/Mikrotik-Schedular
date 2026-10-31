using Microsoft.EntityFrameworkCore;
using MikrotikScheduler.Domain.Entities;

namespace MikrotikScheduler.Application.Common;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<MikrotikRouter> Routers { get; }
    DbSet<PppoeUser> PppoeUsers { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<SyncLog> SyncLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface ICurrentUser
{
    int UserId { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Reversible encryption for router/PPPoE passwords (they must be sent to the router in clear text).</summary>
public interface ISecretProtector
{
    string Protect(string plain);
    string Unprotect(string protectedValue);
}

public record AccessToken(string Token, DateTime ExpiresAt);

public interface IJwtService
{
    AccessToken CreateAccessToken(User user);
    string CreateRefreshTokenValue();
    int RefreshTokenDays { get; }
}

// ---- RouterOS ----
public record RouterConnection(string Host, int Port, bool UseSsl, string Username, string Password);

public record PppSecret(string Id, string Name, bool Disabled);

public interface IRouterOsSession : IAsyncDisposable
{
    Task<IReadOnlyList<PppSecret>> GetPppSecretsAsync(CancellationToken ct);
    Task EnablePppSecretAsync(string id, CancellationToken ct);
}

public interface IRouterOsClient
{
    /// <summary>Opens and authenticates a session. Throws on connection/login failure.</summary>
    Task<IRouterOsSession> ConnectAsync(RouterConnection connection, CancellationToken ct);
}
