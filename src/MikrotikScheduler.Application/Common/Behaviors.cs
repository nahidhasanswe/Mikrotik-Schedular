using AspNetCore.Dispatcher;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace MikrotikScheduler.Application.Common;

public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (validators.Any())
        {
            var ctx = new ValidationContext<TRequest>(request);
            var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(ctx, ct)));
            var failures = results.SelectMany(r => r.Errors).Where(f => f != null).ToList();
            if (failures.Count != 0) throw new ValidationException(failures);
        }
        return await next(ct);
    }
}

public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        logger.LogDebug("Handling {Request}", name);
        try
        {
            return await next(ct);
        }
        catch (Exception ex) when (ex is not ValidationException and not NotFoundException and not ConflictException and not UnauthorizedAppException)
        {
            logger.LogError(ex, "Request {Request} failed", name);
            throw;
        }
    }
}
