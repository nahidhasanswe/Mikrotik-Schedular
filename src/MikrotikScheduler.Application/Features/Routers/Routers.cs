using AspNetCore.Dispatcher;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MikrotikScheduler.Application.Common;
using MikrotikScheduler.Domain.Entities;

namespace MikrotikScheduler.Application.Features.Routers;

public record RouterDto(int Id, string Name, string Host, int Port, bool UseSsl, string ApiUsername,
    bool IsOnline, DateTime? LastSeenAt, int PppoeUserCount);

public record CreateRouterCommand(string Name, string Host, int Port, bool UseSsl, string ApiUsername, string ApiPassword)
    : ICommand<RouterDto>;
/// <summary>ApiPassword null/empty keeps the existing password.</summary>
public record UpdateRouterCommand(int Id, string Name, string Host, int Port, bool UseSsl, string ApiUsername, string? ApiPassword)
    : ICommand<RouterDto>;
public record DeleteRouterCommand(int Id) : ICommand;
public record TestRouterConnectionCommand(int Id) : ICommand<TestConnectionResult>;
public record TestConnectionResult(bool Success, string Message);
public record GetRoutersQuery : IQuery<IReadOnlyList<RouterDto>>;
public record GetRouterByIdQuery(int Id) : IQuery<RouterDto>;

public class CreateRouterValidator : AbstractValidator<CreateRouterCommand>
{
    public CreateRouterValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Host).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Port).InclusiveBetween(1, 65535);
        RuleFor(x => x.ApiUsername).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ApiPassword).NotNull().MaximumLength(255);
    }
}

public class UpdateRouterValidator : AbstractValidator<UpdateRouterCommand>
{
    public UpdateRouterValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Host).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Port).InclusiveBetween(1, 65535);
        RuleFor(x => x.ApiUsername).NotEmpty().MaximumLength(100);
    }
}

internal static class RouterQueries
{
    public static async Task<MikrotikRouter> GetOwnedAsync(IAppDbContext db, int userId, int id, CancellationToken ct) =>
        await db.Routers.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct)
        ?? throw new NotFoundException($"Router {id} not found.");

    public static RouterDto ToDto(MikrotikRouter r, int count) =>
        new(r.Id, r.Name, r.Host, r.Port, r.UseSsl, r.ApiUsername, r.IsOnline, r.LastSeenAt, count);
}

public class CreateRouterHandler(IAppDbContext db, ICurrentUser user, ISecretProtector protector)
    : IRequestHandler<CreateRouterCommand, RouterDto>
{
    public async Task<RouterDto> Handle(CreateRouterCommand c, CancellationToken ct)
    {
        var router = new MikrotikRouter
        {
            UserId = user.UserId,
            Name = c.Name.Trim(),
            Host = c.Host.Trim(),
            Port = c.Port,
            UseSsl = c.UseSsl,
            ApiUsername = c.ApiUsername.Trim(),
            ApiPasswordEncrypted = protector.Protect(c.ApiPassword)
        };
        db.Routers.Add(router);
        await db.SaveChangesAsync(ct);
        return RouterQueries.ToDto(router, 0);
    }
}

public class UpdateRouterHandler(IAppDbContext db, ICurrentUser user, ISecretProtector protector)
    : IRequestHandler<UpdateRouterCommand, RouterDto>
{
    public async Task<RouterDto> Handle(UpdateRouterCommand c, CancellationToken ct)
    {
        var router = await RouterQueries.GetOwnedAsync(db, user.UserId, c.Id, ct);
        router.Name = c.Name.Trim();
        router.Host = c.Host.Trim();
        router.Port = c.Port;
        router.UseSsl = c.UseSsl;
        router.ApiUsername = c.ApiUsername.Trim();
        if (!string.IsNullOrEmpty(c.ApiPassword)) router.ApiPasswordEncrypted = protector.Protect(c.ApiPassword);
        await db.SaveChangesAsync(ct);
        var count = await db.PppoeUsers.CountAsync(p => p.RouterId == router.Id, ct);
        return RouterQueries.ToDto(router, count);
    }
}

public class DeleteRouterHandler(IAppDbContext db, ICurrentUser user) : IRequestHandler<DeleteRouterCommand>
{
    public async Task Handle(DeleteRouterCommand c, CancellationToken ct)
    {
        var router = await RouterQueries.GetOwnedAsync(db, user.UserId, c.Id, ct);
        db.Routers.Remove(router);
        await db.SaveChangesAsync(ct);
    }
}

public class TestRouterConnectionHandler(IAppDbContext db, ICurrentUser user, ISecretProtector protector, IRouterOsClient client)
    : IRequestHandler<TestRouterConnectionCommand, TestConnectionResult>
{
    public async Task<TestConnectionResult> Handle(TestRouterConnectionCommand c, CancellationToken ct)
    {
        var router = await RouterQueries.GetOwnedAsync(db, user.UserId, c.Id, ct);
        try
        {
            await using var session = await client.ConnectAsync(
                new RouterConnection(router.Host, router.Port, router.UseSsl, router.ApiUsername,
                    protector.Unprotect(router.ApiPasswordEncrypted)), ct);
            router.IsOnline = true;
            router.LastSeenAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new TestConnectionResult(true, "Connected and authenticated.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            router.IsOnline = false;
            await db.SaveChangesAsync(ct);
            return new TestConnectionResult(false, ex.Message);
        }
    }
}

public class GetRoutersHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetRoutersQuery, IReadOnlyList<RouterDto>>
{
    public async Task<IReadOnlyList<RouterDto>> Handle(GetRoutersQuery q, CancellationToken ct) =>
        await db.Routers.AsNoTracking().Where(r => r.UserId == user.UserId).OrderBy(r => r.Name)
            .Select(r => new RouterDto(r.Id, r.Name, r.Host, r.Port, r.UseSsl, r.ApiUsername, r.IsOnline, r.LastSeenAt,
                r.PppoeUsers.Count))
            .ToListAsync(ct);
}

public class GetRouterByIdHandler(IAppDbContext db, ICurrentUser user) : IRequestHandler<GetRouterByIdQuery, RouterDto>
{
    public async Task<RouterDto> Handle(GetRouterByIdQuery q, CancellationToken ct) =>
        await db.Routers.AsNoTracking().Where(r => r.Id == q.Id && r.UserId == user.UserId)
            .Select(r => new RouterDto(r.Id, r.Name, r.Host, r.Port, r.UseSsl, r.ApiUsername, r.IsOnline, r.LastSeenAt,
                r.PppoeUsers.Count))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException($"Router {q.Id} not found.");
}
