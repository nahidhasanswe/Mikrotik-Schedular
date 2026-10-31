namespace MikrotikScheduler.Domain.Entities;

public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Token { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.UtcNow;
    public User User { get; set; } = default!;
}
