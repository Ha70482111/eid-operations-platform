using EidCharity.Application.Common;
using EidCharity.Application.Integration;
using EidCharity.Application.Projects;
using EidCharity.Domain;
using EidCharity.Domain.Collections;
using EidCharity.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace EidCharity.Application.Collections;

/// <summary>Refunds, chargebacks, reallocations and receipt corrections. The original is always kept and linked.</summary>
public class AdjustmentService(IEidDb db, ICurrentUser user, INumberGenerator numbers, AuditService audit, ApprovalMatrixService matrix,
    DonationService donations, EligibilityGateway gateway, ProjectStatusRefresher refresher, FinancialsService fin)
{
    // ---------------- Refunds ----------------
    public async Task<List<RefundRequest>> RefundsAsync(CancellationToken ct = default)
    {
        user.RequireAny(Perm.RefundsRequest, Perm.RefundsApprove, Perm.RefundsExecute);
        return await db.Refunds.AsNoTracking().Include(r => r.Donation).ThenInclude(d => d!.Donor).OrderByDescending(r => r.Id).ToListAsync(ct);
    }

    public async Task<int> RequestRefundAsync(int donationId, decimal amount, string reason, bool chargeback, CancellationToken ct = default)
    {
        user.Require(Perm.RefundsRequest);
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("سبب الاسترداد مطلوب.", "Reason is required.");
        var d = await db.Donations.FirstAsync(x => x.Id == donationId, ct);
        var inFlight = await db.Refunds.Where(r => r.DonationId == donationId && (r.Status == RequestStatus.Submitted || r.Status == RequestStatus.UnderReview || r.Status == RequestStatus.Approved))
            .SumAsync(r => (decimal?)r.Amount, ct) ?? 0;
        if (amount <= 0 || amount > d.RefundableAmount - inFlight)
            throw new DomainException($"المسترد لا يزيد عن المتبقي القابل للاسترداد ({d.RefundableAmount - inFlight:N2}).", $"Refund cannot exceed refundable remainder ({d.RefundableAmount - inFlight:N2}).");
        var r = new RefundRequest { Number = await numbers.NextAsync("refund", "RFD", 5, true, ct), DonationId = d.Id, Amount = amount, Reason = reason, IsChargeback = chargeback, RequestedById = user.UserId! };
        db.Refunds.Add(r);
        await db.SaveChangesAsync(ct);
        audit.Log("Collections", "RefundRequested", nameof(RefundRequest), r.Id, null, new { d.Number, amount, chargeback }, reason, r.Number);
        await db.SaveChangesAsync(ct);
        return r.Id;
    }

    /// <summary>Review considers donor terms and the effect on spending and reserves before approval.</summary>
    public async Task<string?> RefundImpactAsync(int refundId, CancellationToken ct = default)
    {
        var r = await db.Refunds.FirstAsync(x => x.Id == refundId, ct);
        var projects = await db.DonationLines.Where(l => l.DonationId == r.DonationId).Select(l => l.ProjectId).Distinct().ToListAsync(ct);
        foreach (var pid in projects)
        {
            var f = await fin.ForProjectAsync(pid, ct);
            if (f.AvailableToSpend < r.Amount)
                return $"تنبيه: المتاح للصرف في المشروع ({f.AvailableToSpend:N2}) أقل من مبلغ الاسترداد — قد يتأثر الصرف أو الحجوزات.";
        }
        return null;
    }

    public async Task DecideRefundAsync(int refundId, bool approve, string? note, CancellationToken ct = default)
    {
        user.Require(Perm.RefundsApprove);
        var r = await db.Refunds.Include(x => x.Donation).FirstAsync(x => x.Id == refundId, ct);
        ApprovalMatrixService.EnsureNotSelf(r.RequestedById, user.UserId);
        if (r.Status is not (RequestStatus.Submitted or RequestStatus.UnderReview)) throw Invalid();
        if (approve) await matrix.EnsureWithinLimitAsync(ApprovalModule.Refund, r.Amount, r.Donation!.CenterId, null, ct);
        r.Status = approve ? RequestStatus.Approved : RequestStatus.Rejected; r.DecidedById = user.UserId; r.DecisionNote = note;
        audit.Log("Collections", approve ? "RefundApproved" : "RefundRejected", nameof(RefundRequest), r.Id, reason: note, reference: r.Number);
        await db.SaveChangesAsync(ct);
    }

    public async Task ExecuteRefundAsync(int refundId, string reference, CancellationToken ct = default)
    {
        user.Require(Perm.RefundsExecute);
        if (string.IsNullOrWhiteSpace(reference)) throw new DomainException("مرجع إثبات التنفيذ مطلوب.", "Execution proof reference is required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var r = await db.Refunds.FirstAsync(x => x.Id == refundId, ct);
        if (r.Status != RequestStatus.Approved) throw Invalid();
        var d = await db.Donations.Include(x => x.Lines).Include(x => x.Receipt).FirstAsync(x => x.Id == r.DonationId, ct);
        if (r.Amount > d.RefundableAmount) throw new DomainException("المبلغ يتجاوز المتبقي القابل للاسترداد.", "Amount exceeds refundable remainder.");

        // Distribute across lines proportionally to their remaining net (rounding remainder on the last line).
        var lines = d.Lines.Where(l => l.Amount - l.RefundedAmount - l.ReallocatedAmount > 0).ToList();
        var netTotal = lines.Sum(l => l.Amount - l.RefundedAmount - l.ReallocatedAmount);
        var left = r.Amount;
        var portions = new List<(DonationLine Line, decimal Amount)>();
        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            var share = i == lines.Count - 1 ? left : Money.Round(r.Amount * (l.Amount - l.RefundedAmount - l.ReallocatedAmount) / netTotal);
            share = Math.Min(share, l.Amount - l.RefundedAmount - l.ReallocatedAmount);
            l.RefundedAmount += share; left -= share;
            portions.Add((l, share));
        }
        d.RefundedAmount += r.Amount;
        d.Status = d.RefundedAmount >= d.Total ? DonationStatus.Refunded : DonationStatus.PartiallyRefunded;
        r.Status = RequestStatus.Executed; r.ExecutedAt = DateTime.UtcNow; r.ExecutionReference = reference;

        foreach (var (line, amt) in portions.Where(p => p.Amount > 0))
        {
            var key = $"refund:{r.Id}:line:{line.Id}";
            await new OutboxPublisher(db).PublishAsync(Domain.Integration.MessageTypes.CollectionResult,
                new CollectionResultPayload(key, line.ProjectId, line.FinancialItemId, line.LicenseId, CollectionResultKind.Refund, -amt, d.Currency, DateTime.UtcNow, d.Number, d.Receipt?.Number, null, line.MappingVersion),
                key, r.Number, ct);
        }
        audit.Log("Collections", "RefundExecuted", nameof(RefundRequest), r.Id, newValues: new { r.Amount, reference, DonationNet = d.NetAmount }, reference: r.Number);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await refresher.RefreshAsync(portions.Select(p => p.Line.ProjectId).Distinct().ToList(), ct);
    }

    // ---------------- Reallocation ----------------
    public async Task<List<ReallocationRequest>> ReallocationsAsync(CancellationToken ct = default)
    {
        user.RequireAny(Perm.ReallocationRequest, Perm.ReallocationApprove);
        return await db.Reallocations.AsNoTracking().OrderByDescending(r => r.Id).ToListAsync(ct);
    }

    public async Task<int> RequestReallocationAsync(int donationLineId, int toProjectId, decimal amount, string justification, bool donorConsent, CancellationToken ct = default)
    {
        user.Require(Perm.ReallocationRequest);
        if (string.IsNullOrWhiteSpace(justification)) throw new DomainException("المبرر مطلوب.", "Justification is required.");
        var line = await db.DonationLines.FirstAsync(l => l.Id == donationLineId, ct);
        if (line.ProjectId == toProjectId) throw new DomainException("اختر مشروعًا وجهة مختلفًا.", "Choose a different destination project.");
        var available = line.Amount - line.RefundedAmount - line.ReallocatedAmount;
        if (amount <= 0 || amount > available) throw new DomainException($"المبلغ يتجاوز المتاح في البند ({available:N2}).", $"Amount exceeds the line's available amount ({available:N2}).");
        // A restricted donation is not moved by direct edit: donor consent is required where its terms demand it.
        if (!string.IsNullOrWhiteSpace(line.Restrictions) && !donorConsent)
            throw new DomainException("التبرع مقيد بشروط — يلزم موافقة المتبرع.", "Restricted donation — donor consent is required.");
        var r = new ReallocationRequest { Number = await numbers.NextAsync("reallocation", "RAL", 5, true, ct), DonationLineId = line.Id, FromProjectId = line.ProjectId,
            ToProjectId = toProjectId, Amount = amount, Justification = justification, DonorConsentObtained = donorConsent, RequestedById = user.UserId! };
        db.Reallocations.Add(r);
        await db.SaveChangesAsync(ct);
        audit.Log("Collections", "ReallocationRequested", nameof(ReallocationRequest), r.Id, null, new { line.ProjectCode, toProjectId, amount }, justification, r.Number);
        await db.SaveChangesAsync(ct);
        return r.Id;
    }

    /// <summary>Approval re-checks destination eligibility. Reallocation is not a new collection.</summary>
    public async Task DecideReallocationAsync(int id, bool approve, string? note, CancellationToken ct = default)
    {
        user.Require(Perm.ReallocationApprove);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var r = await db.Reallocations.FirstAsync(x => x.Id == id, ct);
        ApprovalMatrixService.EnsureNotSelf(r.RequestedById, user.UserId);
        if (r.Status != RequestStatus.Submitted) throw Invalid();
        if (!approve)
        {
            r.Status = RequestStatus.Rejected; r.DecidedById = user.UserId; r.DecisionNote = note;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return;
        }
        var src = await db.DonationLines.FirstAsync(l => l.Id == r.DonationLineId, ct);
        var d = await db.Donations.Include(x => x.Lines).Include(x => x.Receipt).FirstAsync(x => x.Id == src.DonationId, ct);
        var srcProject = await db.Projects.FirstAsync(p => p.Id == src.ProjectId, ct);
        if (srcProject.IsClosed) throw new DomainException("مشروع المصدر مقفل — تنسق المعالجة الاستثنائية مع المشاريع.", "Source project is closed — coordinate an exceptional treatment with Projects.");
        await matrix.EnsureWithinLimitAsync(ApprovalModule.Reallocation, r.Amount, d.CenterId, null, ct);
        await gateway.LockAsync([r.ToProjectId], ct);
        var allocs = await gateway.AllocateAsync(r.ToProjectId, r.Amount, LicenseService.Today, ct);
        src.ReallocatedAmount += r.Amount;
        var outbox = new OutboxPublisher(db);
        var keyOut = $"realloc:{r.Id}:out";
        await outbox.PublishAsync(Domain.Integration.MessageTypes.CollectionResult, new CollectionResultPayload(keyOut, src.ProjectId, src.FinancialItemId, src.LicenseId,
            CollectionResultKind.ReallocationOut, -r.Amount, d.Currency, DateTime.UtcNow, d.Number, d.Receipt?.Number, null, src.MappingVersion), keyOut, r.Number, ct);
        foreach (var a in allocs)
        {
            var nl = new DonationLine
            {
                DonationId = d.Id, ProjectId = a.ProjectId, ProgramId = a.ProgramId, FinancialItemId = a.FinancialItemId, LicenseId = a.LicenseId, MappingVersion = a.MappingVersion,
                ProjectCode = a.ProjectCode, ProjectNameAr = a.NameAr, ProjectNameEn = a.NameEn, FinancialItemNumber = a.FinancialItemNumber, LicenseNumber = a.LicenseNumber,
                Amount = a.Amount, Restrictions = $"إعادة تخصيص {r.Number} من {src.ProjectCode}",
            };
            d.Lines.Add(nl);
            await db.SaveChangesAsync(ct);
            var keyIn = $"realloc:{r.Id}:in:{nl.Id}";
            await outbox.PublishAsync(Domain.Integration.MessageTypes.CollectionResult, new CollectionResultPayload(keyIn, a.ProjectId, a.FinancialItemId, a.LicenseId,
                CollectionResultKind.ReallocationIn, a.Amount, d.Currency, DateTime.UtcNow, d.Number, d.Receipt?.Number, null, a.MappingVersion), keyIn, r.Number, ct);
        }
        r.Status = RequestStatus.Executed; r.DecidedById = user.UserId; r.DecisionNote = note;
        audit.Log("Collections", "ReallocationApproved", nameof(ReallocationRequest), r.Id, null, new { r.FromProjectId, r.ToProjectId, r.Amount }, note, r.Number);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await refresher.RefreshAsync([src.ProjectId, r.ToProjectId], ct);
    }

    // ---------------- Receipt corrections ----------------
    public async Task<List<ReceiptCorrection>> CorrectionsAsync(CancellationToken ct = default)
    {
        user.RequireAny(Perm.ReceiptsCorrect, Perm.ReceiptsCorrectApprove);
        return await db.ReceiptCorrections.AsNoTracking().OrderByDescending(c => c.Id).ToListAsync(ct);
    }

    public async Task RequestCorrectionAsync(int receiptId, CorrectionType type, string reason, string? correctedDonorNote, CancellationToken ct = default)
    {
        user.Require(Perm.ReceiptsCorrect);
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("السبب مطلوب.", "Reason is required.");
        var rc = await db.Receipts.FirstAsync(x => x.Id == receiptId, ct);
        if (rc.IsCancelled) throw new DomainException("السند ملغى.", "Receipt is cancelled.");
        db.ReceiptCorrections.Add(new ReceiptCorrection { ReceiptId = receiptId, Type = type, Reason = reason, CorrectedDonorNote = correctedDonorNote, RequestedById = user.UserId! });
        audit.Log("Collections", "ReceiptCorrectionRequested", nameof(Receipt), receiptId, null, new { type }, reason, rc.Number);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>A receipt is never deleted nor its value edited directly. Cancel reverses its effect; correct issues a replacement linked to the original.</summary>
    public async Task DecideCorrectionAsync(int correctionId, bool approve, int? newDonorId, CancellationToken ct = default)
    {
        user.Require(Perm.ReceiptsCorrectApprove);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var c = await db.ReceiptCorrections.FirstAsync(x => x.Id == correctionId, ct);
        ApprovalMatrixService.EnsureNotSelf(c.RequestedById, user.UserId);
        if (c.Status != RequestStatus.Submitted) throw Invalid();
        c.DecidedById = user.UserId;
        if (!approve) { c.Status = RequestStatus.Rejected; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return; }
        var rc = await db.Receipts.FirstAsync(x => x.Id == c.ReceiptId, ct);
        var d = await db.Donations.Include(x => x.Lines).FirstAsync(x => x.Id == rc.DonationId, ct);
        if (d.RefundedAmount > 0 && c.Type == CorrectionType.Cancel) throw new DomainException("لا يمكن إلغاء سند عليه استرداد.", "Cannot cancel a receipt with refunds.");
        rc.IsCancelled = true; rc.CancelReason = c.Reason;
        if (c.Type == CorrectionType.Cancel)
        {
            d.Status = DonationStatus.Cancelled;
            await donations.PublishLinesAsync(d, CollectionResultKind.Correction, rc.Number, null, ct, sign: -1, suffix: $":cancel:{c.Id}");
        }
        else
        {
            if (newDonorId is int nd) d.DonorId = nd;
            var replacement = await donations.IssueReceiptAsync(d, rc.AgreementNumber, ct);
            replacement.ReplacesReceiptId = rc.Id;
        }
        c.Status = RequestStatus.Executed;
        audit.Log("Collections", c.Type == CorrectionType.Cancel ? "ReceiptCancelled" : "ReceiptCorrected", nameof(Receipt), rc.Id, reason: c.Reason, reference: rc.Number);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await refresher.RefreshAsync(d.Lines.Select(l => l.ProjectId).Distinct().ToList(), ct);
    }

    private static DomainException Invalid() => new("الإجراء غير متاح في الحالة الحالية.", "Action not available in the current state.");
}

public class ReconciliationService(IEidDb db, ICurrentUser user, AuditService audit)
{
    /// <summary>Builds reconciliation lines for confirmed non-cash transactions not yet listed.</summary>
    public async Task<int> GenerateAsync(CancellationToken ct = default)
    {
        user.Require(Perm.ReconciliationManage);
        var listed = db.ReconciliationItems.Where(r => r.DonationId != null).Select(r => r.DonationId);
        var toAdd = await db.Donations.Where(d => d.PaymentMethod != PaymentMethod.Cash && (d.Status == DonationStatus.Confirmed || d.Status == DonationStatus.PartiallyRefunded)
                                                  && !listed.Contains(d.Id)).ToListAsync(ct);
        foreach (var d in toAdd)
            db.ReconciliationItems.Add(new ReconciliationItem
            {
                DonationId = d.Id, Channel = d.PaymentMethod switch { PaymentMethod.Pos => ReconChannel.Pos, PaymentMethod.Online => ReconChannel.Online, _ => ReconChannel.BankTransfer },
                ExternalReference = d.PaymentReference ?? d.Number, ExpectedAmount = d.Total, Fee = d.ChannelFee, Status = ReconStatus.Unmatched,
            });
        await db.SaveChangesAsync(ct);
        return toAdd.Count;
    }

    public async Task<List<ReconciliationItem>> ListAsync(ReconChannel? channel, ReconStatus? status, CancellationToken ct = default)
    {
        user.Require(Perm.ReconciliationManage);
        var q = db.ReconciliationItems.AsNoTracking().AsQueryable();
        if (channel is not null) q = q.Where(r => r.Channel == channel);
        if (status is not null) q = q.Where(r => r.Status == status);
        return await q.OrderByDescending(r => r.Id).Take(300).ToListAsync(ct);
    }

    public async Task MatchAsync(int id, decimal actualAmount, string? note, CancellationToken ct = default)
    {
        user.Require(Perm.ReconciliationManage);
        var r = await db.ReconciliationItems.FirstAsync(x => x.Id == id, ct);
        r.ActualAmount = actualAmount;
        var diff = actualAmount - (r.ExpectedAmount - r.Fee);
        r.Status = diff == 0 ? ReconStatus.Matched : ReconStatus.Difference;
        if (r.Status == ReconStatus.Difference && string.IsNullOrWhiteSpace(note)) throw new DomainException("يوجد فرق — اكتب ملاحظة.", "Difference — add a note.");
        r.Note = note; r.Responsible = user.DisplayName;
        if (r.DonationId is int did && r.Status == ReconStatus.Matched)
            await db.Donations.Where(d => d.Id == did).ExecuteUpdateAsync(s => s.SetProperty(d => d.Settlement, SettlementStatus.Settled), ct);
        audit.Log("Collections", "ReconciliationMatched", nameof(ReconciliationItem), r.Id, null, new { r.ExpectedAmount, actualAmount, r.Status }, note, r.ExternalReference);
        await db.SaveChangesAsync(ct);
    }

    public async Task ResolveAsync(int id, string note, CancellationToken ct = default)
    {
        user.Require(Perm.ReconciliationManage);
        if (string.IsNullOrWhiteSpace(note)) throw new DomainException("وصف المعالجة مطلوب.", "Resolution note is required.");
        var r = await db.ReconciliationItems.FirstAsync(x => x.Id == id, ct);
        r.Status = ReconStatus.Resolved; r.Note = note;
        audit.Log("Collections", "ReconciliationResolved", nameof(ReconciliationItem), r.Id, reason: note, reference: r.ExternalReference);
        await db.SaveChangesAsync(ct);
    }
}
