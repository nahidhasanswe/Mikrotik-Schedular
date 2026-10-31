using AspNetCore.Dispatcher;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MikrotikScheduler.Application.Common;
using MikrotikScheduler.Application.Features.Sync;

namespace MikrotikScheduler.Infrastructure.Workers;

public class SyncOptions
{
    public const string Section = "Sync";
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 30;
}

/// <summary>
/// Periodically enables, on the Mikrotik, every PPPoE user that is currently active (IsActive and not expired).
/// </summary>
public class SyncWorker(IServiceScopeFactory scopeFactory, IOptions<SyncOptions> options, ILogger<SyncWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        if (!opts.Enabled)
        {
            logger.LogInformation("Sync worker disabled.");
            return;
        }

        logger.LogInformation("Sync worker started, interval {Seconds}s.", opts.IntervalSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, opts.IntervalSeconds)));
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Sync cycle failed.");
            }
        } while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        List<int> routerIds;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var now = DateTime.UtcNow;
            routerIds = await db.Routers.AsNoTracking()
                .Where(r => r.PppoeUsers.Any(p => p.IsActive && (p.ExpiresAt == null || p.ExpiresAt > now)))
                .Select(r => r.Id)
                .ToListAsync(ct);
        }

        foreach (var id in routerIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope(); // fresh DbContext per router
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var result = await sender.Send(new SyncRouterCommand(id), ct);
                if (result.Enabled + result.Failed > 0)
                    logger.LogInformation("Router {Id}: enabled {E}, failed {F}", id, result.Enabled, result.Failed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Sync of router {Id} failed.", id);
            }
        }
    }
}
