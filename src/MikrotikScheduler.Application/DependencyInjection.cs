using AspNetCore.Dispatcher;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MikrotikScheduler.Application.Common;

namespace MikrotikScheduler.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var asm = typeof(DependencyInjection).Assembly;
        services.AddDispatcher(cfg =>
        {
            cfg.RegisterServicesFromAssembly(asm);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(asm);
        return services;
    }
}
