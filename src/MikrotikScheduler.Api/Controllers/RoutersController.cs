using AspNetCore.Dispatcher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MikrotikScheduler.Application.Features.Routers;
using MikrotikScheduler.Application.Features.Sync;

namespace MikrotikScheduler.Api.Controllers;

[ApiController, Route("api/routers"), Authorize]
public class RoutersController(ISender sender) : ControllerBase
{
    public record RouterRequest(string Name, string Host, int Port, bool UseSsl, string ApiUsername, string? ApiPassword);

    [HttpGet]
    public async Task<IReadOnlyList<RouterDto>> GetAll(CancellationToken ct) => await sender.Send(new GetRoutersQuery(), ct);

    [HttpGet("{id:int}")]
    public async Task<RouterDto> Get(int id, CancellationToken ct) => await sender.Send(new GetRouterByIdQuery(id), ct);

    [HttpPost]
    public async Task<ActionResult<RouterDto>> Create(RouterRequest r, CancellationToken ct)
    {
        var dto = await sender.Send(new CreateRouterCommand(r.Name, r.Host, r.Port, r.UseSsl, r.ApiUsername, r.ApiPassword ?? ""), ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:int}")]
    public async Task<RouterDto> Update(int id, RouterRequest r, CancellationToken ct) =>
        await sender.Send(new UpdateRouterCommand(id, r.Name, r.Host, r.Port, r.UseSsl, r.ApiUsername, r.ApiPassword), ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await sender.Send(new DeleteRouterCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/test")]
    public async Task<TestConnectionResult> Test(int id, CancellationToken ct) =>
        await sender.Send(new TestRouterConnectionCommand(id), ct);

    [HttpPost("{id:int}/sync")]
    public async Task<SyncResult> Sync(int id, CancellationToken ct) => await sender.Send(new SyncRouterNowCommand(id), ct);

    [HttpGet("sync-logs")]
    public async Task<IReadOnlyList<SyncLogDto>> Logs([FromQuery] int? routerId, [FromQuery] int take = 100, CancellationToken ct = default) =>
        await sender.Send(new GetSyncLogsQuery(routerId, take), ct);
}
