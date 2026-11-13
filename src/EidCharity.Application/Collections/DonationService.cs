using EidCharity.Application.Common;
using EidCharity.Application.Integration;
using EidCharity.Application.Projects;
using EidCharity.Domain;
using EidCharity.Domain.Collections;
using EidCharity.Domain.Integration;
using EidCharity.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace EidCharity.Application.Collections;

public class DraftLine
{
    public int ProjectId { get; set; }
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string Code { get; set; } = "";
    public decimal Amount { get; set; }
    public string? Restrictions { get; set; }
}

/// <summary>The collector's donation draft (the "cart"). Kept on language switch and step changes.</summary>
public class DonationDraft
{
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");
    public List<DraftLine> Lines { get; set; } = [];
    public int? DonorId { get; set; }
    public string? DonorDisplay { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public string? PaymentReference { get; set; }
    public string? BankName { get; set; }
    public DateOnly? TransferDate { get; set; }
    public string? PosTerminal { get; set; }
    /// <summary>POS: Approved / Declined. Online: Success / Pending / Failed.</summary>
    public string? ChannelResult { get; set; }
    public decimal ChannelFee { get; set; }
    public string CollectionPoint { get; set; } = "";
    public string? Notes { get; set; }
    public int? AgreementId { get; set; }
    public bool WantsInstallments { get; set; }
    public decimal Total => Lines.Sum(l => l.Amount);
}

public record DonationResult(int DonationId, string Number, DonationStatus Status, int? ReceiptId, string? ReceiptNumber, bool WasDuplicate, string? MessageAr = null, string? MessageEn = null);

/// <summary>Payload sent to Projects for every effect on a project (idempotent by key).</summary>
public record CollectionResultPayload(string Key, int ProjectId, int? FinancialItemId, int? LicenseId, CollectionResultKind Kind, decimal Amount, string Currency,
    DateTime OccurredAt, string? TransactionNumber, string? ReceiptNumber, string? AgreementNumber, int MappingVersion);

public class DonationService(IEidDb db, ICurrentUser user, INumberGenerator numbers, AuditService audit, OutboxPublisher outbox,
    EligibilityGateway gateway, ProjectStatusRefresher refresher, CustodyService custody)
{
    public async Task<DonationResult> ConfirmAsync(DonationDraft draft, CancellationToken ct = default)
    {
        user.Require(Perm.DonationsConfirm);

        // Repeated click / notification never creates a second transaction or receipt.
        var existing = await db.Donations.AsNoTracking().Include(d => d.Receipt).FirstOrDefaultAsync(d => d.IdempotencyKey == draft.IdempotencyKey, ct);
        if (existing is not null)
            return new DonationResult(existing.Id, existing.Number, existing.Status, existing.Receipt?.Id, existing.Receipt?.Number, true);

        ValidateDraft(draft);
        var donor = await db.Donors.FirstOrDefaultAsync(d => d.Id == draft.DonorId && d.IsActive && d.MergedIntoDonorId == null, ct)
                    ?? throw new DomainException("اختر المتبرع أو سجّله قبل التأكيد.", "Select or register the donor before confirming.");

        Agreement? agreement = null;
        if (draft.AgreementId is int aid)
        {
            agreement = await db.Agreements.Include(a => a.Installments).Include(a => a.Lines).FirstAsync(a => a.Id == aid, ct);
            if (agreement.Status != AgreementStatus.Active) throw new DomainException("الاتفاقية غير مفعلة.", "Agreement is not active.");
            if (agreement.DonorId != donor.Id) throw new DomainException("المتبرع لا يطابق الاتفاقية.", "Donor does not match the agreement.");
            if (draft.Total > agreement.Remaining)
                throw new DomainException($"المبلغ يتجاوز المتبقي من الاتفاقية ({agreement.Remaining:N2}). تُراجع الزيادة كتعديل أو تبرع مستقل.",
                    $"Amount exceeds agreement remaining ({agreement.Remaining:N2}). Treat the excess as an amendment or separate donation.");
            if (draft.Lines.Any(l => agreement.Lines.All(al => al.ProjectId != l.ProjectId)))
                throw new DomainException("مشاريع الدفعة يجب أن تكون من مشاريع الاتفاقية.", "Payment projects must belong to the agreement.");
        }

        var status = draft.Method switch
        {
            PaymentMethod.Cash => DonationStatus.Confirmed,
            PaymentMethod.Pos => draft.ChannelResult == "Approved" ? DonationStatus.Confirmed : DonationStatus.Failed,
            PaymentMethod.Online => draft.ChannelResult switch { "Success" => DonationStatus.Confirmed, "Pending" => DonationStatus.PendingVerification, _ => DonationStatus.Failed },
            // A transfer image alone does not prove arrival: stays pending until a specialist confirms.
            PaymentMethod.BankTransfer => DonationStatus.PendingVerification,
            _ => DonationStatus.Failed,
        };

        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        // Serialise concurrent confirmations on the same licenses / project caps, then re-check eligibility live.
        await gateway.LockAsync(draft.Lines.Select(l => l.ProjectId), ct);
        var today = LicenseService.Today;
        var allocations = new List<(DraftLine Line, LineAllocation Alloc)>();
        foreach (var group in draft.Lines.GroupBy(l => l.ProjectId))
        {
            foreach (var line in group)
            {
                var allocs = await gateway.AllocateAsync(line.ProjectId, line.Amount, today, ct);
                allocations.AddRange(allocs.Select(a => (line, a)));
            }
        }

        var d = new Donation
        {
            Number = await numbers.NextAsync("donation", "TRX", 6, true, ct),
            DonorId = donor.Id, PaymentMethod = draft.Method, Status = status, Total = Money.Round(draft.Total), ChannelFee = draft.ChannelFee,
            CollectorUserId = user.UserId!, CollectorName = user.DisplayName, CenterId = user.CenterId ?? 0, CollectionPoint = draft.CollectionPoint,
            OccurredAt = DateTime.UtcNow, PaymentReference = draft.PaymentReference, BankName = draft.BankName, TransferDate = draft.TransferDate,
            PosTerminal = draft.PosTerminal, PosResult = draft.ChannelResult, IdempotencyKey = draft.IdempotencyKey, AgreementId = agreement?.Id,
            Notes = draft.Notes,
            Settlement = draft.Method == PaymentMethod.Cash ? SettlementStatus.Unsettled : draft.Method == PaymentMethod.BankTransfer ? SettlementStatus.NotApplicable : SettlementStatus.Unsettled,
            FailureReason = status == DonationStatus.Failed ? "رفض قناة الدفع" : null,
        };
        foreach (var (line, a) in allocations)
            d.Lines.Add(new DonationLine
            {
                ProjectId = a.ProjectId, ProgramId = a.ProgramId, FinancialItemId = a.FinancialItemId, LicenseId = a.LicenseId, MappingVersion = a.MappingVersion,
                ProjectCode = a.ProjectCode, ProjectNameAr = a.NameAr, ProjectNameEn = a.NameEn, FinancialItemNumber = a.FinancialItemNumber, LicenseNumber = a.LicenseNumber,
                Amount = a.Amount, Restrictions = line.Restrictions,
            });
        if (d.Lines.Sum(l => l.Amount) != d.Total)
            throw new DomainException("مجموع بنود التخصيص لا يطابق الإجمالي.", "Allocation lines do not match the total.");

        if (status == DonationStatus.Confirmed)
        {
            d.ConfirmedAt = DateTime.UtcNow; d.ConfirmedById = user.UserId;
            if (draft.Method == PaymentMethod.Cash) d.CustodyId = await custody.EnsureOpenAsync(user.UserId!, user.DisplayName, ct);
        }
        db.Donations.Add(d);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            if (!await db.Donations.AnyAsync(x => x.IdempotencyKey == draft.IdempotencyKey, ct)) throw;
            var dup = await db.Donations.AsNoTracking().Include(x => x.Receipt).FirstAsync(x => x.IdempotencyKey == draft.IdempotencyKey, ct);
            return new DonationResult(dup.Id, dup.Number, dup.Status, dup.Receipt?.Id, dup.Receipt?.Number, true);
        }

        Receipt? receipt = null;
        if (status == DonationStatus.Confirmed)
        {
            receipt = await IssueReceiptAsync(d, agreement?.Number, ct);
            if (agreement is not null) ApplyToInstallments(agreement, d.Total, today);
        }
        if (status != DonationStatus.Failed)
            await PublishLinesAsync(d, status == DonationStatus.Confirmed ? CollectionResultKind.Confirmed : CollectionResultKind.Pending, receipt?.Number, agreement?.Number, ct);

        audit.Log("Collections", "DonationCreated", nameof(Donation), d.Id, newValues: new { d.Number, d.Total, d.PaymentMethod, Status = d.Status.ToString(), Lines = d.Lines.Select(l => new { l.ProjectCode, l.LicenseNumber, l.Amount }) }, reference: d.Number);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await refresher.RefreshAsync(d.Lines.Select(l => l.ProjectId).Distinct().ToList(), ct);

        return new DonationResult(d.Id, d.Number, d.Status, receipt?.Id, receipt?.Number, false,
            status == DonationStatus.PendingVerification ? "العملية قيد التحقق — صدر إشعار متابعة وليس سند قبض." : status == DonationStatus.Failed ? "فشلت عملية الدفع ولم يُسجل تحصيل." : null,
            status == DonationStatus.PendingVerification ? "Pending verification — a follow-up notice (not a receipt) was issued." : status == DonationStatus.Failed ? "Payment failed; nothing was collected." : null);
    }

    private static void ValidateDraft(DonationDraft draft)
    {
        if (draft.Lines.Count == 0) throw new DomainException("أضف مشروعًا واحدًا على الأقل.", "Add at least one project.");
        if (draft.Lines.Any(l => l.ProjectId <= 0))
            throw new DomainException("لا يمكن تأكيد تبرع دون مشروع — البرنامج للتصفية فقط.", "A donation cannot be confirmed without a project — programs are filters only.");
        if (draft.Lines.Any(l => l.Amount <= 0)) throw new DomainException("كل بند يحتاج مبلغًا أكبر من صفر.", "Every line needs a positive amount.");
        switch (draft.Method)
        {
            case PaymentMethod.BankTransfer when string.IsNullOrWhiteSpace(draft.PaymentReference) || string.IsNullOrWhiteSpace(draft.BankName) || draft.TransferDate is null:
                throw new DomainException("التحويل البنكي يحتاج البنك والمرجع والتاريخ.", "Bank transfer requires bank, reference and date.");
            case PaymentMethod.Pos when string.IsNullOrWhiteSpace(draft.PosTerminal) || string.IsNullOrWhiteSpace(draft.PaymentReference) || string.IsNullOrWhiteSpace(draft.ChannelResult):
                throw new DomainException("نقاط البيع تحتاج الجهاز ورقم العملية والنتيجة.", "POS requires terminal, transaction number and result.");
            case PaymentMethod.Online when string.IsNullOrWhiteSpace(draft.PaymentReference) || string.IsNullOrWhiteSpace(draft.ChannelResult):
                throw new DomainException("القناة الإلكترونية تحتاج مرجعًا فريدًا ونتيجة الدفع.", "Online channel requires a unique reference and result.");
        }
        if (draft.ChannelFee < 0 || draft.ChannelFee > draft.Total) throw new DomainException("رسوم القناة غير صحيحة.", "Invalid channel fee.");
    }

    internal async Task<Receipt> IssueReceiptAsync(Donation d, string? agreementNumber, CancellationToken ct)
    {
        var r = new Receipt
        {
            Number = await numbers.NextAsync("receipt", "RCP", 6, true, ct), DonationId = d.Id, IssuedAt = DateTime.UtcNow, Amount = d.Total,
            Currency = d.Currency, AgreementNumber = agreementNumber,
        };
        db.Receipts.Add(r);
        await db.SaveChangesAsync(ct);
        return r;
    }

    internal static void ApplyToInstallments(Agreement agreement, decimal amount, DateOnly today)
    {
        // Partial and early payments are distributed over due installments in order.
        var left = amount;
        foreach (var i in agreement.Installments.Where(i => i.Status != InstallmentStatus.Cancelled).OrderBy(i => i.DueDate).ThenBy(i => i.Seq))
        {
            if (left <= 0) break;
            var open = i.Amount - i.PaidAmount;
            if (open <= 0) continue;
            var pay = Math.Min(open, left);
            i.PaidAmount += pay; left -= pay;
            i.Status = i.Compute(today);
        }
        if (agreement.Installments.All(i => i.Status is InstallmentStatus.Paid or InstallmentStatus.Cancelled)) agreement.Status = AgreementStatus.Completed;
    }

    internal async Task PublishLinesAsync(Donation d, CollectionResultKind kind, string? receiptNumber, string? agreementNumber, CancellationToken ct, decimal sign = 1, string? suffix = null)
    {
        foreach (var l in d.Lines)
        {
            var key = $"donation:{d.Id}:line:{l.Id}:{kind}{suffix}";
            await outbox.PublishAsync(MessageTypes.CollectionResult,
                new CollectionResultPayload(key, l.ProjectId, l.FinancialItemId, l.LicenseId, kind, sign * l.Amount, d.Currency, DateTime.UtcNow, d.Number, receiptNumber, agreementNumber, l.MappingVersion),
                key, d.Number, ct);
        }
    }

    // ---------------- Pending verification (C14) ----------------
    public async Task<List<Donation>> PendingAsync(CancellationToken ct = default)
    {
        user.Require(Perm.DonationsVerify);
        return await db.Donations.AsNoTracking().Include(d => d.Donor).Include(d => d.Lines)
            .Where(d => d.Status == DonationStatus.PendingVerification).OrderBy(d => d.OccurredAt).ToListAsync(ct);
    }

    public async Task<DonationResult> VerifyAsync(int donationId, bool received, string? note, CancellationToken ct = default)
    {
        user.Require(Perm.DonationsVerify);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var d = await db.Donations.Include(x => x.Lines).Include(x => x.Receipt).FirstAsync(x => x.Id == donationId, ct);
        if (d.Status != DonationStatus.PendingVerification)
            return new DonationResult(d.Id, d.Number, d.Status, d.Receipt?.Id, d.Receipt?.Number, true);
        if (!received && string.IsNullOrWhiteSpace(note)) throw new DomainException("سبب عدم التأكيد مطلوب.", "Reason is required.");

        // Pending release on the project side first, then confirmed (net effect on cap = 0; on collected = +amount).
        await PublishLinesAsync(d, CollectionResultKind.PendingCancelled, null, null, ct, sign: -1);
        Receipt? r = null;
        if (received)
        {
            d.Status = DonationStatus.Confirmed; d.ConfirmedAt = DateTime.UtcNow; d.ConfirmedById = user.UserId; d.Settlement = SettlementStatus.Settled;
            Agreement? ag = d.AgreementId is int aid ? await db.Agreements.Include(a => a.Installments).FirstAsync(a => a.Id == aid, ct) : null;
            r = await IssueReceiptAsync(d, ag?.Number, ct);
            if (ag is not null) ApplyToInstallments(ag, d.Total, LicenseService.Today);
            await PublishLinesAsync(d, CollectionResultKind.Confirmed, r.Number, ag?.Number, ct);
        }
        else { d.Status = DonationStatus.Failed; d.FailureReason = note; }
        audit.Log("Collections", received ? "PendingVerified" : "PendingRejected", nameof(Donation), d.Id, reason: note, reference: d.Number);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await refresher.RefreshAsync(d.Lines.Select(l => l.ProjectId).Distinct().ToList(), ct);
        return new DonationResult(d.Id, d.Number, d.Status, r?.Id, r?.Number, false);
    }

    // ---------------- Transactions & receipts ----------------
    public async Task<PagedResult<Donation>> ListAsync(string? search, DonationStatus? status, PaymentMethod? method, bool mineOnly, int page, CancellationToken ct = default)
    {
        user.Require(Perm.CollectUse);
        var q = db.Donations.AsNoTracking().Include(d => d.Donor).Include(d => d.Receipt).Include(d => d.Lines).AsQueryable();
        if (mineOnly || !user.Has(Perm.DonationsViewAll)) q = q.Where(d => d.CollectorUserId == user.UserId);
        if (status is not null) q = q.Where(d => d.Status == status);
        if (method is not null) q = q.Where(d => d.PaymentMethod == method);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(d => d.Number.Contains(search) || (d.Receipt != null && d.Receipt.Number.Contains(search)) || (d.Donor != null && (d.Donor.NameAr.Contains(search) || (d.Donor.Phone != null && d.Donor.Phone.Contains(search))))
                          || (d.PaymentReference != null && d.PaymentReference.Contains(search)));
        return await q.OrderByDescending(d => d.Id).AsSplitQuery().PageAsync(page, 20, ct);
    }

    public async Task<Donation> GetAsync(int id, CancellationToken ct = default)
    {
        user.Require(Perm.CollectUse);
        var d = await db.Donations.AsNoTracking().Include(x => x.Donor).Include(x => x.Lines).Include(x => x.Receipt).AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw ProjectService.NotFound();
        if (d.CollectorUserId != user.UserId && !user.Has(Perm.DonationsViewAll) && !user.Has(Perm.DonationsVerify)) throw new ForbiddenException(Perm.DonationsViewAll);
        return d;
    }

    public async Task<Receipt> GetReceiptAsync(int receiptId, CancellationToken ct = default)
    {
        user.Require(Perm.CollectUse);
        var r = await db.Receipts.AsNoTracking().Include(x => x.Donation).ThenInclude(d => d!.Lines).Include(x => x.Donation).ThenInclude(d => d!.Donor)
                    .FirstOrDefaultAsync(x => x.Id == receiptId, ct) ?? throw ProjectService.NotFound();
        if (r.Donation!.CollectorUserId != user.UserId && !user.Has(Perm.DonationsViewAll)) throw new ForbiddenException(Perm.DonationsViewAll);
        return r;
    }

    /// <summary>Reprint is a copy of the same receipt, not a new transaction.</summary>
    public async Task RecordPrintAsync(int receiptId, CancellationToken ct = default)
    {
        var r = await db.Receipts.Include(x => x.Donation).FirstAsync(x => x.Id == receiptId, ct);
        // First print belongs to the issuing collector; any later print is a controlled reprint (copy).
        if (r.PrintCount > 0 || r.Donation!.CollectorUserId != user.UserId) user.Require(Perm.ReceiptsReprint);
        else user.Require(Perm.CollectUse);
        r.PrintCount++; r.LastPrintedAt = DateTime.UtcNow;
        audit.Log("Collections", r.PrintCount > 1 ? "ReceiptReprinted" : "ReceiptPrinted", nameof(Receipt), r.Id, reference: r.Number);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Keeps the project's collection status (e.g. cap reached) in sync after collections change usage.</summary>
public class ProjectStatusRefresher(IEidDb db, EligibilityGateway gateway, FinanceReviewService finance)
{
    public async Task RefreshAsync(List<int> projectIds, CancellationToken ct = default)
    {
        var check = await gateway.CheckAsync(projectIds, ct);
        foreach (var id in projectIds)
        {
            var p = await db.Projects.FirstAsync(x => x.Id == id, ct);
            if (p.ApprovalStatus != ApprovalStatus.FinanciallyApproved || p.IsClosed || p.CollectionStatus == CollectionStatus.Suspended) continue;
            var next = check[id].Eligible ? CollectionStatus.Available : check[id].RemainingCap <= 0 && check[id].ReasonEn == "Collection cap reached" ? CollectionStatus.CapReached : CollectionStatus.NotAvailable;
            if (next == p.CollectionStatus) continue;
            p.CollectionStatus = next;
            await db.SaveChangesAsync(ct);
            await finance.PublishAsync(p, ct);
            await db.SaveChangesAsync(ct);
        }
    }
}
