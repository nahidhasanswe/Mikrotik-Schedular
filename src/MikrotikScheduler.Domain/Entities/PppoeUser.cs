using MikrotikScheduler.Domain.Enums;

namespace MikrotikScheduler.Domain.Entities;

public class PppoeUser
{
    public int Id { get; set; }
    public int RouterId { get; set; }
    public string Username { get; set; } = default!;

    /// <summary>Switch: when false the user is never enabled by the worker.</summary>
    public bool IsActive { get; set; }

    /// <summary>UTC. The user is active until this moment; null = never expires.</summary>
    public DateTime? ExpiresAt { get; set; }

    public bool IsExpired(DateTime utcNow) => ExpiresAt != null && ExpiresAt <= utcNow;
    public bool IsCurrentlyActive(DateTime utcNow) => IsActive && !IsExpired(utcNow);

    public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
    public DateTime? LastSyncedAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public MikrotikRouter Router { get; set; } = default!;
}
