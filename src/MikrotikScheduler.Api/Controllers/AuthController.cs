using AspNetCore.Dispatcher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MikrotikScheduler.Application.Features.Auth;

namespace MikrotikScheduler.Api.Controllers;

[ApiController, Route("api/auth"), AllowAnonymous]
public class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResult>> Register(RegisterCommand cmd, CancellationToken ct) =>
        Ok(await sender.Send(cmd, ct));

    [HttpPost("login")]
    public async Task<ActionResult<AuthResult>> Login(LoginCommand cmd, CancellationToken ct) =>
        Ok(await sender.Send(cmd, ct));

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResult>> Refresh(RefreshTokenCommand cmd, CancellationToken ct) =>
        Ok(await sender.Send(cmd, ct));
}
