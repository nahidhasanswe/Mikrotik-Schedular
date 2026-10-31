using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MikrotikScheduler.Application.Common;
using MikrotikScheduler.Domain.Entities;

namespace MikrotikScheduler.Infrastructure.Services;

public class JwtOptions
{
    public const string Section = "Jwt";
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "MikrotikScheduler";
    public string Audience { get; set; } = "MikrotikScheduler";
    public int AccessMinutes { get; set; } = 30;
    public int RefreshDays { get; set; } = 14;
}

public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 210_000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iter)) return false;
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iter, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("MikrotikScheduler.Secrets.v1");
    public string Protect(string plain) => _protector.Protect(plain);
    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}

public class JwtService(IOptions<JwtOptions> options) : IJwtService
{
    private readonly JwtOptions _o = options.Value;
    public int RefreshTokenDays => _o.RefreshDays;

    public AccessToken CreateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.Key));
        var expires = DateTime.UtcNow.AddMinutes(_o.AccessMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        var token = new JwtSecurityToken(_o.Issuer, _o.Audience, claims, expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public string CreateRefreshTokenValue() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}
