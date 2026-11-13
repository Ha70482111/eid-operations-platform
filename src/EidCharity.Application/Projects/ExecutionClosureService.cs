using EidCharity.Application.Common;
using EidCharity.Domain;
using EidCharity.Domain.Collections;
using EidCharity.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace EidCharity.Application.Projects;

public class ExecutionService(IEidDb db, ICurrentUser user, AuditService audit)
{
    public Task<List<ExecutionMilestone>> MilestonesAsync(int projectId, CancellationToken ct = default)
    {
        user.Require(Perm.ProjectsView);
        return db.Milestones.AsNoTracking().Where(m => m.ProjectId == projectId).OrderBy(m => m.PlannedStart).ToListAsync(ct);
    }

    public async Task SaveMilestoneAsync(ExecutionMilestone input, CancellationToken ct = default)
    {
        user.Require(Perm.ExecutionManage);
        var p = await db.Projects.FirstAsync(x => x.Id == input.ProjectId, ct);
        ProjectService.EnsureNotClosed(p);
        if (string.IsNullOrWhiteSpace(input.NameAr)) throw new DomainException("اسم النشاط مطلوب.", "Activity name is required.");
        if (input.Status == MilestoneStatus.Delayed && string.IsNullOrWhiteSpace(input.DelayReason))
            throw new DomainException("سبب التأخر مطلوب.", "Delay reason is required.");
        ExecutionMilestone m;
        if (input.Id == 0) { m = new ExecutionMilestone { ProjectId = p.Id }; db.Milestones.Add(m); }
        else m = await db.Milestones.FirstAsync(x => x.Id == input.Id, ct);
        m.NameAr = input.NameAr; m.NameEn = string.IsNullOrWhiteSpace(input.NameEn) ? input.NameAr : input.NameEn;
        m.PlannedStart = input.PlannedStart; m.PlannedEnd = input.PlannedEnd; m.ActualStart = input.ActualStart; m.ActualEnd = input.ActualEnd;
        m.Responsible = input.Responsible; m.Indicator = input.Indicator; m.TargetValue = input.TargetValue; m.ActualValue = input.ActualValue;
        m.Status = input.Status; m.DelayReason = input.DelayReason; m.CorrectiveAction = input.CorrectiveAction;
        if (p.ExecutionStatus == ExecutionStatus.NotStarted && m.Status is MilestoneStatus.InProgress or MilestoneStatus.Done) p.ExecutionStatus = ExecutionStatus.InProgress;
        audit.Log("Projects", "MilestoneSaved", nameof(ExecutionMilestone), m.Id, newValues: new { m.NameAr, m.Status, m.ActualValue }, reference: p.Code);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Progress is measured from indicators, never inferred from spending alone.</summary>
    public static decimal Progress(IEnumerable<ExecutionMilestone> ms)
    {
        var list = ms.Where(m => m.Status != MilestoneStatus.Cancelled && m.TargetValue > 0).ToList();
        return list.Count == 0 ? 0 : Math.Round(list.Average(m => Math.Min(1, m.ActualValue / m.TargetValue)) * 100, 1);
    }

    public Task<List<BeneficiaryRecord>> BeneficiariesAsync(int projectId, CancellationToken ct = default)
    {
        user.Require(Perm.ProjectsView);
        return db.Beneficiaries.AsNoTracking().Where(b => b.ProjectId == projectId).OrderByDescending(b => b.Id).ToListAsync(ct);
    }

    public async Task SaveBeneficiaryAsync(BeneficiaryRecord input, CancellationToken ct = default)
    {
        user.Require(Perm.BeneficiariesManage);
        var p = await db.Projects.FirstAsync(x => x.Id == input.ProjectId, ct);
        ProjectService.EnsureNotClosed(p);
        if (input.UniqueCount < 0 || input.Occurrences < input.UniqueCount)
            throw new DomainException("عدد مرات الاستفادة لا يقل عن عدد المستفيدين الفريدين.", "Occurrences cannot be less than unique beneficiaries.");
        BeneficiaryRecord b;
        if (input.Id == 0) { b = new BeneficiaryRecord { ProjectId = p.Id }; db.Beneficiaries.Add(b); }
        else b = await db.Beneficiaries.FirstAsync(x => x.Id == input.Id, ct);
        b.Category = input.Category; b.Unit = input.Unit; b.UniqueCount = input.UniqueCount; b.Occurrences = input.Occurrences;
        b.PeriodFrom = input.PeriodFrom; b.PeriodTo = input.PeriodTo; b.Location = input.Location; b.Notes = input.Notes;
        audit.Log("Projects", "BeneficiariesSaved", nameof(BeneficiaryRecord), b.Id, newValues: new { b.Category, b.UniqueCount, b.Occurrences }, reference: p.Code);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<DonorReport>> DonorReportsAsync(int projectId, CancellationToken ct = default)
    {
        user.Require(Perm.ProjectsView);
        return db.DonorReports.AsNoTracking().Where(r => r.ProjectId == projectId).OrderBy(r => r.DueDate).ToListAsync(ct);
    }

    public async Task SaveDonorReportAsync(DonorReport input, CancellationToken ct = default)
    {
        user.Require(Perm.ExecutionManage);
        DonorReport r;
        if (input.Id == 0) { r = new DonorReport { ProjectId = input.ProjectId }; db.DonorReports.Add(r); }
        else r = await db.DonorReports.FirstAsync(x => x.Id == input.Id, ct);
        r.Title = input.Title; r.DonorName = input.DonorName; r.DueDate = input.DueDate; r.Responsible = input.Responsible; r.Status = input.Status;
        await db.SaveChangesAsync(ct);
    }
}

public record ClosureCheck(string KeyAr, string KeyEn, bool Passed, string? DetailAr = null);

public class ClosureService(IEidDb db, ICurrentUser user, AuditService audit, FinanceReviewService finance)
{
    /// <summary>Automatic closing conditions (analysis §6.5) plus the documented manual confirmations.</summary>
    public async Task<List<ClosureCheck>> ChecksAsync(int projectId, CancellationToken ct = default)
    {
        user.Require(Perm.ProjectsView);
        var p = await db.Projects.AsNoTracking().FirstAsync(x => x.Id == projectId, ct);
        var c = await db.Closures.AsNoTracking().FirstOrDefaultAsync(x => x.ProjectId == projectId, ct) ?? new ProjectClosure();
        var openRequests = await db.Disbursements.CountAsync(d => d.ProjectId == projectId && (d.Status == DisbursementStatus.Submitted || d.Status == DisbursementStatus.UnderReview
                                       || d.Status == DisbursementStatus.ReturnedForEdit || d.Status == DisbursementStatus.Draft), ct);
        var reserves = await db.Disbursements.Where(d => d.ProjectId == projectId).SumAsync(d => (decimal?)d.ReservedAmount, ct) ?? 0;
        var openAdvances = await db.Disbursements.CountAsync(d => d.ProjectId == projectId && d.IsAdvance && d.Status == DisbursementStatus.PartiallyExecuted, ct);
        var pendingCollections = await db.DonationLines.Join(db.Donations, l => l.DonationId, d => d.Id, (l, d) => new { l.ProjectId, d.Status })
            .CountAsync(x => x.ProjectId == projectId && x.Status == DonationStatus.PendingVerification, ct);
        var openInstallments = await db.AgreementLines.Where(a => a.ProjectId == projectId)
            .Join(db.Agreements, l => l.AgreementId, a => a.Id, (l, a) => a).Where(a => a.Status == AgreementStatus.Active).CountAsync(ct);
        var pendingRefunds = await db.Refunds.Join(db.DonationLines, r => r.DonationId, l => l.DonationId, (r, l) => new { r.Status, l.ProjectId })
            .CountAsync(x => x.ProjectId == projectId && (x.Status == RequestStatus.Submitted || x.Status == RequestStatus.Approved), ct);
        var cancelledOrCompleted = p.ExecutionStatus is ExecutionStatus.Completed or ExecutionStatus.Cancelled;
        return
        [
            new("اكتمال التنفيذ أو قرار الإلغاء موثق", "Execution completed or cancellation decided", cancelledOrCompleted && c.ExecutionDocumented),
            new("لا توجد طلبات صرف مفتوحة", "No open disbursement requests", openRequests == 0, openRequests > 0 ? $"{openRequests} طلبات" : null),
            new("لا توجد حجوزات غير لازمة", "No outstanding reserves", reserves == 0, reserves > 0 ? $"{reserves:N2}" : null),
            new("العهد والسلف مسواة", "Advances settled", openAdvances == 0 && c.ExpensesSettled),
            new("لا توجد عمليات تحصيل معلقة", "No pending collections", pendingCollections == 0, pendingCollections > 0 ? $"{pendingCollections} عمليات" : null),
            new("تم تحديد مصير التعهدات والدفعات غير المحصلة", "Uncollected pledges/installments resolved", openInstallments == 0 || c.PledgesResolved),
            new("لا توجد استردادات قيد الإجراء", "No refunds in progress", pendingRefunds == 0),
            new("تسوية الفروق والمرتجعات وتوثيق الرصيد المتبقي", "Differences settled, balance treatment documented", c.DifferencesSettled && !string.IsNullOrWhiteSpace(c.BalanceTreatment)),
            new("التقرير الختامي معتمد", "Final report approved", c.FinalReportApproved),
        ];
    }

    public async Task<ProjectClosure> GetAsync(int projectId, CancellationToken ct = default) =>
        await db.Closures.AsNoTracking().FirstOrDefaultAsync(x => x.ProjectId == projectId, ct) ?? new ProjectClosure { ProjectId = projectId };

    public async Task SaveChecklistAsync(ProjectClosure input, CancellationToken ct = default)
    {
        user.Require(Perm.ProjectClose);
        var c = await db.Closures.FirstOrDefaultAsync(x => x.ProjectId == input.ProjectId, ct);
        if (c is null) { c = new ProjectClosure { ProjectId = input.ProjectId }; db.Closures.Add(c); }
        c.ExecutionDocumented = input.ExecutionDocumented; c.ExpensesSettled = input.ExpensesSettled; c.OpenRequestsResolved = input.OpenRequestsResolved;
        c.PledgesResolved = input.PledgesResolved; c.DifferencesSettled = input.DifferencesSettled; c.FinalReportApproved = input.FinalReportApproved;
        c.BalanceTreatment = input.BalanceTreatment; c.Notes = input.Notes;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetExecutionStatusAsync(int projectId, ExecutionStatus status, string reason, CancellationToken ct = default)
    {
        if (status is ExecutionStatus.Stopped or ExecutionStatus.Cancelled) user.Require(Perm.ProjectSuspend); else user.Require(Perm.ExecutionManage);
        if (status == ExecutionStatus.Closed) throw new DomainException("الإقفال يتم من شاشة الإقفال بعد التحقق من الشروط.", "Closing is done from the closure screen after checks.");
        if (status is ExecutionStatus.Stopped or ExecutionStatus.Cancelled && string.IsNullOrWhiteSpace(reason))
            throw new DomainException("السبب ونطاق الإيقاف وآثاره مطلوبة.", "Reason, scope and impact are required.");
        var p = await db.Projects.Include(x => x.Mappings).ThenInclude(m => m.License).FirstAsync(x => x.Id == projectId, ct);
        ProjectService.EnsureNotClosed(p);
        var old = p.ExecutionStatus;
        p.ExecutionStatus = status;
        if (status is ExecutionStatus.Stopped or ExecutionStatus.Cancelled)
        {
            p.CollectionStatus = CollectionStatus.Suspended; p.StatusReason = reason;
            db.ProjectReviews.Add(new ProjectReviewEntry { ProjectId = p.Id, Action = status == ExecutionStatus.Cancelled ? ReviewAction.Cancelled : ReviewAction.Suspended,
                FromStatus = p.ApprovalStatus, ToStatus = p.ApprovalStatus, ByUserName = user.DisplayName, Note = reason });
        }
        audit.Log("Projects", "ExecutionStatusChanged", nameof(Project), p.Id, new { old }, new { status }, reason, p.Code);
        await db.SaveChangesAsync(ct);
        if (p.ApprovalStatus == ApprovalStatus.FinanciallyApproved) { await finance.PublishAsync(p, ct); await db.SaveChangesAsync(ct); }
    }

    public async Task CloseAsync(int projectId, CancellationToken ct = default)
    {
        user.Require(Perm.ProjectClose);
        var checks = await ChecksAsync(projectId, ct);
        var failed = checks.Where(c => !c.Passed).ToList();
        if (failed.Count > 0)
            throw new DomainException("شروط الإقفال غير مكتملة: " + string.Join("، ", failed.Select(f => f.KeyAr)), "Closing conditions not met: " + string.Join(", ", failed.Select(f => f.KeyEn)));
        var p = await db.Projects.FirstAsync(x => x.Id == projectId, ct);
        var c = await db.Closures.FirstAsync(x => x.ProjectId == projectId, ct);
        c.IsClosed = true;
        p.ExecutionStatus = ExecutionStatus.Closed; p.CollectionStatus = CollectionStatus.Closed; p.ClosedAt = DateTime.UtcNow;
        db.ProjectReviews.Add(new ProjectReviewEntry { ProjectId = p.Id, Action = ReviewAction.Closed, FromStatus = p.ApprovalStatus, ToStatus = p.ApprovalStatus, ByUserName = user.DisplayName });
        audit.Log("Projects", "ProjectClosed", nameof(Project), p.Id, reference: p.Code);
        await db.SaveChangesAsync(ct);
        await finance.PublishAsync(p, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Reopening is an exception: permission, reason, approval and log. Eligibility of every new movement is re-checked.</summary>
    public async Task ReopenAsync(int projectId, string reason, CancellationToken ct = default)
    {
        user.Require(Perm.ProjectReopen);
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("سبب إعادة الفتح مطلوب.", "Reason is required.");
        var p = await db.Projects.FirstAsync(x => x.Id == projectId, ct);
        if (!p.IsClosed) throw new DomainException("المشروع غير مقفل.", "Project is not closed.");
        var c = await db.Closures.FirstAsync(x => x.ProjectId == projectId, ct);
        c.IsClosed = false;
        p.ExecutionStatus = ExecutionStatus.InProgress; p.ClosedAt = null;
        p.CollectionStatus = CollectionStatus.Suspended; p.StatusReason = "أعيد فتحه — يلزم إعادة اعتماد الإتاحة";
        p.ApprovalStatus = ApprovalStatus.AwaitingFinance;
        db.ProjectReviews.Add(new ProjectReviewEntry { ProjectId = p.Id, Action = ReviewAction.Reopened, FromStatus = ApprovalStatus.FinanciallyApproved, ToStatus = p.ApprovalStatus, ByUserName = user.DisplayName, Note = reason });
        audit.Log("Projects", "ProjectReopened", nameof(Project), p.Id, reason: reason, reference: p.Code);
        await db.SaveChangesAsync(ct);
    }
}

public record ProjectsDashboard(int ActivePrograms, int ActiveProjects, int AwaitingFinance, int Returned, decimal Reserved, decimal Executed, decimal ConfirmedNet,
    int LicensesExpiring, int SyncFailures, List<LicenseRow> LicenseAlerts, List<ProjectRow> Inbox);

public record ProjectReportRow(ProjectRow Project, ProjectFinancials Financials);

public class ProjectsDashboardService(IEidDb db, ICurrentUser user, LicenseService licenses, SettingsService settings, FinancialsService fin)
{
    /// <summary>P22: per-project financial position (revenue from Collections only, budget vs reserved vs executed).</summary>
    public async Task<List<ProjectReportRow>> FinancialReportAsync(int? programId, CancellationToken ct = default)
    {
        user.Require(Perm.ProjectsReports);
        var q = db.Projects.AsNoTracking().ScopeToCenter(user, p => p.CenterId);
        if (programId is not null) q = q.Where(p => p.ProgramId == programId);
        var rows = await q.OrderBy(p => p.Code).Take(500).Select(ProjectService.ToRow()).ToListAsync(ct);
        var result = new List<ProjectReportRow>(rows.Count);
        foreach (var r in rows) result.Add(new ProjectReportRow(r, await fin.ForProjectAsync(r.Id, ct)));
        return result;
    }

    public async Task<ProjectsDashboard> GetAsync(CancellationToken ct = default)
    {
        user.Require(Perm.ProjectsView);
        var projects = db.Projects.AsNoTracking().ScopeToCenter(user, p => p.CenterId);
        var ids = projects.Select(p => p.Id);
        var alertDays = await settings.GetIntAsync(SettingKeys.LicenseExpiryAlertDays, 30, ct);
        var lics = user.Has(Perm.LicensesView) ? await licenses.ListAsync(ct: ct) : [];
        var expiring = lics.Where(l => l.Status == LicenseStatus.Active && l.DaysToExpiry <= alertDays).OrderBy(l => l.DaysToExpiry).ToList();
        return new ProjectsDashboard(
            await db.Programs.ScopeToCenter(user, p => p.CenterId).CountAsync(p => !p.IsClosed, ct),
            await projects.CountAsync(p => p.ExecutionStatus != ExecutionStatus.Closed, ct),
            await projects.CountAsync(p => p.ApprovalStatus == ApprovalStatus.AwaitingFinance || p.ApprovalStatus == ApprovalStatus.UnderFinanceReview, ct),
            await projects.CountAsync(p => p.ApprovalStatus == ApprovalStatus.ReturnedToCenter, ct),
            await db.Disbursements.Where(d => ids.Contains(d.ProjectId)).SumAsync(d => (decimal?)d.ReservedAmount, ct) ?? 0,
            await db.Disbursements.Where(d => ids.Contains(d.ProjectId)).SumAsync(d => (decimal?)d.ExecutedAmount, ct) ?? 0,
            await db.CollectionResults.Where(c => ids.Contains(c.ProjectId) && (c.Kind == CollectionResultKind.Confirmed || c.Kind == CollectionResultKind.Refund || c.Kind == CollectionResultKind.Correction
                || c.Kind == CollectionResultKind.ReallocationIn || c.Kind == CollectionResultKind.ReallocationOut)).SumAsync(c => (decimal?)c.Amount, ct) ?? 0,
            expiring.Count,
            await projects.CountAsync(p => p.MappingSyncStatus == SyncStatus.Failed, ct),
            expiring.Take(5).ToList(),
            await projects.Where(p => p.ApprovalStatus == ApprovalStatus.AwaitingFinance || p.ApprovalStatus == ApprovalStatus.UnderFinanceReview || p.ApprovalStatus == ApprovalStatus.ReturnedToCenter
                                      || p.MappingSyncStatus == SyncStatus.Failed)
                .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt).Take(6).Select(ProjectService.ToRow()).ToListAsync(ct));
    }
}
