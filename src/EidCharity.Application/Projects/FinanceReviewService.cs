using System.Text.Json;
using EidCharity.Application.Common;
using EidCharity.Domain;
using EidCharity.Domain.Integration;
using EidCharity.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace EidCharity.Application.Projects;

public class MappingLineInput
{
    public int LicenseId { get; set; }
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public decimal LinkCap { get; set; }
    public string? ScopeNote { get; set; }
}

/// <summary>Payload sent to the Collections system. Reference data the collector cannot edit.</summary>
public record MappingPublishedPayload(int ProjectId, int ProgramId, int CenterId, string ProjectCode, string ProgramCode,
    string NameAr, string NameEn, string ProgramNameAr, string ProgramNameEn, string CenterNameAr, string CenterNameEn,
    string FinancialItemNumber, string FinancialItemNameAr, string Visual, string QuickAmounts, decimal CollectionCap,
    bool IsAvailable, string? UnavailableReason, int MappingVersion, DateTime PublishedAt,
    List<MappingLicensePayload> Licenses);

public record MappingLicensePayload(int LicenseId, string Number, DateOnly From, DateOnly To, decimal Cap, decimal LinkCap);

public class FinanceReviewService(IEidDb db, ICurrentUser user, AuditService audit, OutboxPublisher outbox, NotificationService notify)
{
    public async Task<List<ProjectRow>> InboxAsync(string tab, CancellationToken ct = default)
    {
        user.Require(Perm.FinanceReview);
        var q = db.Projects.AsNoTracking().ScopeToCenter(user, p => p.CenterId);
        q = tab switch
        {
            "returned" => q.Where(p => p.ApprovalStatus == ApprovalStatus.ReturnedToCenter),
            "approved" => q.Where(p => p.ApprovalStatus == ApprovalStatus.FinanciallyApproved),
            "sync" => q.Where(p => p.MappingSyncStatus == SyncStatus.Failed || p.MappingSyncStatus == SyncStatus.Pending),
            "suspended" => q.Where(p => p.ApprovalStatus == ApprovalStatus.Suspended || p.ApprovalStatus == ApprovalStatus.Rejected),
            _ => q.Where(p => p.ApprovalStatus == ApprovalStatus.AwaitingFinance || p.ApprovalStatus == ApprovalStatus.UnderFinanceReview),
        };
        return await q.OrderBy(p => p.SubmittedAt).Select(ProjectService.ToRow()).ToListAsync(ct);
    }

    public async Task<Dictionary<string, int>> InboxCountsAsync(CancellationToken ct = default)
    {
        user.Require(Perm.FinanceReview);
        var q = db.Projects.ScopeToCenter(user, p => p.CenterId);
        return new()
        {
            ["pending"] = await q.CountAsync(p => p.ApprovalStatus == ApprovalStatus.AwaitingFinance || p.ApprovalStatus == ApprovalStatus.UnderFinanceReview, ct),
            ["returned"] = await q.CountAsync(p => p.ApprovalStatus == ApprovalStatus.ReturnedToCenter, ct),
            ["approved"] = await q.CountAsync(p => p.ApprovalStatus == ApprovalStatus.FinanciallyApproved, ct),
            ["sync"] = await q.CountAsync(p => p.MappingSyncStatus == SyncStatus.Failed || p.MappingSyncStatus == SyncStatus.Pending, ct),
            ["suspended"] = await q.CountAsync(p => p.ApprovalStatus == ApprovalStatus.Suspended || p.ApprovalStatus == ApprovalStatus.Rejected, ct),
        };
    }

    public async Task StartReviewAsync(int projectId, CancellationToken ct = default)
    {
        user.Require(Perm.FinanceReview);
        var p = await Load(projectId, ct);
        if (p.ApprovalStatus != ApprovalStatus.AwaitingFinance) return;
        Transition(p, ApprovalStatus.UnderFinanceReview, ReviewAction.ReviewStarted, null);
        p.ReviewerId = user.UserId;
        await db.SaveChangesAsync(ct);
    }

    public async Task ReturnToCenterAsync(int projectId, string note, CancellationToken ct = default)
    {
        user.Require(Perm.FinanceReview);
        if (string.IsNullOrWhiteSpace(note)) throw new DomainException("اكتب ملاحظات محددة للمركز.", "Write specific notes for the center.");
        var p = await Load(projectId, ct);
        EnsureReviewable(p);
        Transition(p, ApprovalStatus.ReturnedToCenter, ReviewAction.ReturnedToCenter, note);
        await notify.NotifyAsync($"أُعيد المشروع {p.NameAr} بملاحظات", $"Project {p.NameEn} returned with notes", $"/projects/{p.Id}", Perm.ProjectsEdit, centerId: p.CenterId, severity: "warning", ct: ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task RejectAsync(int projectId, string reason, CancellationToken ct = default)
    {
        user.Require(Perm.FinanceReview);
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("سبب الرفض مطلوب.", "Rejection reason is required.");
        var p = await Load(projectId, ct);
        EnsureReviewable(p);
        p.StatusReason = reason;
        Transition(p, ApprovalStatus.Rejected, ReviewAction.Rejected, reason);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Finance chooses the financial item and links it to the project and one or more licenses.</summary>
    public async Task SaveMappingAsync(int projectId, int financialItemId, List<MappingLineInput> lines, CancellationToken ct = default)
    {
        user.Require(Perm.FinanceMap);
        var p = await Load(projectId, ct);
        EnsureReviewable(p);
        var item = await db.FinancialItems.FirstOrDefaultAsync(i => i.Id == financialItemId && i.IsActive, ct)
                   ?? throw new DomainException("اختر بندًا ماليًا نشطًا.", "Select an active financial item.");
        if (lines.Count == 0) throw new DomainException("اربط ترخيصًا واحدًا على الأقل.", "Link at least one license.");
        if (lines.GroupBy(l => l.LicenseId).Any(g => g.Count() > 1)) throw new DomainException("الترخيص مكرر في الربط.", "Duplicate license in mapping.");

        var licIds = lines.Select(l => l.LicenseId).ToList();
        var licenses = await db.Licenses.Where(l => licIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        foreach (var line in lines)
        {
            var lic = licenses.GetValueOrDefault(line.LicenseId) ?? throw ProjectService.NotFound();
            if (line.PeriodFrom < lic.CollectionFrom || line.PeriodTo > lic.CollectionTo || line.PeriodTo < line.PeriodFrom)
                throw new DomainException($"فترة الربط مع {lic.Number} خارج فترة التحصيل المسموحة.", $"Mapping period for {lic.Number} is outside the license collection period.");
            if (lic.Scope == LicenseScope.ProgramAndProjects && lic.ProgramId != p.ProgramId)
                throw new DomainException($"الترخيص {lic.Number} لا يغطي برنامج هذا المشروع.", $"License {lic.Number} does not cover this project's program.");
            if (line.LinkCap < 0 || line.LinkCap > lic.Cap)
                throw new DomainException($"سقف الربط مع {lic.Number} يتجاوز سقف الترخيص.", $"Link cap for {lic.Number} exceeds the license cap.");
            // A shared license cap is not duplicated across projects: sum of link sub-caps on the same license must fit within it.
            var otherLinkCaps = await db.Mappings.Where(m => m.LicenseId == lic.Id && m.ProjectId != p.Id && m.Status == MappingStatus.Active).SumAsync(m => m.LinkCap, ct);
            if (line.LinkCap > 0 && otherLinkCaps + line.LinkCap > lic.Cap)
                throw new DomainException($"مجموع سقوف المشاريع على الترخيص المشترك {lic.Number} يتجاوز سقفه.", $"Sum of project sub-caps on shared license {lic.Number} exceeds its cap.");
        }

        var drafts = p.Mappings.Where(m => m.Status == MappingStatus.Draft).ToList();
        foreach (var d in drafts) db.Mappings.Remove(d);
        foreach (var line in lines)
            p.Mappings.Add(new ProjectLicenseMapping
            {
                ProjectId = p.Id, FinancialItemId = item.Id, LicenseId = line.LicenseId, PeriodFrom = line.PeriodFrom, PeriodTo = line.PeriodTo,
                LinkCap = line.LinkCap, ScopeNote = line.ScopeNote, Status = MappingStatus.Draft,
            });
        db.ProjectReviews.Add(new ProjectReviewEntry { ProjectId = p.Id, Action = ReviewAction.Mapped, FromStatus = p.ApprovalStatus, ToStatus = p.ApprovalStatus, ByUserName = user.DisplayName,
            Note = $"{item.Number} — {item.NameAr} ⟵ {string.Join("، ", lines.Select(l => licenses[l.LicenseId].Number))}" });
        audit.Log("Finance", "MappingSaved", nameof(Project), p.Id, newValues: new { item.Number, lines }, reference: p.Code);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>"اعتماد الإتاحة للتحصيل": approves the data + mapping, then publishes it to Collections.</summary>
    public async Task ApproveEligibilityAsync(int projectId, string? note, CancellationToken ct = default)
    {
        user.Require(Perm.FinanceApproveEligibility);
        var p = await Load(projectId, ct);
        EnsureReviewable(p);
        var draft = p.Mappings.Where(m => m.Status == MappingStatus.Draft).ToList();
        var effective = draft.Count > 0 ? draft : p.Mappings.Where(m => m.Status == MappingStatus.Active).ToList();
        if (effective.Count == 0 || effective.Any(m => m.FinancialItemId is null))
            throw new DomainException("حدد البند المالي واربطه بالترخيص قبل الاعتماد.", "Set the financial item and link licenses before approval.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Apply substantive changes that were waiting for re-approval.
        if (p.PendingChangesJson is not null)
        {
            var pending = JsonSerializer.Deserialize<ProjectInput>(p.PendingChangesJson)!;
            ProjectService.Apply(p, pending);
            p.PendingChangesJson = null;
        }
        p.MappingVersion++;
        if (draft.Count > 0)
            foreach (var old in p.Mappings.Where(m => m.Status == MappingStatus.Active)) old.Status = MappingStatus.Superseded; // history kept, not re-linked retroactively
        foreach (var m in effective) { m.Status = MappingStatus.Active; m.MappingVersion = p.MappingVersion; }

        p.ApprovedById = user.UserId; p.ApprovedAt = DateTime.UtcNow;
        Transition(p, ApprovalStatus.FinanciallyApproved, ReviewAction.Approved, note);
        p.CollectionStatus = ComputeCollectionStatus(p, effective);
        await db.SaveChangesAsync(ct);
        await PublishAsync(p, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task SuspendEligibilityAsync(int projectId, string reason, CancellationToken ct = default)
    {
        user.Require(Perm.FinanceSuspendEligibility);
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("سبب الإيقاف مطلوب.", "Suspension reason is required.");
        var p = await Load(projectId, ct);
        p.CollectionStatus = CollectionStatus.Suspended;
        p.StatusReason = reason;
        db.ProjectReviews.Add(new ProjectReviewEntry { ProjectId = p.Id, Action = ReviewAction.Suspended, FromStatus = p.ApprovalStatus, ToStatus = p.ApprovalStatus, ByUserName = user.DisplayName, Note = reason });
        audit.Log("Finance", "EligibilitySuspended", nameof(Project), p.Id, reason: reason, reference: p.Code);
        await db.SaveChangesAsync(ct);
        await PublishAsync(p, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task ResumeEligibilityAsync(int projectId, CancellationToken ct = default)
    {
        user.Require(Perm.FinanceSuspendEligibility);
        var p = await Load(projectId, ct);
        if (p.ApprovalStatus != ApprovalStatus.FinanciallyApproved) throw new DomainException("المشروع غير معتمد ماليًا.", "Project is not financially approved.");
        p.StatusReason = null;
        p.CollectionStatus = ComputeCollectionStatus(p, p.Mappings.Where(m => m.Status == MappingStatus.Active).ToList());
        db.ProjectReviews.Add(new ProjectReviewEntry { ProjectId = p.Id, Action = ReviewAction.Resumed, FromStatus = p.ApprovalStatus, ToStatus = p.ApprovalStatus, ByUserName = user.DisplayName });
        audit.Log("Finance", "EligibilityResumed", nameof(Project), p.Id, reference: p.Code);
        await db.SaveChangesAsync(ct);
        await PublishAsync(p, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Re-sends the current mapping (same version ⇒ same key ⇒ idempotent on the consumer).</summary>
    public async Task ResendMappingAsync(int projectId, CancellationToken ct = default)
    {
        user.Require(Perm.FinanceApproveEligibility);
        var p = await Load(projectId, ct);
        var existing = await db.Outbox.Where(o => o.Type == MessageTypes.MappingPublished && o.Reference == p.Code).OrderByDescending(o => o.Id).FirstOrDefaultAsync(ct);
        if (existing is not null && existing.State == OutboxState.Failed)
        {
            existing.State = OutboxState.Pending; existing.NextAttemptAt = DateTime.UtcNow; existing.LastError = null;
            p.MappingSyncStatus = SyncStatus.Pending;
            audit.Log("Integration", "MappingResent", nameof(Project), p.Id, reference: p.Code);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await PublishAsync(p, ct, force: true);
            await db.SaveChangesAsync(ct);
        }
    }

    internal static CollectionStatus ComputeCollectionStatus(Project p, IEnumerable<ProjectLicenseMapping> active)
    {
        if (p.IsClosed) return CollectionStatus.Closed;
        if (p.ApprovalStatus != ApprovalStatus.FinanciallyApproved) return CollectionStatus.NotAvailable;
        if (p.CollectionStatus == CollectionStatus.Suspended && p.StatusReason is not null) return CollectionStatus.Suspended;
        var today = LicenseService.Today;
        return active.Any(m => m.License is not null && m.License.AllowsCollectionOn(today) && today >= m.PeriodFrom && today <= m.PeriodTo)
            ? CollectionStatus.Available : CollectionStatus.NotAvailable;
    }

    /// <summary>Publishes the approved mapping to Collections through the outbox. Never shown as success until the consumer acknowledges.</summary>
    internal async Task PublishAsync(Project p, CancellationToken ct, bool force = false)
    {
        var program = await db.Programs.AsNoTracking().FirstAsync(x => x.Id == p.ProgramId, ct);
        var center = await db.Centers.AsNoTracking().FirstAsync(x => x.Id == p.CenterId, ct);
        var active = await db.Mappings.AsNoTracking().Include(m => m.License).Include(m => m.FinancialItem)
            .Where(m => m.ProjectId == p.Id && m.Status == MappingStatus.Active).ToListAsync(ct);
        var item = active.FirstOrDefault()?.FinancialItem;
        var available = p.CollectionStatus == CollectionStatus.Available;
        string? reason = available ? null : p.CollectionStatus switch
        {
            CollectionStatus.Suspended => p.StatusReason ?? "موقوف",
            CollectionStatus.CapReached => "بلغ سقف التحصيل",
            CollectionStatus.Closed => "المشروع مقفل",
            _ => "لا يوجد ترخيص ساري في فترة التحصيل",
        };
        var payload = new MappingPublishedPayload(p.Id, p.ProgramId, p.CenterId, p.Code, program.Code, p.NameAr, p.NameEn, program.NameAr, program.NameEn,
            center.NameAr, center.NameEn, item?.Number ?? "", item?.NameAr ?? "", p.Visual, p.QuickAmounts, p.CollectionCap, available, reason, p.MappingVersion, DateTime.UtcNow,
            active.Select(m => new MappingLicensePayload(m.LicenseId, m.License!.Number, m.PeriodFrom, m.PeriodTo, m.License.Cap, m.LinkCap)).ToList());
        var key = $"mapping:{p.Id}:v{p.MappingVersion}:{p.CollectionStatus}:{(force ? Guid.NewGuid().ToString("N") : (p.UpdatedAt ?? p.CreatedAt).Ticks.ToString())}";
        await outbox.PublishAsync(MessageTypes.MappingPublished, payload, key, p.Code, ct);
        p.MappingSyncStatus = SyncStatus.Pending;
        p.MappingSyncError = null;
    }

    private void Transition(Project p, ApprovalStatus to, ReviewAction action, string? note)
    {
        var from = p.ApprovalStatus;
        p.ApprovalStatus = to;
        db.ProjectReviews.Add(new ProjectReviewEntry { ProjectId = p.Id, Action = action, FromStatus = from, ToStatus = to, ByUserName = user.DisplayName, Note = note });
        audit.Log("Finance", action.ToString(), nameof(Project), p.Id, new { Status = from.ToString() }, new { Status = to.ToString() }, note, p.Code);
    }

    private static void EnsureReviewable(Project p)
    {
        if (p.ApprovalStatus is not (ApprovalStatus.AwaitingFinance or ApprovalStatus.UnderFinanceReview))
            throw new DomainException("المعاملة ليست في قائمة المراجعة المالية.", "The request is not in the finance review queue.");
    }

    private async Task<Project> Load(int id, CancellationToken ct)
    {
        var p = await db.Projects.Include(x => x.Mappings).ThenInclude(m => m.License).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ProjectService.NotFound();
        if (!user.CanSeeCenter(p.CenterId)) throw new ForbiddenException(Perm.ScopeAllCenters);
        ProjectService.EnsureNotClosed(p);
        return p;
    }
}
