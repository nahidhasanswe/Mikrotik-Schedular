using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MikrotikScheduler.Application.Common;

namespace MikrotikScheduler.Api.Services;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            ValidationException => (400, "Validation failed"),
            NotFoundException => (404, "Not found"),
            ConflictException => (409, "Conflict"),
            UnauthorizedAppException => (401, "Unauthorized"),
            RouterConnectionException => (502, "Router error"),
            _ => (500, "Server error")
        };
        if (status == 500) logger.LogError(ex, "Unhandled exception");

        ctx.Response.StatusCode = status;
        if (ex is ValidationException ve)
        {
            var errors = ve.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            await ctx.Response.WriteAsJsonAsync(new ValidationProblemDetails(errors) { Status = status, Title = title }, ct);
        }
        else
        {
            await ctx.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == 500 ? "An unexpected error occurred." : ex.Message
            }, ct);
        }
        return true;
    }
}
