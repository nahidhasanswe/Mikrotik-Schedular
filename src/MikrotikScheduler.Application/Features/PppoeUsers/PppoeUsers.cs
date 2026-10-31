using AspNetCore.Dispatcher;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MikrotikScheduler.Application.Common;
using MikrotikScheduler.Domain.Entities;
using MikrotikScheduler.Domain.Enums;

namespace MikrotikScheduler.Application.Features.PppoeUsers;

public record PppoeUserDto(int Id, int RouterId, string RouterName, string Username, bool IsActive,
    DateTime? ExpiresAt, bool IsExpired, SyncStatus SyncStatus, DateTime? LastSyncedAt, string? LastError, DateTime CreatedAt);

/// <summary>ExpiresAt is UTC; null = never expires.</summary>
public record CreatePppoeUserCommand(int RouterId, string Username, bool IsActive, DateTime? ExpiresAt)
    : ICommand<PppoeUserDto>;
public record UpdatePppoeUserCommand(int Id, string Username, bool IsActive, DateTime? ExpiresAt)
    : ICommand<PppoeUserDto>;
public record SetPppoeUserActiveCommand(int Id, bool IsActive) : ICommand<PppoeUserDto>;
public record SetPppoeUserExpiryCommand(int Id, DateTime? ExpiresAt) : ICommand<PppoeUserDto>;
public record DeletePppoeUserCommand(int Id) : ICommand;
public record GetPppoeUsersQuery(int? RouterId, bool? IsActive, string? Search, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<PppoeUserDto>>;
public record GetPppoeUserByIdQuery(int Id) : IQuery<PppoeUserDto>;

public class CreatePppoeUserValidator : AbstractValidator<CreatePppoeUserCommand>
{
    public CreatePppoeUserValidator()
    {
        RuleFor(x => x.RouterId).GreaterThan(0);
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100);
    }
}

public class UpdatePppoeUserValidator : AbstractValidator<UpdatePppoeUserCommand>
{
    public UpdatePppoeUserValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100);
    }
}

internal static class PppoeQueries
{
    public static async Task<PppoeUser> GetOwnedAsync(IAppDbContext db, int userId, int id, CancellationToken ct) =>
        await db.PppoeUsers.Include(p => p.Router)
            .FirstOrDefaultAsync(p => p.Id == id && p.Router.UserId == userId, ct)
        ?? throw new NotFoundException($"PPPoE user {id} not found.");

    public static DateTime? Utc(DateTime? d) => d == null ? null : d.Value.Kind == DateTimeKind.Local ? d.Value.ToUniversalTime() : DateTime.SpecifyKind(d.Value, DateTimeKind.Utc);

    public static PppoeUserDto ToDto(PppoeUser p) =>
        new(p.Id, p.RouterId, p.Router.Name, p.Username, p.IsActive, p.ExpiresAt, p.IsExpired(DateTime.UtcNow), p.SyncStatus, p.LastSyncedAt, p.LastError, p.CreatedAt);
}

public class CreatePppoeUserHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<CreatePppoeUserCommand, PppoeUserDto>
{
    public async Task<PppoeUserDto> Handle(CreatePppoeUserCommand c, CancellationToken ct)
    {
        var router = await db.Routers.FirstOrDefaultAsync(r => r.Id == c.RouterId && r.UserId == user.UserId, ct)
                     ?? throw new NotFoundException($"Router {c.RouterId} not found.");
        var username = c.Username.Trim();
        if (await db.PppoeUsers.AnyAsync(p => p.RouterId == router.Id && p.Username == username, ct))
            throw new ConflictException($"PPPoE user '{username}' already exists on this router.");

        var entity = new PppoeUser
        {
            RouterId = router.Id,
            Router = router,
            Username = username,
            IsActive = c.IsActive,
            ExpiresAt = PppoeQueries.Utc(c.ExpiresAt),
            SyncStatus = SyncStatus.Pending
        };
        db.PppoeUsers.Add(entity);
        await db.SaveChangesAsync(ct);
        return PppoeQueries.ToDto(entity);
    }
}

public class UpdatePppoeUserHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<UpdatePppoeUserCommand, PppoeUserDto>
{
    public async Task<PppoeUserDto> Handle(UpdatePppoeUserCommand c, CancellationToken ct)
    {
        var entity = await PppoeQueries.GetOwnedAsync(db, user.UserId, c.Id, ct);
        var username = c.Username.Trim();
        if (await db.PppoeUsers.AnyAsync(p => p.RouterId == entity.RouterId && p.Username == username && p.Id != entity.Id, ct))
            throw new ConflictException($"PPPoE user '{username}' already exists on this router.");

        entity.Username = username;
        entity.IsActive = c.IsActive;
        entity.ExpiresAt = PppoeQueries.Utc(c.ExpiresAt);
        entity.SyncStatus = SyncStatus.Pending;
        await db.SaveChangesAsync(ct);
        return PppoeQueries.ToDto(entity);
    }
}

public class SetPppoeUserActiveHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<SetPppoeUserActiveCommand, PppoeUserDto>
{
    public async Task<PppoeUserDto> Handle(SetPppoeUserActiveCommand c, CancellationToken ct)
    {
        var entity = await PppoeQueries.GetOwnedAsync(db, user.UserId, c.Id, ct);
        entity.IsActive = c.IsActive;
        entity.SyncStatus = SyncStatus.Pending;
        await db.SaveChangesAsync(ct);
        return PppoeQueries.ToDto(entity);
    }
}

public class SetPppoeUserExpiryHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<SetPppoeUserExpiryCommand, PppoeUserDto>
{
    public async Task<PppoeUserDto> Handle(SetPppoeUserExpiryCommand c, CancellationToken ct)
    {
        var entity = await PppoeQueries.GetOwnedAsync(db, user.UserId, c.Id, ct);
        entity.ExpiresAt = PppoeQueries.Utc(c.ExpiresAt);
        entity.SyncStatus = SyncStatus.Pending;
        await db.SaveChangesAsync(ct);
        return PppoeQueries.ToDto(entity);
    }
}

public class DeletePppoeUserHandler(IAppDbContext db, ICurrentUser user) : IRequestHandler<DeletePppoeUserCommand>
{
    public async Task Handle(DeletePppoeUserCommand c, CancellationToken ct)
    {
        var entity = await PppoeQueries.GetOwnedAsync(db, user.UserId, c.Id, ct);
        db.PppoeUsers.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}

public class GetPppoeUsersHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetPppoeUsersQuery, PagedResult<PppoeUserDto>>
{
    public async Task<PagedResult<PppoeUserDto>> Handle(GetPppoeUsersQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var now = DateTime.UtcNow;
        var query = db.PppoeUsers.AsNoTracking().Where(p => p.Router.UserId == user.UserId);
        if (q.RouterId is { } rid) query = query.Where(p => p.RouterId == rid);
        if (q.IsActive is { } active) query = query.Where(p => p.IsActive == active);
        if (!string.IsNullOrWhiteSpace(q.Search)) query = query.Where(p => p.Username.Contains(q.Search));

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.Router.Name).ThenBy(p => p.Username)
            .Skip((page - 1) * size).Take(size)
            .Select(p => new PppoeUserDto(p.Id, p.RouterId, p.Router.Name, p.Username, p.IsActive, p.ExpiresAt,
                p.ExpiresAt != null && p.ExpiresAt <= now, p.SyncStatus,
                p.LastSyncedAt, p.LastError, p.CreatedAt))
            .ToListAsync(ct);
        return new PagedResult<PppoeUserDto>(items, total, page, size);
    }
}

public class GetPppoeUserByIdHandler(IAppDbContext db, ICurrentUser user) : IRequestHandler<GetPppoeUserByIdQuery, PppoeUserDto>
{
    public async Task<PppoeUserDto> Handle(GetPppoeUserByIdQuery q, CancellationToken ct) =>
        PppoeQueries.ToDto(await PppoeQueries.GetOwnedAsync(db, user.UserId, q.Id, ct));
}
