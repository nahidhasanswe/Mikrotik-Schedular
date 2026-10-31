using Microsoft.AspNetCore.Authentication.Cookies;
using MikrotikScheduler.Web.Services;

using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Behind nginx-proxy: trust X-Forwarded-For/Proto so the app sees https and the real client IP.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    o.Filters.Add<ApiExceptionFilter>();
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<ApiClient>(c =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("Api:BaseUrl is not configured.");
    c.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Events.OnValidatePrincipal = ctx =>
        {
            if (ctx.Principal?.FindFirst("access_token") is null) ctx.RejectPrincipal();
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Home/Error");
if (builder.Configuration.GetValue("UseHttpsRedirection", false)) app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
