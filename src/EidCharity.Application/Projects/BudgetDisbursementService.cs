using EidCharity.Application.Common;
using EidCharity.Domain;
using EidCharity.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace EidCharity.Application.Projects;

/// <summary>Separate financial values (analysis §4.3) — never merged into one number.</summary>
public record ProjectFinancials(
    decimal ExecutionCost, decimal CollectionCap, decimal Pledged, decimal Pending, decimal ConfirmedGross, decimal Refunds,
    decimal ReallocatedNet, decimal ConfirmedNet, decimal InKindEstimated, decimal Reserved, decimal Executed, decimal BudgetTotal, string Currency)
{
    /// <summary>Eligible funding available to spend = confirmed net − executed − reserved.</summary>
    public decimal AvailableToSpend => ConfirmedNet - Executed - Reserved;
    /// <summary>Budget remaining is monitored independently.</summary>
    public decimal BudgetRemaining => BudgetTotal - Executed - Reserved;
    public decimal CapRemaining => CollectionCap - ConfirmedNet - Pending;
}

public class FinancialsService(IEidDb db)
{
    public async Task<ProjectFinancials> ForProjectAsync(int projectId, CancellationToken ct = default)
    {
        var p = await db.Projects.AsNoTracking().FirstAsync(x => x.Id == projectId, ct);
        var results = await db.CollectionResults.Where(c => c.ProjectId == projectId).GroupBy(c => c.Kind)
            .Select(g => new { g.Key, Sum = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
        decimal R(CollectionResultKind k) => results.GetValueOrDefault(k);
        var disb = await db.Disbursements.Where(d => d.ProjectId == projectId)
            .GroupBy(_ => 1).Select(g => new { Res = g.Sum(d => d.ReservedAmount), Exe = g.Sum(d => d.ExecutedAmount) }).FirstOrDefaultAsync(ct);
        var budget = await db.BudgetLines.Where(b => b.ProjectId == projectId).SumAsync(b => (decimal?)b.Amount, ct) ?? 0;
        var confirmedGross = R(CollectionResultKind.Confirmed);
        var refunds = R(CollectionResultKind.Refund) + R(CollectionResultKind.Correction); // negative values
        var realloc = R(CollectionResultKind.ReallocationIn) + R(CollectionResultKind.ReallocationOut);
        return new ProjectFinancials(p.ExecutionCost, p.CollectionCap, R(CollectionResultKind.Pledge),
            R(CollectionResultKind.Pending) + R(CollectionResultKind.PendingCancelled), confirmedGross, refunds, realloc,
            confirmedGross + refunds + realloc, R(CollectionResultKind.InKind), disb?.Res ?? 0, disb?.Exe ?? 0, budget, p.Currency);
    }

    public async Task<Dictionary<int, decimal>> BudgetLineUsageAsync(int projectId, CancellationToken ct = default) =>
        await db.Disbursements.Where(d => d.ProjectId == projectId).GroupBy(d => d.BudgetLineId)
            .Select(g => new { g.Key, Used = g.Sum(d => d.ReservedAmount + d.ExecutedAmount) }).ToDictionaryAsync(x => x.Key, x => x.Used, ct);
}

public class BudgetService(IEidDb db, ICurrentUser user, AuditService audit)
{
    public async Task<List<BudgetLine>> LinesAsync(int projectId, CancellationToken ct = default)
    {
        user.Require(Perm.BudgetView);
        return await db.BudgetLines.AsNoTracking().Where(b => b.ProjectId == projectId).OrderBy(b => b.Code).ToListAsync(ct);
    }

    public async Task SaveLineAsync(BudgetLine input, CancellationToken ct = default)
    {
        user.Require(Perm.BudgetManage);
        var p = await db.Projects.FirstAsync(x => x.Id == input.ProjectId, ct);
        ProjectService.EnsureNotClosed(p);
        if (p.ApprovalStatus == ApprovalStatus.FinanciallyApproved && input.Id != 0)
            throw new DomainException("بعد الاعتماد يتم تعديل الميزانية عبر طلب تعديل معتمد.", "After approval, budget changes go through an approved adjustment.");
        if (input.Amount < 0 || string.IsNullOrWhiteSpace(input.NameAr)) throw new DomainException("بيانات البند غير مكتملة.", "Budget line data incomplete.");
        BudgetLine line;
        if (input.Id == 0)
        {
            line = new BudgetLine { ProjectId = p.Id, Code = $"{p.Code}-B{await db.BudgetLines.CountAsync(b => b.ProjectId == p.Id, ct) + 1:00}" };
            db.BudgetLines.Add(line);
        }
        else line = await db.BudgetLines.FirstAsync(b => b.Id == input.Id, ct);
        line.NameAr = input.NameAr; line.NameEn = string.IsNullOrWhiteSpace(input.NameEn) ? input.NameAr : input.NameEn; line.Category = input.Category; line.Amount = input.Amount;
        var total = await db.BudgetLines.Where(b => b.ProjectId == p.Id && b.Id != line.Id).SumAsync(b => b.Amount, ct) + line.Amount;
        if (p.ExecutionCost > 0 && total > p.ExecutionCost)
            throw new DomainException($"مجموع بنود الميزانية ({total:N2}) يتجاوز تكلفة التنفيذ المعتمدة.", $"Budget lines total ({total:N2}) exceeds execution cost.");
        audit.Log("Projects", "BudgetLineSaved", nameof(BudgetLine), line.Id, newValues: new { line.Code, line.NameAr, line.Amount }, reference: p.Code);
        await db.SaveChangesAsync(ct);
    }

    public async Task RequestAdjustmentAsync(BudgetAdjustment a, CancellationToken ct = default)
    {
        user.Require(Perm.BudgetManage);
        if (a.Amount <= 0 || string.IsNullOrWhiteSpace(a.Reason)) throw new DomainException("المبلغ والسبب مطلوبان.", "Amount and reason are required.");
        a.Status = AdjustmentStatus.Submitted;
        db.BudgetAdjustments.Add(a);
        audit.Log("Projects", "BudgetAdjustmentRequested", nameof(BudgetAdjustment), a.ProjectId, null, new { a.FromLineId, a.ToLineId, a.Amount }, a.Reason);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<BudgetAdjustment>> AdjustmentsAsync(int projectId, CancellationToken ct = default) =>
        db.BudgetAdjustments.AsNoTracking().Where(a => a.ProjectId == projectId).OrderByDescending(a => a.Id).ToListAsync(ct);

    /// <summary>Budget transfer has its own approval path and never changes a donor's restriction automatically.</summary>
    public async Task DecideAdjustmentAsync(int id, bool approve, string? note, FinancialsService fin, CancellationToken ct = default)
    {
        user.Require(Perm.BudgetApproveAdjustment);
        var a = await db.BudgetAdjustments.FirstAsync(x => x.Id == id, ct);
        ApprovalMatrixService.EnsureNotSelf(a.CreatedById, user.UserId);
        if (a.Status != AdjustmentStatus.Submitted) throw new DomainException("تم البت في الطلب.", "Already decided.");
        if (approve)
        {
            var usage = await fin.BudgetLineUsageAsync(a.ProjectId, ct);
            if (a.FromLineId is int from)
            {
                var fl = await db.BudgetLines.FirstAsync(b => b.Id == from, ct);
                if (fl.Amount - usage.GetValueOrDefault(from) < a.Amount)
                    throw new DomainException("المتبقي في البند المصدر لا يكفي.", "Insufficient remaining in source line.");
                fl.Amount -= a.Amount;
            }
            if (a.ToLineId is int to) (await db.BudgetLines.FirstAsync(b => b.Id == to, ct)).Amount += a.Amount;
        }
        a.Status = approve ? AdjustmentStatus.Approved : AdjustmentStatus.Rejected;
        a.DecidedById = user.UserId; a.DecisionNote = note;
        audit.Log("Projects", approve ? "BudgetAdjustmentApproved" : "BudgetAdjustmentRejected", nameof(BudgetAdjustment), a.Id, reason: note);
        await db.SaveChangesAsync(ct);
    }
}

public class DisbursementInput
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int BudgetLineId { get; set; }
    public string Purpose { get; set; } = "";
    public string PayeeName { get; set; } = "";
    public string PayeeDetails { get; set; } = "";
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "BankTransfer";
    public string ExpenseType { get; set; } = "Operational";
    public bool IsAdvance { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? InvoiceReference { get; set; }
}

public record DisbursementRow(int Id, string Number, string ProjectCode, string ProjectNameAr, string ProjectNameEn, string PayeeName, decimal Amount,
    decimal Reserved, decimal Executed, string Currency, DisbursementStatus Status, DateTime CreatedAt, bool IsAdvance);

/// <summary>
/// Draft → submit → specialist review → finance funding check → approval (reserve) → execution (partial/full) → release.
/// </summary>
public class DisbursementService(IEidDb db, ICurrentUser user, INumberGenerator numbers, AuditService audit, FinancialsService fin,
    ApprovalMatrixService matrix, IResourceLock locks, NotificationService notify)
{
    public async Task<List<DisbursementRow>> ListAsync(DisbursementStatus? status, int? projectId, CancellationToken ct = default)
    {
        user.RequireAny(Perm.BudgetView, Perm.DisbReview, Perm.DisbFinanceCheck, Perm.DisbApprove, Perm.DisbExecute);
        var q = db.Disbursements.AsNoTracking().ScopeToCenter(user, d => d.Project!.CenterId);
        if (status is not null) q = q.Where(d => d.Status == status);
        if (projectId is not null) q = q.Where(d => d.ProjectId == projectId);
        return await q.OrderByDescending(d => d.Id).Select(d => new DisbursementRow(d.Id, d.Number, d.Project!.Code, d.Project.NameAr, d.Project.NameEn, d.PayeeName,
            d.Amount, d.ReservedAmount, d.ExecutedAmount, d.Currency, d.Status, d.CreatedAt, d.IsAdvance)).ToListAsync(ct);
    }

    public async Task<DisbursementRequest> GetAsync(int id, CancellationToken ct = default)
    {
        user.RequireAny(Perm.BudgetView, Perm.DisbReview, Perm.DisbFinanceCheck, Perm.DisbApprove, Perm.DisbExecute);
        var d = await db.Disbursements.Include(x => x.Project).Include(x => x.BudgetLine).Include(x => x.Actions.OrderBy(a => a.Id)).Include(x => x.Payments)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ProjectService.NotFound();
        if (!user.CanSeeCenter(d.Project!.CenterId)) throw new ForbiddenException(Perm.ScopeAllCenters);
        return d;
    }

    public async Task<int> SaveDraftAsync(DisbursementInput i, CancellationToken ct = default)
    {
        user.Require(Perm.DisbCreate);
        var p = await db.Projects.FirstAsync(x => x.Id == i.ProjectId, ct);
        ProjectService.EnsureNotClosed(p);
        if (!user.CanSeeCenter(p.CenterId)) throw new ForbiddenException(Perm.ScopeAllCenters);
        if (!await db.BudgetLines.AnyAsync(b => b.Id == i.BudgetLineId && b.ProjectId == p.Id, ct))
            throw new DomainException("اختر بند ميزانية تابعًا للمشروع.", "Choose a budget line of this project.");
        if (i.Amount <= 0 || string.IsNullOrWhiteSpace(i.PayeeName) || string.IsNullOrWhiteSpace(i.Purpose))
            throw new DomainException("الغرض والمستفيد من الدفع والمبلغ مطلوبة.", "Purpose, payee and amount are required.");
        DisbursementRequest d;
        if (i.Id == 0)
        {
            d = new DisbursementRequest { Number = await numbers.NextAsync("disbursement", "PAY", 5, true, ct), RequestedById = user.UserId!, Currency = p.Currency };
            db.Disbursements.Add(d);
        }
        else
        {
            d = await db.Disbursements.FirstAsync(x => x.Id == i.Id, ct);
            if (d.Status is not (DisbursementStatus.Draft or DisbursementStatus.ReturnedForEdit))
                throw new DomainException("لا يمكن تعديل الطلب في حالته الحالية.", "Request cannot be edited in its current state.");
        }
        d.ProjectId = i.ProjectId; d.BudgetLineId = i.BudgetLineId; d.Purpose = i.Purpose; d.PayeeName = i.PayeeName; d.PayeeDetails = i.PayeeDetails;
        d.Amount = Money.Round(i.Amount); d.PaymentMethod = i.PaymentMethod; d.ExpenseType = i.ExpenseType; d.IsAdvance = i.IsAdvance; d.DueDate = i.DueDate; d.InvoiceReference = i.InvoiceReference;
        await db.SaveChangesAsync(ct);
        return d.Id;
    }

    public async Task SubmitAsync(int id, CancellationToken ct = default)
    {
        user.Require(Perm.DisbCreate);
        var d = await Load(id, ct);
        if (d.Status is not (DisbursementStatus.Draft or DisbursementStatus.ReturnedForEdit)) throw Invalid();
        d.Status = DisbursementStatus.Submitted; d.SpecialistReviewed = false; d.FinanceChecked = false;
        Act(d, DisbursementStep.SpecialistReview, "Submitted", null);
        await notify.NotifyAsync($"طلب صرف جديد {d.Number}", $"New disbursement {d.Number}", $"/projects/disbursements/{d.Id}", Perm.DisbReview, ct: ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Step 2: specialist checks purpose, documents, budget line and restriction.</summary>
    public async Task SpecialistReviewAsync(int id, string decision, string? note, CancellationToken ct = default)
    {
        user.Require(Perm.DisbReview);
        var d = await Load(id, ct);
        if (d.Status != DisbursementStatus.Submitted) throw Invalid();
        ApprovalMatrixService.EnsureNotSelf(d.RequestedById, user.UserId);
        ApplyDecision(d, decision, note, DisbursementStep.SpecialistReview);
        if (decision == "approve") { d.SpecialistReviewed = true; d.Status = DisbursementStatus.UnderReview; }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Step 3: finance verifies funding, liquidity, reserves and duplicates.</summary>
    public async Task FinanceCheckAsync(int id, string decision, string? note, CancellationToken ct = default)
    {
        user.Require(Perm.DisbFinanceCheck);
        var d = await Load(id, ct);
        if (d.Status != DisbursementStatus.UnderReview || !d.SpecialistReviewed) throw Invalid();
        ApprovalMatrixService.EnsureNotSelf(d.RequestedById, user.UserId);
        if (decision == "approve")
        {
            await EnsureFundingAsync(d, ct);
            await EnsureNoDuplicateAsync(d, ct);
            d.FinanceChecked = true;
        }
        ApplyDecision(d, decision, note, DisbursementStep.FinanceCheck);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<DisbursementRequest>> SimilarAsync(DisbursementRequest d, CancellationToken ct = default) =>
        await db.Disbursements.AsNoTracking().Where(x => x.Id != d.Id && x.Status != DisbursementStatus.Cancelled && x.Status != DisbursementStatus.Rejected
            && x.PayeeName == d.PayeeName && (x.Amount == d.Amount || (d.InvoiceReference != null && x.InvoiceReference == d.InvoiceReference))).ToListAsync(ct);

    /// <summary>Step 4–5: approval by the authorised level (matrix) — reserves the amount without recording an expense.</summary>
    public async Task ApproveAsync(int id, string? note, CancellationToken ct = default)
    {
        user.Require(Perm.DisbApprove);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var d = await Load(id, ct);
        await locks.AcquireAsync($"project-funds:{d.ProjectId}", ct);
        if (d.Status != DisbursementStatus.UnderReview || !d.FinanceChecked) throw Invalid();
        ApprovalMatrixService.EnsureNotSelf(d.RequestedById, user.UserId);
        await matrix.EnsureWithinLimitAsync(ApprovalModule.Disbursement, d.Amount, d.Project!.CenterId, d.ExpenseType, ct);
        await EnsureFundingAsync(d, ct); // re-check under lock: concurrent approvals cannot exceed availability
        d.Status = DisbursementStatus.ApprovedReserved;
        d.ReservedAmount = d.Amount;
        d.ApprovedById = user.UserId;
        Act(d, DisbursementStep.Approval, "Approved", note);
        audit.Log("Projects", "DisbursementApproved", nameof(DisbursementRequest), d.Id, newValues: new { d.Amount, Reserved = d.ReservedAmount }, reference: d.Number);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Step 6–7: execution. Executed part moves from reserve to expense; remainder stays reserved or is released.</summary>
    public async Task ExecuteAsync(int id, decimal amount, DateOnly paidOn, string reference, string? document, CancellationToken ct = default)
    {
        user.Require(Perm.DisbExecute);
        if (amount <= 0 || string.IsNullOrWhiteSpace(reference)) throw new DomainException("المبلغ ومرجع الدفع مطلوبان.", "Amount and payment reference are required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var d = await Load(id, ct);
        await locks.AcquireAsync($"project-funds:{d.ProjectId}", ct);
        if (d.Status is not (DisbursementStatus.ApprovedReserved or DisbursementStatus.PartiallyExecuted)) throw Invalid();
        if (amount > d.ReservedAmount) throw new DomainException("المبلغ أكبر من المحجوز المتبقي.", "Amount exceeds remaining reserve.");
        if (await db.DisbursementPayments.AnyAsync(x => x.Reference == reference, ct))
            throw new DomainException("مرجع الدفع مستخدم من قبل — منع ازدواجية الصرف.", "Payment reference already used — duplicate prevented.");
        d.Payments.Add(new DisbursementPayment { RequestId = d.Id, Amount = amount, PaidOn = paidOn, Reference = reference, DocumentReference = document });
        d.ReservedAmount -= amount;
        d.ExecutedAmount += amount;
        d.Status = d.ReservedAmount == 0 ? DisbursementStatus.FullyExecuted : DisbursementStatus.PartiallyExecuted;
        Act(d, DisbursementStep.Execution, d.Status.ToString(), $"{amount:N2} — {reference}");
        audit.Log("Projects", "DisbursementExecuted", nameof(DisbursementRequest), d.Id, newValues: new { amount, reference, d.ReservedAmount, d.ExecutedAmount }, reference: d.Number);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Releases the unexecuted remainder by a documented decision.</summary>
    public async Task ReleaseRemainderAsync(int id, string reason, CancellationToken ct = default)
    {
        user.Require(Perm.DisbCancel);
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("سبب التحرير مطلوب.", "Reason is required.");
        var d = await Load(id, ct);
        if (d.ReservedAmount <= 0) throw Invalid();
        var released = d.ReservedAmount;
        d.ReleasedAmount += released; d.ReservedAmount = 0;
        d.Status = d.ExecutedAmount > 0 ? DisbursementStatus.FullyExecuted : DisbursementStatus.Cancelled;
        Act(d, DisbursementStep.Release, "Released", $"{released:N2} — {reason}");
        audit.Log("Projects", "ReserveReleased", nameof(DisbursementRequest), d.Id, null, new { released }, reason, d.Number);
        await db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(int id, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("سبب الإلغاء مطلوب.", "Reason is required.");
        var d = await Load(id, ct);
        if (d.Status is DisbursementStatus.Draft or DisbursementStatus.ReturnedForEdit) user.Require(Perm.DisbCreate);
        else user.Require(Perm.DisbCancel);
        if (d.ExecutedAmount > 0 || d.Status is DisbursementStatus.Cancelled or DisbursementStatus.FullyExecuted) throw Invalid();
        d.ReleasedAmount += d.ReservedAmount; d.ReservedAmount = 0;
        d.Status = DisbursementStatus.Cancelled;
        Act(d, DisbursementStep.Cancellation, "Cancelled", reason);
        audit.Log("Projects", "DisbursementCancelled", nameof(DisbursementRequest), d.Id, reason: reason, reference: d.Number);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureFundingAsync(DisbursementRequest d, CancellationToken ct)
    {
        var f = await fin.ForProjectAsync(d.ProjectId, ct);
        var alreadyReservedForThis = d.ReservedAmount;
        // Uncollected pledges never fund expenses automatically: only confirmed net counts.
        if (f.AvailableToSpend + alreadyReservedForThis < d.Amount)
            throw new DomainException($"التمويل المؤهل المتاح ({f.AvailableToSpend:N2}) لا يغطي المبلغ. التعهدات غير المحصلة لا تمول الصرف.",
                $"Eligible available funding ({f.AvailableToSpend:N2}) does not cover the amount. Uncollected pledges do not fund expenses.");
        var line = await db.BudgetLines.FirstAsync(b => b.Id == d.BudgetLineId, ct);
        var used = (await fin.BudgetLineUsageAsync(d.ProjectId, ct)).GetValueOrDefault(d.BudgetLineId) - alreadyReservedForThis;
        if (line.Amount - used < d.Amount)
            throw new DomainException($"المتبقي في بند الميزانية «{line.NameAr}» ({line.Amount - used:N2}) لا يكفي.", $"Budget line remaining ({line.Amount - used:N2}) is insufficient.");
    }

    private async Task EnsureNoDuplicateAsync(DisbursementRequest d, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(d.InvoiceReference) && await db.Disbursements.AnyAsync(x => x.Id != d.Id && x.InvoiceReference == d.InvoiceReference
                && x.PayeeName == d.PayeeName && x.Status != DisbursementStatus.Cancelled && x.Status != DisbursementStatus.Rejected, ct))
            throw new DomainException("يوجد طلب آخر لنفس المورد ونفس المستند — احتمال ازدواجية صرف.", "Another request exists for the same payee and document — possible duplicate.");
    }

    private void ApplyDecision(DisbursementRequest d, string decision, string? note, DisbursementStep step)
    {
        switch (decision)
        {
            case "approve": Act(d, step, "Approved", note); break;
            case "return":
                if (string.IsNullOrWhiteSpace(note)) throw new DomainException("اكتب سبب الإعادة.", "Write the return reason.");
                d.Status = DisbursementStatus.ReturnedForEdit; Act(d, step, "Returned", note); break;
            case "reject":
                if (string.IsNullOrWhiteSpace(note)) throw new DomainException("اكتب سبب الرفض.", "Write the rejection reason.");
                d.Status = DisbursementStatus.Rejected; Act(d, step, "Rejected", note); break;
            default: throw Invalid();
        }
    }

    private void Act(DisbursementRequest d, DisbursementStep step, string decision, string? note) =>
        d.Actions.Add(new DisbursementAction { RequestId = d.Id, Step = step, Decision = decision, Note = note, ByUserName = user.DisplayName });

    private async Task<DisbursementRequest> Load(int id, CancellationToken ct)
    {
        var d = await db.Disbursements.Include(x => x.Project).Include(x => x.Actions).Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ProjectService.NotFound();
        if (!user.CanSeeCenter(d.Project!.CenterId)) throw new ForbiddenException(Perm.ScopeAllCenters);
        if (d.Project.IsClosed) ProjectService.EnsureNotClosed(d.Project);
        return d;
    }

    private static DomainException Invalid() => new("الإجراء غير متاح في حالة الطلب الحالية.", "Action not available in the current request state.");
}
