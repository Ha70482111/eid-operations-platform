using System.Security.Claims;
using EidCharity.Application;
using EidCharity.Domain;
using EidCharity.Infrastructure.Data;
using EidCharity.Infrastructure.Jobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EidCharity.Infrastructure;

public static class EidClaims
{
    public const string Permission = "perm";
    public const string CenterId = "center";
    public const string NameAr = "name_ar";
    public const string NameEn = "name_en";
    public const string Language = "lang";
}

/// <summary>Adds permission, center and display-name claims at sign-in (re-issued when the security stamp changes).</summary>
public class EidClaimsFactory(UserManager<AppUser> users, RoleManager<IdentityRole> roles, IOptions<IdentityOptions> options, EidDbContext db)
    : UserClaimsPrincipalFactory<AppUser, IdentityRole>(users, roles, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var id = await base.GenerateClaimsAsync(user);
        var roleNames = await UserManager.GetRolesAsync(user);
        var roleIds = await db.Roles.Where(r => roleNames.Contains(r.Name!)).Select(r => r.Id).ToListAsync();
        var perms = await db.RolePermissions.Where(p => roleIds.Contains(p.RoleId)).Select(p => p.Permission).Distinct().ToListAsync();
        id.AddClaims(perms.Select(p => new Claim(EidClaims.Permission, p)));
        if (user.CenterId is int c) id.AddClaim(new Claim(EidClaims.CenterId, c.ToString()));
        id.AddClaim(new Claim(EidClaims.NameAr, user.FullNameAr));
        id.AddClaim(new Claim(EidClaims.NameEn, user.FullNameEn));
        id.AddClaim(new Claim(EidClaims.Language, user.PreferredLanguage));
        return id;
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddEidInfrastructure(this IServiceCollection s, IConfiguration config, bool backgroundJobs = true)
    {
        var cs = config.GetConnectionString("Eid") ?? throw new InvalidOperationException("Connection string 'Eid' is missing.");
        s.AddScoped<AuditInterceptor>();
        s.AddDbContext<EidDbContext>((sp, o) => o
            .UseSqlServer(cs, sql => { /* No retrying strategy: services use explicit transactions + sp_getapplock; a transient failure rolls back and the UI asks to retry. */ sql.MigrationsHistoryTable("__EFMigrationsHistory", "admin"); })
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));
        s.AddScoped<IEidDb>(sp => sp.GetRequiredService<EidDbContext>());
        s.AddScoped<INumberGenerator, SqlNumberGenerator>();
        s.AddScoped<IResourceLock, SqlResourceLock>();
        s.AddSingleton<IFileStore, LocalFileStore>();
        s.AddEidApplication();
        if (backgroundJobs)
        {
            s.AddHostedService<OutboxDispatcher>();
            s.AddHostedService<AlertsJob>();
        }
        return s;
    }
}
