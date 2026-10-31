using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MikrotikScheduler.Web.Services;

/// <summary>Turns uncaught API failures into a login redirect (401), 404, or a friendly error page.</summary>
public class ApiExceptionFilter : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext ctx)
    {
        if (ctx.Exception is not ApiException ex) return;
        ctx.ExceptionHandled = true;
        switch (ex.Status)
        {
            case HttpStatusCode.Unauthorized:
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                ctx.Result = new RedirectToActionResult("Login", "Account", null);
                break;
            case HttpStatusCode.NotFound:
                ctx.Result = new NotFoundObjectResult(ex.Message);
                break;
            default:
                ctx.Result = new ContentResult { StatusCode = 502, Content = ex.Message, ContentType = "text/plain" };
                break;
        }
    }
}
