namespace MikrotikScheduler.Domain.Entities;

public class SyncLog
{
    public long Id { get; set; }
    public int RouterId { get; set; }
    public int? PppoeUserId { get; set; }
    public string Action { get; set; } = default!;
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public MikrotikRouter Router { get; set; } = default!;
}
