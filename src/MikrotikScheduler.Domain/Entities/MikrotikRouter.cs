namespace MikrotikScheduler.Domain.Entities;

public class MikrotikRouter
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = default!;
    public string Host { get; set; } = default!;
    public int Port { get; set; } = 8728;
    public bool UseSsl { get; set; }
    public string ApiUsername { get; set; } = default!;
    public string ApiPasswordEncrypted { get; set; } = default!;
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = default!;
    public ICollection<PppoeUser> PppoeUsers { get; set; } = new List<PppoeUser>();
}
