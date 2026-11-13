using EidCharity.Application;
using EidCharity.Application.Common;
using EidCharity.Domain;
using EidCharity.Infrastructure;
using EidCharity.Infrastructure.Data;
using EidCharity.Web.Components;
using EidCharity.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddEidInfrastructure(builder.Configuration, backgroundJobs: builder.Configuration.GetValue("Jobs:Enabled", true));
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
builder.Services.AddScoped<Ui>();
builder.Services.AddScoped<DonationCart>();
builder.Services.AddScoped<EidCharity.Web.Components.Shared.Toasts>();

// Identity: cookie sign-in, lockout, strong passwords. Permission claims are re-validated every minute.
builder.Services.AddIdentity<AppUser, IdentityRole>(o =>
    {
        o.Password.RequiredLength = 8;
        o.Password.RequireNonAlphanumeric = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.User.RequireUniqueEmail = false;
    })
    .AddEntityFrameworkStores<EidDbContext>()
    .AddClaimsPrincipalFactory<EidClaimsFactory>()
    .AddDefaultTokenProviders();
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/account/login";
    o.AccessDeniedPath = "/account/denied";
    o.Cookie.Name = "eid.auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.ExpireTimeSpan = TimeSpan.FromHours(10);
    o.SlidingExpiration = true;
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddAuthorization();

// Behind Cloudflare Tunnel: trust forwarded proto/host so cookies & redirects use https.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear(); o.KnownProxies.Clear();
});
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath)).SetApplicationName("EidCharity");
builder.Services.AddHealthChecks().AddDbContextCheck<EidDbContext>("database");
builder.Services.AddAntiforgery();

var app = builder.Build();

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapHealthChecks("/health");

// ----- Account endpoints (static SSR form posts) -----
app.MapPost("/account/signin", async ([FromForm] string username, [FromForm] string password, [FromForm] string? returnUrl,
    SignInManager<AppUser> signIn, UserManager<AppUser> users, EidDbContext db, HttpContext ctx) =>
{
    var safeReturn = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") ? returnUrl : "/";
    var user = await users.FindByNameAsync(username.Trim());
    if (user is null || !user.IsActive) return Results.LocalRedirect($"/account/login?error=1&returnUrl={Uri.EscapeDataString(safeReturn)}");
    var result = await signIn.PasswordSignInAsync(user, password, isPersistent: false, lockoutOnFailure: true);
    db.AuditLogs.Add(new AuditLog { At = DateTime.UtcNow, UserId = user.Id, UserName = user.UserName, Module = "Security",
        Action = result.Succeeded ? "SignIn" : result.IsLockedOut ? "SignInLockedOut" : "SignInFailed", EntityType = "AppUser", EntityId = user.Id,
        IpAddress = ctx.Connection.RemoteIpAddress?.ToString() });
    if (result.Succeeded) { user.LastLoginAt = DateTime.UtcNow; await users.UpdateAsync(user); }
    await db.SaveChangesAsync();
    if (result.IsLockedOut) return Results.LocalRedirect("/account/login?error=locked");
    if (!result.Succeeded) return Results.LocalRedirect($"/account/login?error=1&returnUrl={Uri.EscapeDataString(safeReturn)}");
    ctx.Response.Cookies.Append("eid.lang", user.PreferredLanguage, new CookieOptions { MaxAge = TimeSpan.FromDays(365), SameSite = SameSiteMode.Lax });
    return Results.LocalRedirect(safeReturn);
}).DisableAntiforgery().AllowAnonymous();

app.MapPost("/account/logout", async (SignInManager<AppUser> signIn) =>
{
    await signIn.SignOutAsync();
    return Results.LocalRedirect("/account/login");
}).RequireAuthorization();

// Persist language preference for the user (the UI switches instantly without reload).
app.MapPost("/api/lang/{lang}", async (string lang, HttpContext ctx, UserManager<AppUser> users) =>
{
    lang = lang == "en" ? "en" : "ar";
    ctx.Response.Cookies.Append("eid.lang", lang, new CookieOptions { MaxAge = TimeSpan.FromDays(365), SameSite = SameSiteMode.Lax });
    var u = await users.GetUserAsync(ctx.User);
    if (u is not null && u.PreferredLanguage != lang) { u.PreferredLanguage = lang; await users.UpdateAsync(u); }
    return Results.NoContent();
}).RequireAuthorization().DisableAntiforgery();

// Attachment download with permission check and audit (sensitive downloads are logged).
app.MapGet("/files/{id:int}", async (int id, EidDbContext db, IFileStore files, ICurrentUser user, AuditService audit) =>
{
    var a = await db.Attachments.FindAsync(id);
    if (a is null) return Results.NotFound();
    if (a.IsSensitive && !user.Has(Perm.BeneficiariesViewSensitive) && !user.Has(Perm.AuditView)) return Results.Forbid();
    var stream = await files.OpenAsync(a.StoragePath);
    if (stream is null) return Results.NotFound();
    audit.Log("Files", "Download", nameof(Attachment), a.Id, reference: a.FileName);
    await db.SaveChangesAsync();
    return Results.File(stream, a.ContentType, a.FileName);
}).RequireAuthorization();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

if (app.Configuration.GetValue("Database:MigrateAndSeed", true))
    await Seeder.SeedAsync(app.Services, app.Configuration, app.Logger);

app.Run();

public partial class Program;
