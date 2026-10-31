using System.Security.Claims;
using MikrotikScheduler.Application.Common;

namespace MikrotikScheduler.Api.Services;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public int UserId =>
        int.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedAppException("Not authenticated.");
}
