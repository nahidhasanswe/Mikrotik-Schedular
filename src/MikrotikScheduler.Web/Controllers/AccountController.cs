using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MikrotikScheduler.Web.Models;
using MikrotikScheduler.Web.Services;

namespace MikrotikScheduler.Web.Controllers;

[AllowAnonymous]
public class AccountController(ApiClient api) : Controller
{
    [HttpGet] public IActionResult Login(string? returnUrl = null) { ViewData["ReturnUrl"] = returnUrl; return View(new LoginViewModel()); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel m, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(m);
        try
        {
            var r = await api.PostAsync<AuthResult>("api/auth/login", new { m.Username, m.Password });
            await SignInAsync(r!);
            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "PppoeUsers");
        }
        catch (ApiException ex) { ModelState.AddModelError("", ex.Message); return View(m); }
    }

    [HttpGet] public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel m)
    {
        if (!ModelState.IsValid) return View(m);
        try
        {
            var r = await api.PostAsync<AuthResult>("api/auth/register", new { m.Username, m.Email, m.Password });
            await SignInAsync(r!);
            return RedirectToAction("Index", "Routers");
        }
        catch (ApiException ex) { ModelState.AddModelError("", ex.Message); return View(m); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private async Task SignInAsync(AuthResult r)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, r.Username),
            new(ClaimTypes.Role, r.Role),
            new("access_token", r.AccessToken)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = r.ExpiresAt });
    }
}
