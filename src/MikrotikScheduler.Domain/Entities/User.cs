using MikrotikScheduler.Domain.Enums;

namespace MikrotikScheduler.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public UserRole Role { get; set; } = UserRole.Operator;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MikrotikRouter> Routers { get; set; } = new List<MikrotikRouter>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
