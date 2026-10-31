using AspNetCore.Dispatcher;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MikrotikScheduler.Application.Common;
using MikrotikScheduler.Domain.Entities;
using MikrotikScheduler.Domain.Enums;

namespace MikrotikScheduler.Application.Features.Auth;

public record AuthResult(string AccessToken, DateTime ExpiresAt, string RefreshToken, string Username, string Role);

public record RegisterCommand(string Username, string Email, string Password) : ICommand<AuthResult>;
public record LoginCommand(string Username, string Password) : ICommand<AuthResult>;
public record RefreshTokenCommand(string RefreshToken) : ICommand<AuthResult>;

public class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MinimumLength(3).MaximumLength(64);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).MaximumLength(128);
    }
}

public class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

internal static class AuthHelper
{
    public static async Task<AuthResult> IssueAsync(User user, IAppDbContext db, IJwtService jwt, CancellationToken ct)
    {
        var access = jwt.CreateAccessToken(user);
        var refresh = new RefreshToken
        {
            UserId = user.Id,
            Token = jwt.CreateRefreshTokenValue(),
            ExpiresAt = DateTime.UtcNow.AddDays(jwt.RefreshTokenDays)
        };
        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync(ct);
        return new AuthResult(access.Token, access.ExpiresAt, refresh.Token, user.Username, user.Role.ToString());
    }
}

public class RegisterHandler(IAppDbContext db, IPasswordHasher hasher, IJwtService jwt)
    : IRequestHandler<RegisterCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RegisterCommand r, CancellationToken ct)
    {
        var username = r.Username.Trim();
        if (await db.Users.AnyAsync(u => u.Username == username || u.Email == r.Email, ct))
            throw new ConflictException("Username or email already exists.");

        var first = !await db.Users.AnyAsync(ct);
        var user = new User
        {
            Username = username,
            Email = r.Email.Trim(),
            PasswordHash = hasher.Hash(r.Password),
            Role = first ? UserRole.Admin : UserRole.Operator
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return await AuthHelper.IssueAsync(user, db, jwt, ct);
    }
}

public class LoginHandler(IAppDbContext db, IPasswordHasher hasher, IJwtService jwt)
    : IRequestHandler<LoginCommand, AuthResult>
{
    public async Task<AuthResult> Handle(LoginCommand r, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == r.Username, ct);
        if (user is null || !user.IsActive || !hasher.Verify(r.Password, user.PasswordHash))
            throw new UnauthorizedAppException("Invalid username or password.");
        return await AuthHelper.IssueAsync(user, db, jwt, ct);
    }
}

public class RefreshTokenHandler(IAppDbContext db, IJwtService jwt)
    : IRequestHandler<RefreshTokenCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RefreshTokenCommand r, CancellationToken ct)
    {
        var token = await db.RefreshTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == r.RefreshToken, ct);
        if (token is null || !token.IsActive || !token.User.IsActive)
            throw new UnauthorizedAppException("Invalid refresh token.");

        token.RevokedAt = DateTime.UtcNow; // rotate
        return await AuthHelper.IssueAsync(token.User, db, jwt, ct);
    }
}
