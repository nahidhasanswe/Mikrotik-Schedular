using AspNetCore.Dispatcher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MikrotikScheduler.Application.Common;
using MikrotikScheduler.Application.Features.PppoeUsers;

namespace MikrotikScheduler.Api.Controllers;

[ApiController, Route("api/pppoe-users"), Authorize]
public class PppoeUsersController(ISender sender) : ControllerBase
{
    public record CreateRequest(int RouterId, string Username, bool IsActive, DateTime? ExpiresAt);
    public record UpdateRequest(string Username, bool IsActive, DateTime? ExpiresAt);
    public record ActiveRequest(bool IsActive);
    public record ExpiryRequest(DateTime? ExpiresAt);

    [HttpGet]
    public async Task<PagedResult<PppoeUserDto>> GetAll([FromQuery] int? routerId, [FromQuery] bool? isActive,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await sender.Send(new GetPppoeUsersQuery(routerId, isActive, search, page, pageSize), ct);

    [HttpGet("{id:int}")]
    public async Task<PppoeUserDto> Get(int id, CancellationToken ct) => await sender.Send(new GetPppoeUserByIdQuery(id), ct);

    [HttpPost]
    public async Task<ActionResult<PppoeUserDto>> Create(CreateRequest r, CancellationToken ct)
    {
        var dto = await sender.Send(new CreatePppoeUserCommand(r.RouterId, r.Username, r.IsActive, r.ExpiresAt), ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:int}")]
    public async Task<PppoeUserDto> Update(int id, UpdateRequest r, CancellationToken ct) =>
        await sender.Send(new UpdatePppoeUserCommand(id, r.Username, r.IsActive, r.ExpiresAt), ct);

    [HttpPatch("{id:int}/active")]
    public async Task<PppoeUserDto> SetActive(int id, ActiveRequest r, CancellationToken ct) =>
        await sender.Send(new SetPppoeUserActiveCommand(id, r.IsActive), ct);

    [HttpPatch("{id:int}/expiry")]
    public async Task<PppoeUserDto> SetExpiry(int id, ExpiryRequest r, CancellationToken ct) =>
        await sender.Send(new SetPppoeUserExpiryCommand(id, r.ExpiresAt), ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await sender.Send(new DeletePppoeUserCommand(id), ct);
        return NoContent();
    }
}
