using AspNetCore.Dispatcher;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MikrotikScheduler.Application.Common;
using MikrotikScheduler.Domain.Entities;
using MikrotikScheduler.Domain.Enums;

namespace MikrotikScheduler.Application.Features.Sync;

public record SyncResult(int RouterId, bool Online, int Enabled, int AlreadyEnabled, int Failed, string? Error);

/// <summary>Used by the background worker (no user check).</summary>
public record SyncRouterCommand(int RouterId) : ICommand<SyncResult>;

/// <summary>Used by the UI "Sync now" button (ownership checked).</summary>
public record SyncRouterNowCommand(int RouterId) : ICommand<SyncResult>;

public record SyncLogDto(long Id, int RouterId, int? PppoeUserId, string Action, bool Success, string? Message, DateTime CreatedAt);
public record GetSyncLogsQuery(int? RouterId, int Take = 100) : IQuery<IReadOnlyList<SyncLogDto>>;

/// <summary>
/// Rule: PPPoE users that are currently active in our system (IsActive and not past ExpiresAt) get their
/// existing /ppp/secret enabled on the Mikrotik. Nothing is imported, created, disabled or deleted.
/// </summary>
public class SyncRouterHandler(
    IAppDbContext db,
    ISecretProtector protector,
    IRouterOsClient client,
    ILogger<SyncRouterHandler> logger) : IRequestHandler<SyncRouterCommand, SyncResult>
{
    public async Task<SyncResult> Handle(SyncRouterCommand cmd, CancellationToken ct)
    {
        var router = await db.Routers.FirstOrDefaultAsync(r => r.Id == cmd.RouterId, ct)
                     ?? throw new NotFoundException($"Router {cmd.RouterId} not found.");

        var now = DateTime.UtcNow;
        var pending = await db.PppoeUsers
            .Where(p => p.RouterId == router.Id && p.IsActive && (p.ExpiresAt == null || p.ExpiresAt > now))
            .ToListAsync(ct);

        // Nothing to push: skip connecting to the router entirely.
        if (pending.Count == 0)
            return new SyncResult(router.Id, router.IsOnline, 0, 0, 0, null);

        IRouterOsSession session;
        try
        {
            session = await client.ConnectAsync(new RouterConnection(router.Host, router.Port, router.UseSsl,
                router.ApiUsername, protector.Unprotect(router.ApiPasswordEncrypted)), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Router {Router} unreachable: {Message}", router.Name, ex.Message);
            var wasOnline = router.IsOnline;
            router.IsOnline = false;
            foreach (var p in pending)
            {
                p.SyncStatus = SyncStatus.Failed;
                p.LastError = ex.Message;
            }
            if (wasOnline) AddLog(router.Id, null, "Connect", false, ex.Message);
            await db.SaveChangesAsync(ct);
            return new SyncResult(router.Id, false, 0, 0, pending.Count, ex.Message);
        }

        int enabled = 0, already = 0, failed = 0;
        await using (session)
        {
            router.IsOnline = true;
            router.LastSeenAt = DateTime.UtcNow;

            var secrets = (await session.GetPppSecretsAsync(ct))
                .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var user in pending)
            {
                try
                {
                    if (!secrets.TryGetValue(user.Username, out var secret))
                    {
                        failed++;
                        var msg = $"PPPoE secret '{user.Username}' does not exist on the router.";
                        if (user.LastError != msg) AddLog(router.Id, user.Id, "Enable", false, msg);
                        user.SyncStatus = SyncStatus.Failed;
                        user.LastError = msg;
                    }
                    else if (secret.Disabled)
                    {
                        await session.EnablePppSecretAsync(secret.Id, ct);
                        enabled++;
                        AddLog(router.Id, user.Id, "Enable", true, $"Enabled '{user.Username}'.");
                        MarkSynced(user);
                    }
                    else
                    {
                        already++;
                        if (user.SyncStatus != SyncStatus.Synced || user.LastError != null) MarkSynced(user);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failed++;
                    user.SyncStatus = SyncStatus.Failed;
                    user.LastError = ex.Message;
                    AddLog(router.Id, user.Id, "Enable", false, ex.Message);
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return new SyncResult(router.Id, true, enabled, already, failed, null);
    }

    private static void MarkSynced(PppoeUser u)
    {
        u.SyncStatus = SyncStatus.Synced;
        u.LastSyncedAt = DateTime.UtcNow;
        u.LastError = null;
    }

    private void AddLog(int routerId, int? userId, string action, bool ok, string? message) =>
        db.SyncLogs.Add(new SyncLog { RouterId = routerId, PppoeUserId = userId, Action = action, Success = ok, Message = message });
}

public class SyncRouterNowHandler(IAppDbContext db, ICurrentUser user, ISender sender)
    : IRequestHandler<SyncRouterNowCommand, SyncResult>
{
    public async Task<SyncResult> Handle(SyncRouterNowCommand c, CancellationToken ct)
    {
        if (!await db.Routers.AnyAsync(r => r.Id == c.RouterId && r.UserId == user.UserId, ct))
            throw new NotFoundException($"Router {c.RouterId} not found.");
        return await sender.Send(new SyncRouterCommand(c.RouterId), ct);
    }
}

public class GetSyncLogsHandler(IAppDbContext db, ICurrentUser user)
    : IRequestHandler<GetSyncLogsQuery, IReadOnlyList<SyncLogDto>>
{
    public async Task<IReadOnlyList<SyncLogDto>> Handle(GetSyncLogsQuery q, CancellationToken ct)
    {
        var query = db.SyncLogs.AsNoTracking().Where(l => l.Router.UserId == user.UserId);
        if (q.RouterId is { } rid) query = query.Where(l => l.RouterId == rid);
        return await query.OrderByDescending(l => l.Id).Take(Math.Clamp(q.Take, 1, 500))
            .Select(l => new SyncLogDto(l.Id, l.RouterId, l.PppoeUserId, l.Action, l.Success, l.Message, l.CreatedAt))
            .ToListAsync(ct);
    }
}
