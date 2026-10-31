using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MikrotikScheduler.Application.Common;
using MikrotikScheduler.Infrastructure.Persistence;
using MikrotikScheduler.Infrastructure.RouterOs;
using MikrotikScheduler.Infrastructure.Services;
using MikrotikScheduler.Infrastructure.Workers;

namespace MikrotikScheduler.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Default")
                 ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(cs, sql => sql.EnableRetryOnFailure(3)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        var dp = services.AddDataProtection().SetApplicationName("MikrotikScheduler");
        var keysPath = config["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            Directory.CreateDirectory(keysPath);
            dp.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
        services.Configure<SyncOptions>(config.GetSection(SyncOptions.Section));
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<IRouterOsClient, RouterOsClient>();
        services.AddHostedService<SyncWorker>();
        return services;
    }
}
