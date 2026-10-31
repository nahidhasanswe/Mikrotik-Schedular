using System.ComponentModel.DataAnnotations;

namespace MikrotikScheduler.Web.Models;

public record AuthResult(string AccessToken, DateTime ExpiresAt, string RefreshToken, string Username, string Role);
public record RouterDto(int Id, string Name, string Host, int Port, bool UseSsl, string ApiUsername, bool IsOnline, DateTime? LastSeenAt, int PppoeUserCount);
public record TestConnectionResult(bool Success, string Message);
public record PppoeUserDto(int Id, int RouterId, string RouterName, string Username, bool IsActive, DateTime? ExpiresAt, bool IsExpired, string SyncStatus, DateTime? LastSyncedAt, string? LastError, DateTime CreatedAt);
public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);
public record SyncResult(int RouterId, bool Online, int Enabled, int AlreadyEnabled, int Failed, string? Error);
public record SyncLogDto(long Id, int RouterId, int? PppoeUserId, string Action, bool Success, string? Message, DateTime CreatedAt);

public class LoginViewModel
{
    [Required] public string Username { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
}

public class RegisterViewModel
{
    [Required, StringLength(64, MinimumLength = 3)] public string Username { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 6), DataType(DataType.Password)] public string Password { get; set; } = "";
}

public class RouterFormModel
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(255)] public string Host { get; set; } = "";
    [Range(1, 65535)] public int Port { get; set; } = 8728;
    [Display(Name = "Use SSL (api-ssl)")] public bool UseSsl { get; set; }
    [Required, Display(Name = "API username")] public string ApiUsername { get; set; } = "";
    [DataType(DataType.Password), Display(Name = "API password")] public string? ApiPassword { get; set; }
}

public class PppoeFormModel
{
    public int Id { get; set; }
    [Display(Name = "Router")] public int RouterId { get; set; }
    [Required, StringLength(100)] public string Username { get; set; } = "";
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Active until (UTC)")] public DateTime? ExpiresAt { get; set; }
    public List<RouterDto> Routers { get; set; } = new();
}

public class PppoeListViewModel
{
    public PagedResult<PppoeUserDto> Result { get; set; } = new(new(), 0, 1, 20);
    public List<RouterDto> Routers { get; set; } = new();
    public int? RouterId { get; set; }
    public bool? IsActive { get; set; }
    public string? Search { get; set; }
}
