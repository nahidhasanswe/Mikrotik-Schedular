using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MikrotikScheduler.Web.Models;
using MikrotikScheduler.Web.Services;

namespace MikrotikScheduler.Web.Controllers;

[Authorize]
public class SyncLogsController(ApiClient api) : Controller
{
    public async Task<IActionResult> Index() => View(await api.GetAsync<List<SyncLogDto>>("api/routers/sync-logs?take=200"));
}
