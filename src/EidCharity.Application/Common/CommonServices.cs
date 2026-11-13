using System.Text.Json;
using EidCharity.Domain;
using EidCharity.Domain.Integration;
using Microsoft.EntityFrameworkCore;

namespace EidCharity.Application.Common;

public class AuditService(IEidDb db, ICurrentUser user)
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    /// <summary>Explicit business action entry (in addition to the automatic field-level change log).</summary>
    public void Log(string module, string action, string entityType, object? entityId, object? oldValues = null, object? newValues = null, string? reason = null, string? reference = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            At = DateTime.UtcNow,
            UserId = user.UserId,
            UserName = user.UserName,
            Module = module,
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, Json),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, Json),
            Reason = reason,
            Reference = reference,
        });
    }

    public async Task<PagedResult<AuditLog>> SearchAsync(string? text, string? module, DateTime? from, DateTime? to, int page, CancellationToken ct = default)
    {
        user.Require(Perm.AuditView);
        var q = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(module)) q = q.Where(a => a.Module == module);
        if (!string.IsNullOrWhiteSpace(text))
            q = q.Where(a => a.Action.Contains(text) || a.EntityType.Contains(text) || (a.EntityId != null && a.EntityId.Contains(text))
                          || (a.UserName != null && a.UserName.Contains(text)) || (a.Reference != null && a.Reference.Contains(text)));
        if (from is not null) q = q.Where(a => a.At >= from);
        if (to is not null) q = q.Where(a => a.At < to.Value.AddDays(1));
        return await q.OrderByDescending(a => a.Id).PageAsync(page, 30, ct);
    }
}

public class SettingsService(IEidDb db)
{
    public async Task<string> GetAsync(string key, string fallback = "", CancellationToken ct = default) =>
        (await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct))?.Value ?? fallback;

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default) =>
        int.TryParse(await GetAsync(key, "", ct), out var v) ? v : fallback;

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default) =>
        decimal.TryParse(await GetAsync(key, "", ct), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
}

public class NotificationService(IEidDb db, ICurrentUser user)
{
    /// <summary>Adds a notification once per dedup key.</summary>
    public async Task NotifyAsync(string titleAr, string titleEn, string? link, string? permission = null, string? userId = null, int? centerId = null, string severity = "info", string? dedupKey = null, CancellationToken ct = default)
    {
        if (dedupKey is not null && await db.Notifications.AnyAsync(n => n.DedupKey == dedupKey, ct)) return;
        db.Notifications.Add(new Notification
        {
            At = DateTime.UtcNow, TitleAr = titleAr, TitleEn = titleEn, Link = link, Permission = permission,
            UserId = userId, CenterId = centerId, Severity = severity, DedupKey = dedupKey,
        });
    }

    private IQueryable<Notification> Mine()
    {
        var perms = user.Permissions.ToList();
        var uid = user.UserId;
        var all = user.AllCenters();
        var cid = user.CenterId;
        return db.Notifications.Where(n => (n.UserId == uid || (n.Permission != null && perms.Contains(n.Permission)))
                                           && (all || n.CenterId == null || n.CenterId == cid));
    }

    public Task<List<Notification>> RecentAsync(int take = 15, CancellationToken ct = default) =>
        Mine().AsNoTracking().OrderByDescending(n => n.Id).Take(take).ToListAsync(ct);

    public Task<int> UnreadCountAsync(CancellationToken ct = default) => Mine().CountAsync(n => !n.IsRead, ct);

    public async Task MarkAllReadAsync(CancellationToken ct = default)
    {
        await Mine().Where(n => !n.IsRead && n.UserId == user.UserId).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }
}

public class OutboxPublisher(IEidDb db)
{
    /// <summary>Adds the message in the same transaction as the business change. Duplicate keys are ignored.</summary>
    public async Task PublishAsync(string type, object payload, string idempotencyKey, string? reference = null, CancellationToken ct = default)
    {
        if (await db.Outbox.AnyAsync(o => o.IdempotencyKey == idempotencyKey, ct)) return;
        if (db.Outbox.Local.Any(o => o.IdempotencyKey == idempotencyKey)) return;
        db.Outbox.Add(new OutboxMessage
        {
            Type = type,
            Payload = JsonSerializer.Serialize(payload, AuditService.Json),
            IdempotencyKey = idempotencyKey,
            Reference = reference,
            CreatedAt = DateTime.UtcNow,
            State = OutboxState.Pending,
            NextAttemptAt = DateTime.UtcNow,
        });
    }
}

/// <summary>Institutional approval matrix + delegation + segregation of duties.</summary>
public class ApprovalMatrixService(IEidDb db, ICurrentUser user)
{
    public async Task<decimal?> MaxAmountAsync(ApprovalModule module, int? centerId, string? expenseType, CancellationToken ct = default)
    {
        if (user.UserId is null) return null;
        var roleIds = await db.Database.SqlQueryRaw<string>(
            "SELECT RoleId AS Value FROM [identity].[UserRoles] WHERE UserId = {0}", user.UserId).ToListAsync(ct);

        // Delegations received and currently valid add the delegator's roles for this module.
        var now = DateTime.UtcNow;
        var delegators = await db.Delegations.Where(d => d.ToUserId == user.UserId && d.Module == module && !d.IsRevoked && d.FromDate <= now && d.ToDate >= now)
            .Select(d => d.FromUserId).ToListAsync(ct);
        foreach (var from in delegators)
            roleIds.AddRange(await db.Database.SqlQueryRaw<string>("SELECT RoleId AS Value FROM [identity].[UserRoles] WHERE UserId = {0}", from).ToListAsync(ct));

        var limits = await db.ApprovalLimits.Where(l => l.IsActive && l.Module == module && roleIds.Contains(l.RoleId)
                                                         && (l.CenterId == null || l.CenterId == centerId)
                                                         && (l.ExpenseType == null || l.ExpenseType == expenseType))
            .Select(l => l.MaxAmount).ToListAsync(ct);
        return limits.Count == 0 ? null : limits.Max();
    }

    public async Task EnsureWithinLimitAsync(ApprovalModule module, decimal amount, int? centerId, string? expenseType, CancellationToken ct = default)
    {
        var max = await MaxAmountAsync(module, centerId, expenseType, ct);
        if (max is null)
            throw new DomainException("لا يوجد حد اعتماد معرّف لدورك في مصفوفة الاعتماد لهذا الإجراء.", "No approval limit is defined for your role in the approval matrix.");
        if (amount > max)
            throw new DomainException($"المبلغ {amount:N2} يتجاوز حد اعتمادك ({max:N2}). يلزم اعتماد من مستوى أعلى.",
                                      $"Amount {amount:N2} exceeds your approval limit ({max:N2}). Escalation required.");
    }

    public static void EnsureNotSelf(string? requesterId, string? approverId)
    {
        if (requesterId is not null && requesterId == approverId)
            throw new DomainException("فصل المهام: لا يمكنك اعتماد طلب قدمته بنفسك.", "Segregation of duties: you cannot approve your own request.");
    }
}

/// <summary>Documents attached to any record (project study, license, invoices, receiving minutes…). Files are versioned, never overwritten.</summary>
public class AttachmentService(IEidDb db, ICurrentUser user, IFileStore files, AuditService audit)
{
    public static readonly string[] AllowedExtensions = [".pdf", ".png", ".jpg", ".jpeg", ".webp", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt"];
    public const long MaxBytes = 15 * 1024 * 1024;

    public async Task<List<Attachment>> ListAsync(string ownerType, int ownerId, CancellationToken ct = default)
    {
        if (!user.IsAuthenticated) throw new ForbiddenException("auth");
        var list = await db.Attachments.AsNoTracking().Where(a => a.OwnerType == ownerType && a.OwnerId == ownerId).OrderByDescending(a => a.Id).ToListAsync(ct);
        // Sensitive documents are listed only for holders of the sensitive-data permission.
        return user.Has(Perm.BeneficiariesViewSensitive) || user.Has(Perm.AuditView) ? list : list.Where(a => !a.IsSensitive).ToList();
    }

    public async Task<int> UploadAsync(string ownerType, int ownerId, AttachmentCategory category, Stream content, string fileName, string contentType, long size,
        bool sensitive, string? reference, CancellationToken ct = default)
    {
        if (!user.IsAuthenticated) throw new ForbiddenException("auth");
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext)) throw new DomainException("نوع الملف غير مسموح.", "File type is not allowed.");
        if (size <= 0 || size > MaxBytes) throw new DomainException("حجم الملف يتجاوز 15 ميجابايت.", "File exceeds 15 MB.");
        var path = await files.SaveAsync(content, fileName, ct);
        var version = await db.Attachments.CountAsync(a => a.OwnerType == ownerType && a.OwnerId == ownerId && a.Category == category && a.FileName == fileName, ct) + 1;
        var a = new Attachment
        {
            OwnerType = ownerType, OwnerId = ownerId, Category = category, FileName = fileName, ContentType = string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType,
            Size = size, StoragePath = path, Version = version, IsSensitive = sensitive, Reference = reference,
        };
        db.Attachments.Add(a);
        await db.SaveChangesAsync(ct);
        audit.Log("Files", "Uploaded", ownerType, ownerId, newValues: new { a.FileName, a.Category, a.Version, a.Size }, reference: fileName);
        await db.SaveChangesAsync(ct);
        return a.Id;
    }
}
