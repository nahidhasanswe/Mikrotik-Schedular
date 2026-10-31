using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MikrotikScheduler.Web.Models;
using MikrotikScheduler.Web.Services;

namespace MikrotikScheduler.Web.Controllers;

[Authorize]
public class RoutersController(ApiClient api) : Controller
{
    public async Task<IActionResult> Index() => View(await api.GetAsync<List<RouterDto>>("api/routers"));

    [HttpGet] public IActionResult Create() => View("Form", new RouterFormModel());

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var r = await api.GetAsync<RouterDto>($"api/routers/{id}");
        return View("Form", new RouterFormModel { Id = r.Id, Name = r.Name, Host = r.Host, Port = r.Port, UseSsl = r.UseSsl, ApiUsername = r.ApiUsername });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(RouterFormModel m)
    {
        if (m.Id == 0 && string.IsNullOrEmpty(m.ApiPassword)) ModelState.AddModelError(nameof(m.ApiPassword), "Password is required.");
        if (!ModelState.IsValid) return View("Form", m);
        var body = new { m.Name, m.Host, m.Port, m.UseSsl, m.ApiUsername, m.ApiPassword };
        try
        {
            if (m.Id == 0) await api.PostAsync<RouterDto>("api/routers", body);
            else await api.PutAsync<RouterDto>($"api/routers/{m.Id}", body);
            TempData["Success"] = "Router saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException ex) { ModelState.AddModelError("", ex.Message); return View("Form", m); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await Run(() => api.DeleteAsync($"api/routers/{id}"), "Router deleted.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(int id)
    {
        try
        {
            var r = await api.PostAsync<TestConnectionResult>($"api/routers/{id}/test", null);
            TempData[r!.Success ? "Success" : "Error"] = r.Success ? "Connection OK." : $"Connection failed: {r.Message}";
        }
        catch (ApiException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync(int id)
    {
        try
        {
            var r = await api.PostAsync<SyncResult>($"api/routers/{id}/sync", null);
            if (r!.Online) TempData["Success"] = $"Sync done: {r.Enabled} enabled, {r.AlreadyEnabled} already enabled, {r.Failed} failed (see Sync Logs).";
            else TempData["Error"] = $"Router offline: {r.Error}";
        }
        catch (ApiException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    private async Task Run(Func<Task> action, string ok)
    {
        try { await action(); TempData["Success"] = ok; }
        catch (ApiException ex) { TempData["Error"] = ex.Message; }
    }
}
