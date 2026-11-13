using EidCharity.Application.Common;
using EidCharity.Domain;
using EidCharity.Domain.Collections;
using EidCharity.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace EidCharity.Application.Projects;

public class LicenseInput
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public string Issuer { get; set; } = "هيئة تنظيم الأعمال الخيرية";
    public DateOnly IssueDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly ValidFrom { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly ValidTo { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(1));
    public DateOnly CollectionFrom { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly CollectionTo { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(1));
    public LicenseScope Scope { get; set; } = LicenseScope.Project;
    public int? ProgramId { get; set; }
    public decimal Cap { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    public string Channels { get; set; } = "";
    public string Purposes { get; set; } = "";
    public string Conditions { get; set; } = "";
}

public record LicenseUsage(int LicenseId, decimal Cap, decimal Confirmed, decimal PendingHold, decimal Remaining)
{
    public decimal UsedPercent => Cap == 0 ? 0 : Math.Round((Confirmed + PendingHold) / Cap * 100, 1);
}

public record LicenseRow(License License, LicenseStatus Status, LicenseUsage Usage, int ProjectCount, int DaysToExpiry);

public class LicenseService(IEidDb db, ICurrentUser user, AuditService audit)
{
    public static DateOnly Today => DateOnly.FromDateTime(QatarTime.Now);

    public async Task<List<LicenseRow>> ListAsync(string? search = null, bool includeSuperseded = false, CancellationToken ct = default)
    {
        user.Require(Perm.LicensesView);
        var q = db.Licenses.AsNoTracking().Include(l => l.Program).AsQueryable();
        if (!includeSuperseded) q = q.Where(l => !l.IsSuperseded);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(l => l.Number.Contains(search) || l.Purposes.Contains(search) || l.Issuer.Contains(search));
        var list = await q.OrderBy(l => l.ValidTo).ToListAsync(ct);
        var ids = list.Select(l => l.Id).ToList();
        var usage = await UsageAsync(ids, ct);
        var counts = await db.Mappings.Where(m => ids.Contains(m.LicenseId) && m.Status != MappingStatus.Superseded)
            .GroupBy(m => m.LicenseId).Select(g => new { g.Key, C = g.Select(x => x.ProjectId).Distinct().Count() }).ToDictionaryAsync(x => x.Key, x => x.C, ct);
        return list.Select(l => new LicenseRow(l, l.StatusAt(Today), usage[l.Id], counts.GetValueOrDefault(l.Id), l.ValidTo.DayNumber - Today.DayNumber)).ToList();
    }

    /// <summary>
    /// Usage of each license cap across ALL projects linked to it (a shared cap is watched once, never duplicated).
    /// Pending (unverified) transactions hold part of the cap until confirmed, failed or cancelled.
    /// </summary>
    public async Task<Dictionary<int, LicenseUsage>> UsageAsync(IReadOnlyCollection<int> licenseIds, CancellationToken ct = default)
    {
        var rows = await db.DonationLines.Where(l => licenseIds.Contains(l.LicenseId))
            .Join(db.Donations, l => l.DonationId, d => d.Id, (l, d) => new { l.LicenseId, d.Status, Net = l.Amount - l.RefundedAmount - l.ReallocatedAmount })
            .Where(x => x.Status == DonationStatus.Confirmed || x.Status == DonationStatus.PartiallyRefunded || x.Status == DonationStatus.Refunded || x.Status == DonationStatus.PendingVerification)
            .GroupBy(x => new { x.LicenseId, Pending = x.Status == DonationStatus.PendingVerification })
            .Select(g => new { g.Key.LicenseId, g.Key.Pending, Sum = g.Sum(x => x.Net) }).ToListAsync(ct);
        var caps = await db.Licenses.Where(l => licenseIds.Contains(l.Id)).Select(l => new { l.Id, l.Cap }).ToDictionaryAsync(x => x.Id, x => x.Cap, ct);
        return licenseIds.Distinct().ToDictionary(id => id, id =>
        {
            var confirmed = rows.Where(r => r.LicenseId == id && !r.Pending).Sum(r => r.Sum);
            var pending = rows.Where(r => r.LicenseId == id && r.Pending).Sum(r => r.Sum);
            var cap = caps.GetValueOrDefault(id);
            return new LicenseUsage(id, cap, confirmed, pending, Math.Max(0, cap - confirmed - pending));
        });
    }

    public async Task<License> GetAsync(int id, CancellationToken ct = default)
    {
        user.Require(Perm.LicensesView);
        return await db.Licenses.Include(l => l.Program).FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw ProjectService.NotFound();
    }

    public async Task<int> SaveAsync(LicenseInput i, CancellationToken ct = default)
    {
        user.Require(Perm.LicensesManage);
        if (string.IsNullOrWhiteSpace(i.Number)) throw new DomainException("رقم الترخيص مطلوب.", "License number is required.");
        if (i.ValidTo < i.ValidFrom || i.CollectionTo < i.CollectionFrom) throw new DomainException("تواريخ الترخيص غير منطقية.", "License dates are inconsistent.");
        if (i.CollectionFrom < i.ValidFrom || i.CollectionTo > i.ValidTo)
            throw new DomainException("فترة التحصيل يجب أن تقع داخل فترة سريان الترخيص.", "Collection period must lie within license validity.");
        if (i.Cap <= 0) throw new DomainException("سقف الترخيص مطلوب.", "License cap is required.");
        if (i.Scope == LicenseScope.ProgramAndProjects && i.ProgramId is null) throw new DomainException("حدد البرنامج المشمول بالترخيص.", "Select the covered program.");
        if (await db.Licenses.AnyAsync(l => l.Number == i.Number && l.Id != i.Id && !l.IsSuperseded, ct))
            throw new DomainException("رقم الترخيص مستخدم.", "License number already exists.");

        License l;
        if (i.Id == 0) { l = new License(); db.Licenses.Add(l); }
        else
        {
            l = await db.Licenses.FirstAsync(x => x.Id == i.Id, ct);
            // A cap / scope change on a license already in use goes back to finance for every approved project using it.
            var substantive = l.Cap != i.Cap || l.CollectionFrom != i.CollectionFrom || l.CollectionTo != i.CollectionTo || l.Scope != i.Scope || l.ProgramId != i.ProgramId;
            if (substantive)
            {
                var used = await db.DonationLines.AnyAsync(x => x.LicenseId == l.Id, ct);
                if (used && i.Cap < (await UsageAsync([l.Id], ct))[l.Id].Confirmed)
                    throw new DomainException("لا يمكن خفض السقف تحت المحصّل المؤكد.", "Cap cannot go below confirmed collections.");
                var projects = await db.Mappings.Where(m => m.LicenseId == l.Id && m.Status == MappingStatus.Active).Select(m => m.Project!).Distinct().ToListAsync(ct);
                foreach (var p in projects.Where(p => p.ApprovalStatus == ApprovalStatus.FinanciallyApproved))
                {
                    p.ApprovalStatus = ApprovalStatus.AwaitingFinance;
                    db.ProjectReviews.Add(new ProjectReviewEntry { ProjectId = p.Id, Action = ReviewAction.SubstantiveChange, FromStatus = ApprovalStatus.FinanciallyApproved,
                        ToStatus = ApprovalStatus.AwaitingFinance, ByUserName = user.DisplayName, Note = $"تعديل جوهري على الترخيص {l.Number}" });
                }
            }
        }
        var old = new { l.Cap, l.ValidFrom, l.ValidTo, l.CollectionFrom, l.CollectionTo };
        l.Number = i.Number.Trim(); l.Issuer = i.Issuer; l.IssueDate = i.IssueDate; l.ValidFrom = i.ValidFrom; l.ValidTo = i.ValidTo;
        l.CollectionFrom = i.CollectionFrom; l.CollectionTo = i.CollectionTo; l.Scope = i.Scope; l.ProgramId = i.Scope == LicenseScope.ProgramAndProjects ? i.ProgramId : null;
        l.Cap = i.Cap; l.Currency = i.Currency; l.Channels = i.Channels; l.Purposes = i.Purposes; l.Conditions = i.Conditions;
        await db.SaveChangesAsync(ct);
        audit.Log("Projects", i.Id == 0 ? "LicenseCreated" : "LicenseUpdated", nameof(License), l.Id, i.Id == 0 ? null : old, i, reference: l.Number);
        await db.SaveChangesAsync(ct);
        return l.Id;
    }

    /// <summary>Renewal creates a new version; the previous one is kept and historical transactions keep their reference.</summary>
    public async Task<int> RenewAsync(int licenseId, LicenseInput renewed, CancellationToken ct = default)
    {
        user.Require(Perm.LicensesManage);
        var old = await db.Licenses.FirstAsync(l => l.Id == licenseId, ct);
        renewed.Id = 0;
        if (renewed.Number == old.Number) old.IsSuperseded = true; // same number ⇒ previous version kept as history
        var id = await SaveAsync(renewed, ct);
        var nl = await db.Licenses.FirstAsync(l => l.Id == id, ct);
        nl.PreviousLicenseId = old.Id; nl.VersionNo = old.VersionNo + 1;
        audit.Log("Projects", "LicenseRenewed", nameof(License), id, new { old.Number, old.ValidTo }, new { nl.Number, nl.ValidTo }, reference: nl.Number);
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task SetSuspendedAsync(int licenseId, bool suspended, string reason, CancellationToken ct = default)
    {
        user.Require(Perm.LicensesManage);
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("السبب مطلوب.", "Reason is required.");
        var l = await db.Licenses.FirstAsync(x => x.Id == licenseId, ct);
        l.IsSuspended = suspended; l.SuspensionReason = suspended ? reason : null;
        audit.Log("Projects", suspended ? "LicenseSuspended" : "LicenseResumed", nameof(License), l.Id, reason: reason, reference: l.Number);
        await db.SaveChangesAsync(ct);
    }
}

public class FinancialItemService(IEidDb db, ICurrentUser user, AuditService audit)
{
    public Task<List<FinancialItem>> ListAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        user.Require(Perm.ProjectsView);
        var q = db.FinancialItems.AsNoTracking();
        if (activeOnly) q = q.Where(i => i.IsActive);
        return q.OrderBy(i => i.Number).ToListAsync(ct);
    }

    public async Task<int> SaveAsync(FinancialItem input, CancellationToken ct = default)
    {
        user.Require(Perm.FinanceItemsManage);
        if (string.IsNullOrWhiteSpace(input.Number) || string.IsNullOrWhiteSpace(input.NameAr))
            throw new DomainException("رقم واسم البند مطلوبان.", "Item number and name are required.");
        if (await db.FinancialItems.AnyAsync(i => i.Number == input.Number && i.Id != input.Id, ct))
            throw new DomainException("رقم البند مستخدم.", "Item number already exists.");
        FinancialItem item;
        if (input.Id == 0) { item = new FinancialItem(); db.FinancialItems.Add(item); }
        else item = await db.FinancialItems.FirstAsync(i => i.Id == input.Id, ct);
        item.Number = input.Number.Trim(); item.NameAr = input.NameAr.Trim(); item.NameEn = input.NameEn.Trim(); item.GlMapping = input.GlMapping; item.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct);
        audit.Log("Finance", input.Id == 0 ? "FinancialItemCreated" : "FinancialItemUpdated", nameof(FinancialItem), item.Id, newValues: new { item.Number, item.NameAr, item.IsActive }, reference: item.Number);
        await db.SaveChangesAsync(ct);
        return item.Id;
    }
}
