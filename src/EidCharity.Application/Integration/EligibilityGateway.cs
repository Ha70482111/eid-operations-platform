using EidCharity.Application.Projects;
using EidCharity.Domain;
using EidCharity.Domain.Collections;
using EidCharity.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace EidCharity.Application.Integration;

public record LineAllocation(int ProjectId, int ProgramId, string ProjectCode, string NameAr, string NameEn, int FinancialItemId, string FinancialItemNumber,
    int LicenseId, string LicenseNumber, int MappingVersion, decimal Amount);

public record ProjectEligibility(int ProjectId, bool Eligible, string? ReasonAr, string? ReasonEn, decimal RemainingCap);

/// <summary>
/// The boundary through which Collections asks Projects about eligibility. It is evaluated live at confirmation time
/// (never from a stale list): approval, license validity, collection period, mapping period and remaining caps.
/// </summary>
public class EligibilityGateway(IEidDb db, IResourceLock locks)
{
    private static readonly DonationStatus[] HoldingStatuses = [DonationStatus.Confirmed, DonationStatus.PartiallyRefunded, DonationStatus.Refunded, DonationStatus.PendingVerification];

    public async Task LockAsync(IEnumerable<int> projectIds, CancellationToken ct = default)
    {
        var ids = projectIds.Distinct().ToList();
        var licenseIds = await db.Mappings.Where(m => ids.Contains(m.ProjectId) && m.Status == MappingStatus.Active).Select(m => m.LicenseId).Distinct().ToListAsync(ct);
        foreach (var lid in licenseIds.Order()) await locks.AcquireAsync($"license:{lid}", ct);
        foreach (var pid in ids.Order()) await locks.AcquireAsync($"project-cap:{pid}", ct);
    }

    public async Task<Dictionary<int, ProjectEligibility>> CheckAsync(IReadOnlyCollection<int> projectIds, CancellationToken ct = default)
    {
        var result = new Dictionary<int, ProjectEligibility>();
        foreach (var id in projectIds.Distinct())
        {
            try
            {
                var (cands, projectRemaining) = await CandidatesAsync(id, LicenseService.Today, ct);
                var rem = Math.Min(projectRemaining, cands.Sum(c => c.Remaining));
                result[id] = rem <= 0
                    ? new(id, false, "بلغ سقف التحصيل", "Collection cap reached", 0)
                    : new(id, true, null, null, rem);
            }
            catch (DomainException e) { result[id] = new(id, false, e.MessageAr, e.MessageEn, 0); }
        }
        return result;
    }

    /// <summary>
    /// Allocates an amount for a project to one or more eligible licenses. If one license is not enough the line is
    /// split into clear allocation lines, each with its own license — the total is never duplicated.
    /// Rule (published): the license that expires first is used first.
    /// </summary>
    public async Task<List<LineAllocation>> AllocateAsync(int projectId, decimal amount, DateOnly day, CancellationToken ct = default)
    {
        if (amount <= 0) throw new DomainException("المبلغ يجب أن يكون أكبر من صفر.", "Amount must be greater than zero.");
        var (cands, projectRemaining) = await CandidatesAsync(projectId, day, ct);
        if (amount > projectRemaining)
            throw new DomainException($"المبلغ يتجاوز المتبقي من سقف تحصيل المشروع ({projectRemaining:N2}).", $"Amount exceeds the project's remaining collection cap ({projectRemaining:N2}).");
        var left = amount;
        var result = new List<LineAllocation>();
        foreach (var c in cands)
        {
            if (left <= 0) break;
            var take = Math.Min(left, c.Remaining);
            if (take <= 0) continue;
            result.Add(c.Allocation with { Amount = take });
            left -= take;
        }
        if (left > 0)
            throw new DomainException($"المبلغ يتجاوز المتبقي من سقوف التراخيص المؤهلة بمقدار {left:N2}.", $"Amount exceeds the remaining eligible license caps by {left:N2}.");
        return result;
    }

    private record Candidate(LineAllocation Allocation, decimal Remaining, DateOnly ValidTo);

    private async Task<(List<Candidate>, decimal ProjectRemaining)> CandidatesAsync(int projectId, DateOnly day, CancellationToken ct)
    {
        var p = await db.Projects.AsNoTracking().Include(x => x.Mappings).ThenInclude(m => m.License).Include(x => x.Mappings).ThenInclude(m => m.FinancialItem)
                    .FirstOrDefaultAsync(x => x.Id == projectId, ct)
                ?? throw new DomainException("المشروع غير موجود.", "Project not found.");

        if (p.IsClosed) throw new DomainException("المشروع مقفل.", "Project is closed.");
        if (p.ApprovalStatus != ApprovalStatus.FinanciallyApproved)
            throw new DomainException("المشروع غير معتمد ماليًا للتحصيل.", "Project is not financially approved for collection.");
        if (p.CollectionStatus == CollectionStatus.Suspended) throw new DomainException("التحصيل موقوف على المشروع: " + p.StatusReason, "Collection suspended: " + p.StatusReason);

        var active = p.Mappings.Where(m => m.Status == MappingStatus.Active && m.FinancialItemId != null && m.FinancialItem!.IsActive).ToList();
        if (active.Count == 0) throw new DomainException("لا يوجد ربط بند مالي وترخيص معتمد.", "No approved financial item/license mapping.");
        var eligible = active.Where(m => m.License!.AllowsCollectionOn(day) && day >= m.PeriodFrom && day <= m.PeriodTo).ToList();
        if (eligible.Count == 0) throw new DomainException("لا يوجد ترخيص ساري ضمن فترة التحصيل اليوم.", "No license valid within its collection period today.");

        var licIds = eligible.Select(m => m.LicenseId).ToList();
        // Shared license usage across all projects (watched once).
        var licUsage = await db.DonationLines.Where(l => licIds.Contains(l.LicenseId))
            .Join(db.Donations, l => l.DonationId, d => d.Id, (l, d) => new { l.LicenseId, l.ProjectId, d.Status, Net = l.Amount - l.RefundedAmount - l.ReallocatedAmount })
            .Where(x => HoldingStatuses.Contains(x.Status))
            .GroupBy(x => new { x.LicenseId, x.ProjectId }).Select(g => new { g.Key.LicenseId, g.Key.ProjectId, Sum = g.Sum(x => x.Net) }).ToListAsync(ct);

        var projectUsed = await db.DonationLines.Where(l => l.ProjectId == projectId)
            .Join(db.Donations, l => l.DonationId, d => d.Id, (l, d) => new { d.Status, Net = l.Amount - l.RefundedAmount - l.ReallocatedAmount })
            .Where(x => HoldingStatuses.Contains(x.Status)).SumAsync(x => (decimal?)x.Net, ct) ?? 0;
        var projectRemaining = p.CollectionCap - projectUsed;

        var cands = eligible.Select(m =>
        {
            var licUsed = licUsage.Where(u => u.LicenseId == m.LicenseId).Sum(u => u.Sum);
            var rem = m.License!.Cap - licUsed;
            if (m.LinkCap > 0) rem = Math.Min(rem, m.LinkCap - licUsage.Where(u => u.LicenseId == m.LicenseId && u.ProjectId == projectId).Sum(u => u.Sum));
            return new Candidate(new LineAllocation(p.Id, p.ProgramId, p.Code, p.NameAr, p.NameEn, m.FinancialItemId!.Value, m.FinancialItem!.Number,
                m.LicenseId, m.License.Number, m.MappingVersion, 0), Math.Max(0, rem), m.License.ValidTo);
        }).OrderBy(c => c.ValidTo).ThenBy(c => c.Allocation.LicenseId).ToList();
        return (cands, Math.Max(0, projectRemaining));
    }
}
