using EidCharity.Application.Common;
using EidCharity.Domain;
using EidCharity.Domain.Integration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EidCharity.Application.Admin;

public record UserRow(string Id, string UserName, string Email, string FullNameAr, string FullNameEn, string? CenterAr, int? CenterId, bool IsActive,
    bool IsLocked, List<string> Roles, DateTime? LastLoginAt, string JobTitle = "");

public class UserInput
{
    public string? Id { get; set; }
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullNameAr { get; set; } = "";
    public string FullNameEn { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public int? CenterId { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> Roles { get; set; } = [];
    public string? NewPassword { get; set; }
}

public record RoleRow(string Id, string Name, string NameAr, string NameEn, string DescriptionAr, bool IsSystem, int Users, HashSet<string> Permissions);

public class AdminService(IEidDb db, ICurrentUser user, UserManager<AppUser> users, RoleManager<IdentityRole> roles, AuditService audit)
{
    // ---------------- Users ----------------
    public async Task<List<UserRow>> UsersAsync(string? search, CancellationToken ct = default)
    {
        user.Require(Perm.AdminUsers);
        var q = db.Users.AsNoTracking().Include(u => u.Center).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(u => u.UserName!.Contains(search) || u.FullNameAr.Contains(search) || u.FullNameEn.Contains(search) || u.Email!.Contains(search));
        var list = await q.OrderBy(u => u.UserName).ToListAsync(ct);
        var roleMap = await db.Database.SqlQueryRaw<UserRoleName>(
            "SELECT ur.UserId, r.Name FROM [identity].[UserRoles] ur JOIN [identity].[Roles] r ON r.Id = ur.RoleId").ToListAsync(ct);
        return list.Select(u => new UserRow(u.Id, u.UserName!, u.Email ?? "", u.FullNameAr, u.FullNameEn, u.Center?.NameAr, u.CenterId, u.IsActive,
            u.LockoutEnd > DateTimeOffset.UtcNow, roleMap.Where(r => r.UserId == u.Id).Select(r => r.Name).ToList(), u.LastLoginAt, u.JobTitle)).ToList();
    }

    public record UserRoleName(string UserId, string Name);

    public async Task<string> SaveUserAsync(UserInput i, CancellationToken ct = default)
    {
        user.Require(Perm.AdminUsers);
        if (string.IsNullOrWhiteSpace(i.UserName) || string.IsNullOrWhiteSpace(i.FullNameAr)) throw new DomainException("اسم المستخدم والاسم مطلوبان.", "Username and full name are required.");
        AppUser u;
        IdentityResult res;
        if (string.IsNullOrEmpty(i.Id))
        {
            if (string.IsNullOrWhiteSpace(i.NewPassword)) throw new DomainException("كلمة المرور الأولية مطلوبة.", "Initial password is required.");
            u = new AppUser { UserName = i.UserName.Trim(), Email = i.Email.Trim(), EmailConfirmed = true, CreatedAt = DateTime.UtcNow };
            Apply(u, i);
            res = await users.CreateAsync(u, i.NewPassword);
        }
        else
        {
            u = await users.FindByIdAsync(i.Id) ?? throw new DomainException("المستخدم غير موجود.", "User not found.");
            if (u.Id == user.UserId && !i.IsActive) throw new DomainException("لا يمكنك تعطيل حسابك.", "You cannot deactivate your own account.");
            u.UserName = i.UserName.Trim(); u.Email = i.Email.Trim();
            Apply(u, i);
            res = await users.UpdateAsync(u);
            if (res.Succeeded && !string.IsNullOrWhiteSpace(i.NewPassword))
            {
                var token = await users.GeneratePasswordResetTokenAsync(u);
                res = await users.ResetPasswordAsync(u, token, i.NewPassword);
            }
        }
        if (!res.Succeeded) throw new DomainException(string.Join(" ", res.Errors.Select(e => e.Description)), string.Join(" ", res.Errors.Select(e => e.Description)));

        var current = await users.GetRolesAsync(u);
        var oldRoles = current.ToList();
        await users.RemoveFromRolesAsync(u, current.Except(i.Roles));
        await users.AddToRolesAsync(u, i.Roles.Except(current));
        await users.UpdateSecurityStampAsync(u); // forces re-evaluation of permissions on next request
        audit.Log("Admin", string.IsNullOrEmpty(i.Id) ? "UserCreated" : "UserUpdated", nameof(AppUser), u.Id,
            new { Roles = oldRoles }, new { u.UserName, i.Roles, i.CenterId, i.IsActive, PasswordChanged = !string.IsNullOrWhiteSpace(i.NewPassword) }, reference: u.UserName);
        await db.SaveChangesAsync(ct);
        return u.Id;
    }

    private static void Apply(AppUser u, UserInput i)
    {
        u.FullNameAr = i.FullNameAr; u.FullNameEn = string.IsNullOrWhiteSpace(i.FullNameEn) ? i.FullNameAr : i.FullNameEn; u.JobTitle = i.JobTitle;
        u.CenterId = i.CenterId; u.IsActive = i.IsActive;
        u.LockoutEnabled = true;
        u.LockoutEnd = i.IsActive ? null : DateTimeOffset.MaxValue;
    }

    public async Task UnlockAsync(string userId, CancellationToken ct = default)
    {
        user.Require(Perm.AdminUsers);
        var u = await users.FindByIdAsync(userId) ?? throw new DomainException("المستخدم غير موجود.", "User not found.");
        await users.SetLockoutEndDateAsync(u, null);
        await users.ResetAccessFailedCountAsync(u);
        audit.Log("Admin", "UserUnlocked", nameof(AppUser), u.Id, reference: u.UserName);
        await db.SaveChangesAsync(ct);
    }

    public record LookupItem(string Id, string Key, string NameAr, string NameEn);

    /// <summary>Role names for pickers (user editor, approval matrix).</summary>
    public async Task<List<LookupItem>> RoleLookupAsync(CancellationToken ct = default)
    {
        user.RequireAny(Perm.AdminUsers, Perm.AdminRoles, Perm.AdminApprovalMatrix, Perm.AdminDelegations);
        var rs = await roles.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
        var profiles = await db.RoleProfiles.AsNoTracking().ToDictionaryAsync(p => p.RoleId, ct);
        return rs.Select(r => new LookupItem(r.Id, r.Name!, profiles.GetValueOrDefault(r.Id)?.NameAr ?? r.Name!, profiles.GetValueOrDefault(r.Id)?.NameEn ?? r.Name!)).ToList();
    }

    /// <summary>Active users for pickers (delegations).</summary>
    public async Task<List<LookupItem>> UserLookupAsync(CancellationToken ct = default)
    {
        user.RequireAny(Perm.AdminUsers, Perm.AdminDelegations);
        return await db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.FullNameAr)
            .Select(u => new LookupItem(u.Id, u.UserName!, u.FullNameAr + " (" + u.UserName + ")", u.FullNameEn + " (" + u.UserName + ")")).ToListAsync(ct);
    }

    // ---------------- Roles & permission matrix ----------------
    public async Task<List<RoleRow>> RolesAsync(CancellationToken ct = default)
    {
        user.Require(Perm.AdminRoles);
        var rs = await roles.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
        var profiles = await db.RoleProfiles.AsNoTracking().ToDictionaryAsync(p => p.RoleId, ct);
        var perms = await db.RolePermissions.AsNoTracking().ToListAsync(ct);
        var counts = await db.Database.SqlQueryRaw<RoleCount>("SELECT RoleId, COUNT(*) AS Count FROM [identity].[UserRoles] GROUP BY RoleId").ToListAsync(ct);
        return rs.Select(r =>
        {
            var p = profiles.GetValueOrDefault(r.Id);
            return new RoleRow(r.Id, r.Name!, p?.NameAr ?? r.Name!, p?.NameEn ?? r.Name!, p?.DescriptionAr ?? "", p?.IsSystem ?? false,
                counts.FirstOrDefault(c => c.RoleId == r.Id)?.Count ?? 0, perms.Where(x => x.RoleId == r.Id).Select(x => x.Permission).ToHashSet());
        }).ToList();
    }

    public record RoleCount(string RoleId, int Count);

    public async Task<string> SaveRoleAsync(string? roleId, string name, string nameAr, string nameEn, string descriptionAr, CancellationToken ct = default)
    {
        user.Require(Perm.AdminRoles);
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(nameAr)) throw new DomainException("اسم الدور مطلوب.", "Role name is required.");
        IdentityRole r;
        if (string.IsNullOrEmpty(roleId)) { r = new IdentityRole(name.Trim()); var res = await roles.CreateAsync(r); if (!res.Succeeded) throw new DomainException("تعذر إنشاء الدور.", "Could not create role."); }
        else r = await roles.FindByIdAsync(roleId) ?? throw new DomainException("الدور غير موجود.", "Role not found.");
        var p = await db.RoleProfiles.FirstOrDefaultAsync(x => x.RoleId == r.Id, ct);
        if (p is null) { p = new RoleProfile { RoleId = r.Id }; db.RoleProfiles.Add(p); }
        p.NameAr = nameAr; p.NameEn = nameEn; p.DescriptionAr = descriptionAr;
        audit.Log("Admin", string.IsNullOrEmpty(roleId) ? "RoleCreated" : "RoleUpdated", "Role", r.Id, reference: r.Name);
        await db.SaveChangesAsync(ct);
        return r.Id;
    }

    /// <summary>Changing permissions is itself a sensitive operation with a documented log (old and new values).</summary>
    public async Task SetRolePermissionsAsync(string roleId, IReadOnlyCollection<string> permissions, string reason, CancellationToken ct = default)
    {
        user.Require(Perm.AdminRoles);
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("سبب تغيير الصلاحيات مطلوب.", "Reason for changing permissions is required.");
        var valid = Perm.All.Select(p => p.Key).ToHashSet();
        var desired = permissions.Where(valid.Contains).ToHashSet();
        var current = await db.RolePermissions.Where(p => p.RoleId == roleId).ToListAsync(ct);
        var role = await roles.FindByIdAsync(roleId) ?? throw new DomainException("الدور غير موجود.", "Role not found.");
        if (role.Name == DefaultRoles.SystemAdmin && !desired.Contains(Perm.AdminRoles))
            throw new DomainException("لا يمكن سحب إدارة الصلاحيات من دور مدير النظام.", "Cannot remove role administration from System Administrator.");
        var removed = current.Where(c => !desired.Contains(c.Permission)).ToList();
        var added = desired.Where(d => current.All(c => c.Permission != d)).ToList();
        db.RolePermissions.RemoveRange(removed);
        db.RolePermissions.AddRange(added.Select(a => new RolePermission { RoleId = roleId, Permission = a }));
        audit.Log("Admin", "RolePermissionsChanged", "Role", roleId, new { Removed = removed.Select(r => r.Permission) }, new { Added = added }, reason, role.Name);
        await db.SaveChangesAsync(ct);
        // Invalidate sessions of users in the role so the new matrix applies.
        foreach (var u in await users.GetUsersInRoleAsync(role.Name!)) await users.UpdateSecurityStampAsync(u);
    }

    // ---------------- Centers ----------------
    public async Task<List<Center>> CentersAsync(CancellationToken ct = default) => await db.Centers.AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct);

    public async Task SaveCenterAsync(Center input, CancellationToken ct = default)
    {
        user.Require(Perm.AdminCenters);
        if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.NameAr)) throw new DomainException("الرمز والاسم مطلوبان.", "Code and name are required.");
        Center c;
        if (input.Id == 0) { c = new Center(); db.Centers.Add(c); } else c = await db.Centers.FirstAsync(x => x.Id == input.Id, ct);
        c.Code = input.Code; c.NameAr = input.NameAr; c.NameEn = input.NameEn; c.DepartmentAr = input.DepartmentAr; c.DepartmentEn = input.DepartmentEn; c.IsActive = input.IsActive;
        audit.Log("Admin", input.Id == 0 ? "CenterCreated" : "CenterUpdated", nameof(Center), c.Id, newValues: new { c.Code, c.NameAr, c.IsActive });
        await db.SaveChangesAsync(ct);
    }

    // ---------------- Approval matrix ----------------
    public async Task<List<ApprovalLimit>> LimitsAsync(CancellationToken ct = default)
    {
        user.Require(Perm.AdminApprovalMatrix);
        return await db.ApprovalLimits.AsNoTracking().OrderBy(l => l.Module).ThenBy(l => l.MaxAmount).ToListAsync(ct);
    }

    public async Task SaveLimitAsync(ApprovalLimit input, CancellationToken ct = default)
    {
        user.Require(Perm.AdminApprovalMatrix);
        if (input.MaxAmount <= 0 || string.IsNullOrEmpty(input.RoleId)) throw new DomainException("الدور والحد مطلوبان.", "Role and limit are required.");
        ApprovalLimit l;
        if (input.Id == 0) { l = new ApprovalLimit(); db.ApprovalLimits.Add(l); } else l = await db.ApprovalLimits.FirstAsync(x => x.Id == input.Id, ct);
        var old = new { l.MaxAmount, l.IsActive };
        l.RoleId = input.RoleId; l.Module = input.Module; l.CenterId = input.CenterId; l.ExpenseType = input.ExpenseType; l.MaxAmount = input.MaxAmount; l.IsActive = input.IsActive; l.Currency = input.Currency;
        audit.Log("Admin", "ApprovalLimitSaved", nameof(ApprovalLimit), l.Id, old, new { l.Module, l.RoleId, l.MaxAmount, l.IsActive });
        await db.SaveChangesAsync(ct);
    }

    // ---------------- Delegations ----------------
    public async Task<List<Delegation>> DelegationsAsync(CancellationToken ct = default)
    {
        user.Require(Perm.AdminDelegations);
        return await db.Delegations.AsNoTracking().OrderByDescending(d => d.Id).ToListAsync(ct);
    }

    public async Task SaveDelegationAsync(Delegation d, CancellationToken ct = default)
    {
        user.Require(Perm.AdminDelegations);
        if (d.FromUserId == d.ToUserId || d.ToDate <= d.FromDate || string.IsNullOrWhiteSpace(d.Reason))
            throw new DomainException("بيانات التفويض غير صحيحة (مستخدمان مختلفان، مدة، سبب).", "Invalid delegation (two users, period, reason).");
        db.Delegations.Add(d);
        audit.Log("Admin", "DelegationCreated", nameof(Delegation), null, null, new { d.FromUserId, d.ToUserId, d.Module, d.FromDate, d.ToDate }, d.Reason);
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeDelegationAsync(int id, CancellationToken ct = default)
    {
        user.Require(Perm.AdminDelegations);
        var d = await db.Delegations.FirstAsync(x => x.Id == id, ct);
        d.IsRevoked = true;
        audit.Log("Admin", "DelegationRevoked", nameof(Delegation), id);
        await db.SaveChangesAsync(ct);
    }

    // ---------------- Settings ----------------
    public async Task<List<SystemSetting>> SettingsAsync(CancellationToken ct = default)
    {
        user.Require(Perm.AdminSettings);
        return await db.Settings.AsNoTracking().OrderBy(s => s.Key).ToListAsync(ct);
    }

    public async Task SaveSettingAsync(string key, string value, CancellationToken ct = default)
    {
        user.Require(Perm.AdminSettings);
        var s = await db.Settings.FirstAsync(x => x.Key == key, ct);
        audit.Log("Admin", "SettingChanged", nameof(SystemSetting), key, new { s.Value }, new { Value = value });
        s.Value = value;
        await db.SaveChangesAsync(ct);
    }

    // ---------------- Integration monitor ----------------
    public async Task<List<OutboxMessage>> OutboxAsync(OutboxState? state, CancellationToken ct = default)
    {
        user.Require(Perm.IntegrationMonitor);
        var q = db.Outbox.AsNoTracking().AsQueryable();
        if (state is not null) q = q.Where(o => o.State == state);
        return await q.OrderByDescending(o => o.Id).Take(200).ToListAsync(ct);
    }

    /// <summary>Retry is safe: the consumer de-duplicates by idempotency key.</summary>
    public async Task RetryAsync(long id, CancellationToken ct = default)
    {
        user.Require(Perm.IntegrationMonitor);
        var m = await db.Outbox.FirstAsync(o => o.Id == id, ct);
        m.State = OutboxState.Pending; m.NextAttemptAt = DateTime.UtcNow; m.LastError = null;
        audit.Log("Integration", "MessageRetried", nameof(OutboxMessage), id, reference: m.Reference);
        await db.SaveChangesAsync(ct);
    }
}
