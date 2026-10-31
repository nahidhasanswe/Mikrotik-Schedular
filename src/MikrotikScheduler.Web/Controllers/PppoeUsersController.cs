using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MikrotikScheduler.Web.Models;
using MikrotikScheduler.Web.Services;

namespace MikrotikScheduler.Web.Controllers;

[Authorize]
public class PppoeUsersController(ApiClient api) : Controller
{
    public async Task<IActionResult> Index(int? routerId, bool? isActive, string? search, int page = 1)
    {
        var qs = $"api/pppoe-users?page={page}&pageSize=20";
        if (routerId != null) qs += $"&routerId={routerId}";
        if (isActive != null) qs += $"&isActive={isActive.ToString()!.ToLowerInvariant()}";
        if (!string.IsNullOrWhiteSpace(search)) qs += $"&search={Uri.EscapeDataString(search)}";
        return View(new PppoeListViewModel
        {
            Result = await api.GetAsync<PagedResult<PppoeUserDto>>(qs),
            Routers = await api.GetAsync<List<RouterDto>>("api/routers"),
            RouterId = routerId, IsActive = isActive, Search = search
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create() =>
        View("Form", new PppoeFormModel { Routers = await api.GetAsync<List<RouterDto>>("api/routers") });

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var u = await api.GetAsync<PppoeUserDto>($"api/pppoe-users/{id}");
        return View("Form", new PppoeFormModel
        {
            Id = u.Id, RouterId = u.RouterId, Username = u.Username, IsActive = u.IsActive, ExpiresAt = u.ExpiresAt,
            Routers = await api.GetAsync<List<RouterDto>>("api/routers")
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PppoeFormModel m)
    {
        if (m.Id == 0 && m.RouterId == 0) ModelState.AddModelError(nameof(m.RouterId), "Select a router.");
        if (ModelState.IsValid)
        {
            try
            {
                if (m.Id == 0) await api.PostAsync<PppoeUserDto>("api/pppoe-users", new { m.RouterId, m.Username, m.IsActive, m.ExpiresAt });
                else await api.PutAsync<PppoeUserDto>($"api/pppoe-users/{m.Id}", new { m.Username, m.IsActive, m.ExpiresAt });
                TempData["Success"] = "PPPoE user saved. The background service enables its existing secret on the router while the user is active.";
                return RedirectToAction(nameof(Index));
            }
            catch (ApiException ex) { ModelState.AddModelError("", ex.Message); }
        }
        m.Routers = await api.GetAsync<List<RouterDto>>("api/routers");
        return View("Form", m);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int id, bool isActive, string? returnUrl)
    {
        try { await api.PatchAsync<PppoeUserDto>($"api/pppoe-users/{id}/active", new { isActive }); }
        catch (ApiException ex) { TempData["Error"] = ex.Message; }
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try { await api.DeleteAsync($"api/pppoe-users/{id}"); TempData["Success"] = "PPPoE user deleted."; }
        catch (ApiException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}
