using EidCharity.Application.Common;
using EidCharity.Application.Integration;
using EidCharity.Application.Projects;
using EidCharity.Domain;
using EidCharity.Domain.Collections;
using EidCharity.Domain.Integration;
using EidCharity.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace EidCharity.Application.Collections;

public class AgreementInput
{
    public int Id { get; set; }
    public int DonorId { get; set; }
    public int? RepresentativeId { get; set; }
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string Terms { get; set; } = "";
    public string ReportingTerms { get; set; } = "";
    public List<AgreementLineInput> Lines { get; set; } = [];
    public List<InstallmentInput> Installments { get; set; } = [];
    public decimal Total => Lines.Sum(l => l.Amount);
}
public class AgreementLineInput { public int ProjectId { get; set; } public decimal Amount { get; set; } }
public class InstallmentInput { public DateOnly DueDate { get; set; } public decimal Amount { get; set; } }

/// <summary>Pledge = intention; agreement = documented terms; receipt = each amount received. They are never conflated.</summary>
public class AgreementService(IEidDb db, ICurrentUser user, INumberGenerator numbers, AuditService audit, OutboxPublisher outbox, EligibilityGateway gateway)
{
    public async Task<List<Agreement>> ListAsync(AgreementStatus? status, CancellationToken ct = default)
    {
        user.RequireAny(Perm.AgreementsManage, Perm.AgreementsApprove);
        var q = db.Agreements.AsNoTracking().Include(a => a.Donor).Include(a => a.Installments).Include(a => a.Lines).AsQueryable();
        if (status is not null) q = q.Where(a => a.Status == status);
        return await q.OrderByDescending(a => a.Id).AsSplitQuery().ToListAsync(ct);
    }

    public async Task<Agreement> GetAsync(int id, CancellationToken ct = default)
    {
        user.RequireAny(Perm.AgreementsManage, Perm.AgreementsApprove);
        return await db.Agreements.AsNoTracking().Include(a => a.Donor).ThenInclude(d => d!.Representatives).Include(a => a.Installments.OrderBy(i => i.Seq)).Include(a => a.Lines)
            .AsSplitQuery().FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw ProjectService.NotFound();
    }

    public async Task<int> SaveDraftAsync(AgreementInput i, CancellationToken ct = default)
    {
        user.Require(Perm.AgreementsManage);
        if (i.Lines.Count == 0 || i.Lines.Any(l => l.ProjectId == 0 || l.Amount <= 0))
            throw new DomainException("حدد مشروعًا ومبلغًا لكل بند — لا اتفاق على برنامج دون مشروع.", "Each line needs a project and amount — no agreement on a program alone.");
        if (i.Installments.Count == 0) throw new DomainException("أضف جدول الدفعات.", "Add the payment schedule.");
        if (i.Installments.Sum(x => x.Amount) != i.Total)
            throw new DomainException($"مجموع الدفعات ({i.Installments.Sum(x => x.Amount):N2}) يجب أن يساوي إجمالي الاتفاقية ({i.Total:N2}).",
                $"Installments total ({i.Installments.Sum(x => x.Amount):N2}) must equal the agreement total ({i.Total:N2}).");
        var eligibility = await gateway.CheckAsync(i.Lines.Select(l => l.ProjectId).ToList(), ct);
        var notEligible = eligibility.Values.Where(e => !e.Eligible).ToList();
        if (notEligible.Count > 0) throw new DomainException("مشروع غير مؤهل للتحصيل: " + notEligible[0].ReasonAr, "Project not eligible: " + notEligible[0].ReasonEn);
        var donor = await db.Donors.Include(d => d.Representatives).FirstAsync(d => d.Id == i.DonorId, ct);
        if (donor.Type == DonorType.Organization && (i.RepresentativeId is null || donor.Representatives.All(r => r.Id != i.RepresentativeId || !r.CanSign)))
            throw new DomainException("حدد ممثل التوقيع المفوض للجهة.", "Select the authorised signing representative.");

        Agreement a;
        if (i.Id == 0) { a = new Agreement { Number = await numbers.NextAsync("agreement", "AGR", 5, true, ct) }; db.Agreements.Add(a); }
        else
        {
            a = await db.Agreements.Include(x => x.Lines).Include(x => x.Installments).FirstAsync(x => x.Id == i.Id, ct);
            if (a.Status is not (AgreementStatus.Draft or AgreementStatus.UnderReview)) throw new DomainException("لا يمكن تعديل اتفاقية مفعلة إلا بتعديل موثق.", "Active agreements change only via documented amendment.");
            db.AgreementLines.RemoveRange(a.Lines); db.Installments.RemoveRange(a.Installments);
            a.Lines.Clear(); a.Installments.Clear();
        }
        var names = await db.Projects.Where(p => i.Lines.Select(l => l.ProjectId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        a.DonorId = i.DonorId; a.RepresentativeId = i.RepresentativeId; a.Date = i.Date; a.Terms = i.Terms; a.ReportingTerms = i.ReportingTerms; a.Total = i.Total;
        a.Status = AgreementStatus.Draft;
        foreach (var l in i.Lines) a.Lines.Add(new AgreementLine { ProjectId = l.ProjectId, Amount = l.Amount, ProjectNameAr = names[l.ProjectId].NameAr, ProjectNameEn = names[l.ProjectId].NameEn });
        var seq = 1;
        foreach (var inst in i.Installments.OrderBy(x => x.DueDate)) a.Installments.Add(new Installment { Seq = seq++, DueDate = inst.DueDate, Amount = inst.Amount });
        await db.SaveChangesAsync(ct);
        audit.Log("Collections", "AgreementSaved", nameof(Agreement), a.Id, newValues: new { a.Total, Lines = i.Lines, Schedule = i.Installments }, reference: a.Number);
        await db.SaveChangesAsync(ct);
        return a.Id;
    }

    public async Task SubmitAsync(int id, CancellationToken ct = default) => await Move(id, AgreementStatus.Draft, AgreementStatus.UnderReview, Perm.AgreementsManage, null, ct);

    public async Task ApproveAsync(int id, string? note, CancellationToken ct = default)
    {
        var a = await db.Agreements.FirstAsync(x => x.Id == id, ct);
        ApprovalMatrixService.EnsureNotSelf(a.CreatedById, user.UserId);
        await Move(id, AgreementStatus.UnderReview, AgreementStatus.AwaitingSignature, Perm.AgreementsApprove, note, ct);
        a.ApprovedById = user.UserId;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Signed copy recorded ⇒ active. Each installment collected later produces its own receipt linked to the agreement.</summary>
    public async Task MarkSignedAsync(int id, string signedCopyReference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(signedCopyReference)) throw new DomainException("مرجع النسخة الموقعة مطلوب.", "Signed copy reference is required.");
        await Move(id, AgreementStatus.AwaitingSignature, AgreementStatus.Active, Perm.AgreementsManage, signedCopyReference, ct);
        var a = await db.Agreements.Include(x => x.Lines).FirstAsync(x => x.Id == id, ct);
        a.SignedAt = DateTime.UtcNow; a.SignedCopyReference = signedCopyReference;
        // Agreements increase expected (pledged) amounts only.
        foreach (var l in a.Lines)
        {
            var key = $"agreement:{a.Id}:v{a.Version}:line:{l.Id}:pledge";
            await outbox.PublishAsync(MessageTypes.CollectionResult, new CollectionResultPayload(key, l.ProjectId, null, null, CollectionResultKind.Pledge, l.Amount, a.Currency, DateTime.UtcNow, null, null, a.Number, 0), key, a.Number, ct);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task RescheduleAsync(int installmentId, DateOnly newDate, string reason, CancellationToken ct = default)
    {
        user.Require(Perm.AgreementsReschedule);
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("سبب إعادة الجدولة مطلوب.", "Reason is required.");
        var i = await db.Installments.FirstAsync(x => x.Id == installmentId, ct);
        if (i.Status == InstallmentStatus.Paid) throw new DomainException("الدفعة مسددة.", "Installment already paid.");
        audit.Log("Collections", "InstallmentRescheduled", nameof(Installment), i.Id, new { i.DueDate }, new { DueDate = newDate }, reason);
        i.PreviousDueDate ??= i.DueDate;
        i.DueDate = newDate; i.RescheduleReason = reason; i.Status = i.Compute(LicenseService.Today);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Cancelling/reducing never deletes receipts; collected money is handled by a separate procedure.</summary>
    public async Task CancelAsync(int id, string reason, CancellationToken ct = default)
    {
        user.Require(Perm.AgreementsApprove);
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("السبب مطلوب.", "Reason is required.");
        var a = await db.Agreements.Include(x => x.Installments).Include(x => x.Lines).FirstAsync(x => x.Id == id, ct);
        var collected = a.Collected;
        foreach (var i in a.Installments.Where(x => x.PaidAmount < x.Amount)) i.Status = InstallmentStatus.Cancelled;
        var wasActive = a.Status is AgreementStatus.Active or AgreementStatus.Amended;
        a.Status = AgreementStatus.Cancelled; a.AmendmentReason = reason;
        if (wasActive)
        {
            var remainingRatio = a.Total == 0 ? 0 : (a.Total - collected) / a.Total;
            foreach (var l in a.Lines)
            {
                var key = $"agreement:{a.Id}:cancel:line:{l.Id}";
                await outbox.PublishAsync(MessageTypes.CollectionResult, new CollectionResultPayload(key, l.ProjectId, null, null, CollectionResultKind.Pledge,
                    -Money.Round(l.Amount * remainingRatio), a.Currency, DateTime.UtcNow, null, null, a.Number, 0), key, a.Number, ct);
            }
        }
        audit.Log("Collections", "AgreementCancelled", nameof(Agreement), a.Id, reason: reason, newValues: new { collected }, reference: a.Number);
        await db.SaveChangesAsync(ct);
    }

    private async Task Move(int id, AgreementStatus from, AgreementStatus to, string perm, string? note, CancellationToken ct)
    {
        user.Require(perm);
        var a = await db.Agreements.FirstAsync(x => x.Id == id, ct);
        if (a.Status != from) throw new DomainException("الإجراء غير متاح في حالة الاتفاقية الحالية.", "Action not available in the agreement's current state.");
        a.Status = to;
        audit.Log("Collections", $"Agreement{to}", nameof(Agreement), a.Id, new { Status = from.ToString() }, new { Status = to.ToString() }, note, a.Number);
        await db.SaveChangesAsync(ct);
    }

    // ---------- Pledges ----------
    public async Task<List<Pledge>> PledgesAsync(CancellationToken ct = default)
    {
        user.Require(Perm.PledgesManage);
        return await db.Pledges.AsNoTracking().Include(p => p.Donor).OrderByDescending(p => p.Id).ToListAsync(ct);
    }

    public async Task SavePledgeAsync(Pledge input, CancellationToken ct = default)
    {
        user.Require(Perm.PledgesManage);
        if (input.Amount <= 0 || input.ProjectId == 0 || input.DonorId == 0) throw new DomainException("المتبرع والمشروع والمبلغ مطلوبة.", "Donor, project and amount are required.");
        Pledge p;
        if (input.Id == 0) { p = new Pledge { Number = await numbers.NextAsync("pledge", "PLG", 5, true, ct) }; db.Pledges.Add(p); }
        else p = await db.Pledges.FirstAsync(x => x.Id == input.Id, ct);
        var delta = input.Amount - (input.Id == 0 ? 0 : p.Amount);
        p.DonorId = input.DonorId; p.ProjectId = input.ProjectId; p.Amount = input.Amount; p.ExpectedDate = input.ExpectedDate; p.Notes = input.Notes; p.Status = input.Status;
        await db.SaveChangesAsync(ct);
        if (delta != 0)
        {
            var key = $"pledge:{p.Id}:{p.UpdatedAt?.Ticks ?? p.CreatedAt.Ticks}";
            await outbox.PublishAsync(MessageTypes.CollectionResult, new CollectionResultPayload(key, p.ProjectId, null, null, CollectionResultKind.Pledge, delta, p.Currency, DateTime.UtcNow, null, null, p.Number, 0), key, p.Number, ct);
        }
        audit.Log("Collections", "PledgeSaved", nameof(Pledge), p.Id, newValues: new { p.Amount, p.Status }, reference: p.Number);
        await db.SaveChangesAsync(ct);
    }
}

public class CustodyService(IEidDb db, ICurrentUser user, INumberGenerator numbers, AuditService audit)
{
    public async Task<int> EnsureOpenAsync(string collectorId, string collectorName, CancellationToken ct = default)
    {
        var c = await db.Custodies.FirstOrDefaultAsync(x => x.CollectorUserId == collectorId && x.Status == CustodyStatus.Open, ct);
        if (c is not null) return c.Id;
        c = new CollectorCustody { Number = await numbers.NextAsync("custody", "CST", 5, true, ct), CollectorUserId = collectorId, CollectorName = collectorName, OpenedAt = DateTime.UtcNow };
        db.Custodies.Add(c);
        await db.SaveChangesAsync(ct);
        return c.Id;
    }

    /// <summary>Expected cash = confirmed cash receipts in this custody (cancelled ones excluded automatically).</summary>
    public async Task<decimal> ExpectedAsync(int custodyId, CancellationToken ct = default) =>
        await db.Donations.Where(d => d.CustodyId == custodyId && d.PaymentMethod == PaymentMethod.Cash
                                      && (d.Status == DonationStatus.Confirmed || d.Status == DonationStatus.PartiallyRefunded || d.Status == DonationStatus.Refunded))
            .SumAsync(d => (decimal?)d.Total, ct) ?? 0;

    public async Task<List<(CollectorCustody Custody, decimal Expected, int Receipts)>> ListAsync(bool mine, CancellationToken ct = default)
    {
        if (mine) user.Require(Perm.CustodyHandover); else user.Require(Perm.CustodyConfirm);
        var q = db.Custodies.AsNoTracking().AsQueryable();
        if (mine) q = q.Where(c => c.CollectorUserId == user.UserId);
        var list = await q.OrderByDescending(c => c.Id).Take(100).ToListAsync(ct);
        var result = new List<(CollectorCustody, decimal, int)>();
        foreach (var c in list)
            result.Add((c, c.Status == CustodyStatus.Open ? await ExpectedAsync(c.Id, ct) : c.ExpectedAmount,
                await db.Donations.CountAsync(d => d.CustodyId == c.Id && d.Status != DonationStatus.Cancelled, ct)));
        return result;
    }

    /// <summary>Handover to treasury or bank deposit moves the same money — it is not a new collection.</summary>
    public async Task HandoverAsync(int custodyId, decimal amount, HandoverType type, string? depositReference, string? differenceReason, CancellationToken ct = default)
    {
        user.Require(Perm.CustodyHandover);
        var c = await db.Custodies.FirstAsync(x => x.Id == custodyId, ct);
        if (c.CollectorUserId != user.UserId) throw new ForbiddenException(Perm.CustodyHandover);
        if (c.Status != CustodyStatus.Open) throw new DomainException("العهدة ليست مفتوحة.", "Custody is not open.");
        if (type == HandoverType.BankDeposit && string.IsNullOrWhiteSpace(depositReference)) throw new DomainException("مرجع الإيداع مطلوب.", "Deposit reference is required.");
        c.ExpectedAmount = await ExpectedAsync(c.Id, ct);
        c.HandedOverAmount = amount; c.Difference = amount - c.ExpectedAmount;
        if (c.Difference != 0 && string.IsNullOrWhiteSpace(differenceReason)) throw new DomainException("يوجد فرق — اذكر السبب.", "There is a difference — state the reason.");
        c.DifferenceReason = differenceReason; c.HandoverType = type; c.DepositReference = depositReference; c.HandedOverAt = DateTime.UtcNow;
        c.Status = CustodyStatus.HandedOver;
        audit.Log("Collections", "CustodyHandedOver", nameof(CollectorCustody), c.Id, null, new { c.ExpectedAmount, c.HandedOverAmount, c.Difference, type }, differenceReason, c.Number);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The receiver confirms the match; differences must be documented before the custody closes.</summary>
    public async Task ConfirmAsync(int custodyId, string? note, CancellationToken ct = default)
    {
        user.Require(Perm.CustodyConfirm);
        var c = await db.Custodies.FirstAsync(x => x.Id == custodyId, ct);
        ApprovalMatrixService.EnsureNotSelf(c.CollectorUserId, user.UserId);
        if (c.Status != CustodyStatus.HandedOver) throw new DomainException("لم يتم تسليم العهدة بعد.", "Custody not handed over yet.");
        c.Status = CustodyStatus.Closed; c.ConfirmedById = user.UserId; c.ConfirmedAt = DateTime.UtcNow;
        await db.Donations.Where(d => d.CustodyId == c.Id).ExecuteUpdateAsync(s => s.SetProperty(d => d.Settlement, c.Difference == 0 ? SettlementStatus.Settled : SettlementStatus.Difference), ct);
        audit.Log("Collections", "CustodyConfirmed", nameof(CollectorCustody), c.Id, reason: note, reference: c.Number);
        await db.SaveChangesAsync(ct);
    }
}

public class InKindService(IEidDb db, ICurrentUser user, INumberGenerator numbers, AuditService audit, OutboxPublisher outbox)
{
    public async Task<List<InKindDonation>> ListAsync(CancellationToken ct = default)
    {
        user.RequireAny(Perm.InKindManage, Perm.InKindApproveValuation);
        return await db.InKindDonations.AsNoTracking().Include(i => i.Donor).OrderByDescending(i => i.Id).ToListAsync(ct);
    }

    public async Task SaveAsync(InKindDonation input, CancellationToken ct = default)
    {
        user.Require(Perm.InKindManage);
        if (input.ProjectId == 0 || string.IsNullOrWhiteSpace(input.ItemDescription) || input.Quantity <= 0)
            throw new DomainException("المشروع ووصف الصنف والكمية مطلوبة.", "Project, item description and quantity are required.");
        var p = await db.Projects.FirstAsync(x => x.Id == input.ProjectId, ct);
        InKindDonation k;
        if (input.Id == 0) { k = new InKindDonation { Number = await numbers.NextAsync("inkind", "INK", 5, true, ct), ReceivedAt = DateTime.UtcNow, ReceivedBy = user.DisplayName }; db.InKindDonations.Add(k); }
        else k = await db.InKindDonations.FirstAsync(x => x.Id == input.Id, ct);
        k.DonorId = input.DonorId; k.ProjectId = p.Id; k.ProjectNameAr = p.NameAr; k.ItemDescription = input.ItemDescription; k.Category = input.Category; k.Quantity = input.Quantity;
        k.Unit = input.Unit; k.Condition = input.Condition; k.Location = input.Location; k.ExpiryDate = input.ExpiryDate; k.StorageNotes = input.StorageNotes;
        if (!k.ValuationApproved) { k.EstimatedValue = input.EstimatedValue; k.ValuationBasis = input.ValuationBasis; }
        k.Status = k.ValuationApproved ? InKindStatus.Valued : (k.EstimatedValue > 0 ? InKindStatus.ValuationPending : InKindStatus.Received);
        audit.Log("Collections", "InKindSaved", nameof(InKindDonation), k.Id, newValues: new { k.ItemDescription, k.Quantity, k.EstimatedValue }, reference: k.Number);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Estimated value is recorded separately and never enters cash liquidity.</summary>
    public async Task ApproveValuationAsync(int id, CancellationToken ct = default)
    {
        user.Require(Perm.InKindApproveValuation);
        var k = await db.InKindDonations.FirstAsync(x => x.Id == id, ct);
        ApprovalMatrixService.EnsureNotSelf(k.CreatedById, user.UserId);
        if (string.IsNullOrWhiteSpace(k.ValuationBasis)) throw new DomainException("أساس التقييم مطلوب.", "Valuation basis is required.");
        k.ValuationApproved = true; k.Status = InKindStatus.Valued;
        var key = $"inkind:{k.Id}:valued";
        await outbox.PublishAsync(MessageTypes.CollectionResult, new CollectionResultPayload(key, k.ProjectId, null, null, CollectionResultKind.InKind, k.EstimatedValue, Money.DefaultCurrency, DateTime.UtcNow, k.Number, null, null, 0), key, k.Number, ct);
        audit.Log("Collections", "InKindValuationApproved", nameof(InKindDonation), k.Id, newValues: new { k.EstimatedValue }, reference: k.Number);
        await db.SaveChangesAsync(ct);
    }

    public async Task DistributeAsync(int id, decimal qty, CancellationToken ct = default)
    {
        user.Require(Perm.InKindManage);
        var k = await db.InKindDonations.FirstAsync(x => x.Id == id, ct);
        if (qty <= 0 || k.DistributedQuantity + qty > k.Quantity) throw new DomainException("الكمية تتجاوز المتاح في المخزن.", "Quantity exceeds stock.");
        k.DistributedQuantity += qty;
        k.Status = k.DistributedQuantity == k.Quantity ? InKindStatus.Distributed : InKindStatus.InStock;
        audit.Log("Collections", "InKindDistributed", nameof(InKindDonation), k.Id, newValues: new { qty, k.DistributedQuantity }, reference: k.Number);
        await db.SaveChangesAsync(ct);
    }
}
