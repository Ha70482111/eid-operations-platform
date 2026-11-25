using EidCharity.Domain;
using EidCharity.Domain.Collections;
using Microsoft.EntityFrameworkCore;

namespace EidCharity.Application.Collections;

public record CollectorStats(decimal Today, decimal Month, decimal Year, int TodayCount, decimal Pending, int PendingCount, decimal RefundsMonth,
    decimal NetMonth, decimal CashCustody, List<Receipt> LatestReceipts);

public record ReportRow(string KeyAr, string KeyEn, string Currency, decimal Confirmed, decimal Pending, decimal Refunded, decimal Net, int Count);

/// <summary>
/// Personal and institutional collection statistics. Times use Qatar time. Date rule: confirmed amounts by confirmation date;
/// refunds by their execution date and linked to the original. Pledges and in-kind are excluded from confirmed cash totals.
/// </summary>
public class CollectionStatsService(IEidDb db, ICurrentUser user, CustodyService custody)
{
    private static readonly DonationStatus[] Counted = [DonationStatus.Confirmed, DonationStatus.PartiallyRefunded, DonationStatus.Refunded];

    public async Task<CollectorStats> ForMeAsync(CancellationToken ct = default)
    {
        user.Require(Perm.CollectUse);
        var mine = db.Donations.AsNoTracking().Where(d => d.CollectorUserId == user.UserId);
        var confirmed = mine.Where(d => Counted.Contains(d.Status) && d.ConfirmedAt != null);
        var today = QatarTime.StartOfTodayUtc; var month = QatarTime.StartOfMonthUtc; var year = QatarTime.StartOfYearUtc;
        var refundsMonth = await db.Refunds.Where(r => r.Status == RequestStatus.Executed && r.ExecutedAt >= month && r.Donation!.CollectorUserId == user.UserId)
            .SumAsync(r => (decimal?)r.Amount, ct) ?? 0;
        var monthGross = await confirmed.Where(d => d.ConfirmedAt >= month).SumAsync(d => (decimal?)d.Total, ct) ?? 0;
        var open = await db.Custodies.FirstOrDefaultAsync(c => c.CollectorUserId == user.UserId && c.Status == CustodyStatus.Open, ct);
        return new CollectorStats(
            await confirmed.Where(d => d.ConfirmedAt >= today).SumAsync(d => (decimal?)d.Total, ct) ?? 0,
            monthGross,
            await confirmed.Where(d => d.ConfirmedAt >= year).SumAsync(d => (decimal?)d.Total, ct) ?? 0,
            await confirmed.CountAsync(d => d.ConfirmedAt >= today, ct),
            await mine.Where(d => d.Status == DonationStatus.PendingVerification).SumAsync(d => (decimal?)d.Total, ct) ?? 0,
            await mine.CountAsync(d => d.Status == DonationStatus.PendingVerification, ct),
            refundsMonth, monthGross - refundsMonth,
            open is null ? 0 : await custody.ExpectedAsync(open.Id, ct),
            await db.Receipts.AsNoTracking().Include(r => r.Donation).ThenInclude(d => d!.Donor).Where(r => r.Donation!.CollectorUserId == user.UserId)
                .OrderByDescending(r => r.Id).Take(5).ToListAsync(ct));
    }

    public async Task<List<ReportRow>> ReportAsync(string groupBy, DateTime? fromLocal, DateTime? toLocal, bool mineOnly, CancellationToken ct = default)
    {
        if (mineOnly) user.Require(Perm.CollectUse); else user.Require(Perm.CollectionsReports);
        var from = fromLocal is null ? QatarTime.StartOfMonthUtc : QatarTime.ToUtc(fromLocal.Value.Date);
        var to = toLocal is null ? DateTime.UtcNow.AddMinutes(1) : QatarTime.ToUtc(toLocal.Value.Date.AddDays(1));
        var q = db.DonationLines.Join(db.Donations, l => l.DonationId, d => d.Id, (l, d) => new { l, d })
            .Where(x => x.d.OccurredAt >= from && x.d.OccurredAt < to && x.d.Status != DonationStatus.Draft && x.d.Status != DonationStatus.Failed && x.d.Status != DonationStatus.Cancelled);
        if (mineOnly) q = q.Where(x => x.d.CollectorUserId == user.UserId);
        var rows = await q.Select(x => new Flat(x.l.ProjectCode, x.l.ProjectNameAr, x.l.ProjectNameEn, x.l.ProgramId, x.l.FinancialItemNumber, x.l.LicenseNumber,
            x.d.CenterId, x.d.PaymentMethod, x.d.CollectorName, x.d.Currency, x.d.Status, x.d.Id, x.l.Amount, x.l.RefundedAmount, x.l.ReallocatedAmount)).ToListAsync(ct);
        var programs = await db.Programs.AsNoTracking().ToDictionaryAsync(p => p.Id, p => (p.NameAr, p.NameEn), ct);
        var centers = await db.Centers.AsNoTracking().ToDictionaryAsync(c => c.Id, c => (c.NameAr, c.NameEn), ct);
        (string, string) Key(Flat r) => groupBy switch
        {
            "program" => programs.TryGetValue(r.ProgramId, out var p) ? p : ("—", "—"),
            "center" => centers.TryGetValue(r.CenterId, out var c) ? c : ("—", "—"),
            "channel" => (MethodAr(r.PaymentMethod), r.PaymentMethod.ToString()),
            "collector" => (r.CollectorName ?? "—", r.CollectorName ?? "—"),
            "item" => (r.FinancialItemNumber, r.FinancialItemNumber),
            "license" => (r.LicenseNumber, r.LicenseNumber),
            _ => ($"{r.ProjectCode} — {r.ProjectNameAr}", $"{r.ProjectCode} — {r.ProjectNameEn}"),
        };
        // Currencies are never summed together.
        return rows.GroupBy(r => (Key(r), r.Currency)).Select(g =>
        {
            var confirmed = g.Where(r => Counted.Contains(r.Status)).Sum(r => r.Amount);
            var pending = g.Where(r => r.Status == DonationStatus.PendingVerification).Sum(r => r.Amount);
            var refunded = g.Sum(r => r.RefundedAmount);
            var realloc = g.Sum(r => r.ReallocatedAmount);
            return new ReportRow(g.Key.Item1.Item1, g.Key.Item1.Item2, g.Key.Currency, confirmed, pending, refunded, confirmed - refunded - realloc, g.Select(r => r.DonationId).Distinct().Count());
        }).OrderByDescending(r => r.Net).ToList();
    }

    private record Flat(string ProjectCode, string ProjectNameAr, string ProjectNameEn, int ProgramId, string FinancialItemNumber, string LicenseNumber,
        int CenterId, PaymentMethod PaymentMethod, string? CollectorName, string Currency, DonationStatus Status, int DonationId, decimal Amount, decimal RefundedAmount, decimal ReallocatedAmount);

    public static string MethodAr(PaymentMethod m) => m switch
    {
        PaymentMethod.Cash => "نقدي", PaymentMethod.BankTransfer => "تحويل بنكي", PaymentMethod.Pos => "نقاط بيع", PaymentMethod.Online => "قناة إلكترونية", PaymentMethod.Cheque => "شيك", _ => m.ToString()
    };
}
