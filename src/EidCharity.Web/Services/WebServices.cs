using System.Globalization;
using System.Security.Claims;
using EidCharity.Application;
using EidCharity.Application.Collections;
using EidCharity.Domain;
using EidCharity.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Options;

namespace EidCharity.Web.Services;

/// <summary>Resolves the signed-in user from the HTTP request (SSR) or the circuit's authentication state (interactive).</summary>
public class CurrentUser(IHttpContextAccessor http, IServiceProvider sp) : ICurrentUser
{
    private ClaimsPrincipal? _principal;
    private HashSet<string>? _perms;

    private ClaimsPrincipal Principal
    {
        get
        {
            if (_principal is not null) return _principal;
            var p = http.HttpContext?.User;
            if (p?.Identity?.IsAuthenticated != true)
            {
                try
                {
                    var asp = sp.GetService<AuthenticationStateProvider>();
                    if (asp is not null) p = asp.GetAuthenticationStateAsync().GetAwaiter().GetResult().User;
                }
                catch { /* background scope: no circuit */ }
            }
            _principal = p ?? new ClaimsPrincipal(new ClaimsIdentity());
            return _principal;
        }
    }

    public void Reset() { _principal = null; _perms = null; }
    /// <summary>Pins the principal (used when a page creates its own service scope inside the circuit).</summary>
    public void Use(ClaimsPrincipal principal) { _principal = principal; _perms = null; }
    public ClaimsPrincipal User => Principal;
    public string? UserId => Principal.FindFirstValue(ClaimTypes.NameIdentifier);
    public string UserName => Principal.Identity?.Name ?? "anonymous";
    public string DisplayName => Principal.FindFirstValue(EidClaims.NameAr) ?? UserName;
    public string DisplayNameEn => Principal.FindFirstValue(EidClaims.NameEn) ?? UserName;
    public int? CenterId => int.TryParse(Principal.FindFirstValue(EidClaims.CenterId), out var c) ? c : null;
    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;
    public IReadOnlySet<string> Permissions => _perms ??= Principal.FindAll(EidClaims.Permission).Select(c => c.Value).ToHashSet();
    public bool Has(string permission) => Permissions.Contains(permission);
}

/// <summary>Any policy name that is a permission key becomes a "has perm claim" policy.</summary>
public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) =>
        await base.GetPolicyAsync(policyName)
        ?? (policyName.Contains('.')
            ? new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireClaim(EidClaims.Permission, policyName.Split(',', StringSplitOptions.TrimEntries)).Build()
            : null);
}

/// <summary>Circuit-scoped UI state: language & direction. Switching never reloads the page, so drafts are kept.</summary>
public class Ui
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    public string Lang { get; private set; } = "ar";
    public bool IsAr => Lang == "ar";
    public string Dir => IsAr ? "rtl" : "ltr";
    public event Action? Changed;

    public void Set(string lang)
    {
        lang = lang == "en" ? "en" : "ar";
        if (lang == Lang) return;
        Lang = lang;
        Changed?.Invoke();
    }

    public string T(string ar, string en) => IsAr ? ar : en;
    public string Name(IBilingual b) => IsAr ? b.NameAr : b.NameEn;
    public string N(string ar, string? en) => IsAr || string.IsNullOrWhiteSpace(en) ? ar : en!;
    public string Cur => T("ر.ق", "QAR");
    /// <summary>Numbers and references stay identical in both languages (western digits).</summary>
    public string Money(decimal v, bool decimals = false) => v.ToString(decimals ? "#,##0.00" : "#,##0.##", En);
    public string Date(DateTime? utc) => utc is null ? "—" : QatarTime.ToLocal(utc.Value).ToString("yyyy-MM-dd", En);
    public string DateTimeStr(DateTime? utc) => utc is null ? "—" : QatarTime.ToLocal(utc.Value).ToString("yyyy-MM-dd HH:mm", En);
    public string Date(DateOnly? d) => d?.ToString("yyyy-MM-dd", En) ?? "—";
    public string Ago(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span.TotalMinutes < 1) return T("الآن", "just now");
        if (span.TotalMinutes < 60) return T($"منذ {(int)span.TotalMinutes} دقيقة", $"{(int)span.TotalMinutes} min ago");
        if (span.TotalHours < 24) return T($"منذ {(int)span.TotalHours} ساعة", $"{(int)span.TotalHours} h ago");
        return Date(utc);
    }
}

/// <summary>The collector's donation draft for this circuit, mirrored to browser storage so a reload does not lose it.</summary>
public class DonationCart(ProtectedLocalStorage storage)
{
    public DonationDraft Draft { get; private set; } = new();
    public event Action? Changed;
    private bool _loaded;

    public async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            var r = await storage.GetAsync<DonationDraft>("eid-draft");
            if (r.Success && r.Value is not null) Draft = r.Value;
        }
        catch { /* storage unavailable during prerender */ }
        Changed?.Invoke();
    }

    public async Task SaveAsync()
    {
        Changed?.Invoke();
        try { await storage.SetAsync("eid-draft", Draft); } catch { }
    }

    /// <summary>A quick amount replaces the line's amount; it never adds to it.</summary>
    public async Task SetLineAsync(DraftLine line)
    {
        var existing = Draft.Lines.FirstOrDefault(l => l.ProjectId == line.ProjectId);
        if (existing is null) Draft.Lines.Add(line); else existing.Amount = line.Amount;
        await SaveAsync();
    }

    public async Task RemoveAsync(int projectId) { Draft.Lines.RemoveAll(l => l.ProjectId == projectId); await SaveAsync(); }

    public async Task ClearAsync()
    {
        Draft = new DonationDraft();
        await SaveAsync();
    }
}

public record Tone(string Css, string Ar, string En);
