using EidCharity.Application.Common;
using EidCharity.Application.Integration;
using EidCharity.Application.Projects;
using EidCharity.Domain;
using EidCharity.Domain.Collections;
using Microsoft.EntityFrameworkCore;

namespace EidCharity.Application.Collections;

public record DuplicateHint(Donor Donor, string ReasonAr, string ReasonEn, bool Strong);

public class DonorService(IEidDb db, ICurrentUser user, INumberGenerator numbers, AuditService audit)
{
    public async Task<List<Donor>> SearchAsync(string? text, DonorType? type = null, int take = 20, CancellationToken ct = default)
    {
        user.Require(Perm.DonorsView);
        var q = db.Donors.AsNoTracking().Where(d => d.IsActive && d.MergedIntoDonorId == null);
        if (type is not null) q = q.Where(d => d.Type == type);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var t = text.Trim();
            q = q.Where(d => d.Number.Contains(t) || d.NameAr.Contains(t) || (d.NameEn != null && d.NameEn.Contains(t)) || (d.Phone != null && d.Phone.Contains(t))
                             || (d.Email != null && d.Email.Contains(t)) || (d.RegistrationNumber != null && d.RegistrationNumber.Contains(t)) || (d.NationalId != null && d.NationalId == t));
        }
        return await q.OrderBy(d => d.Privacy == DonorPrivacy.Unknown ? 0 : 1).ThenByDescending(d => d.Id).Take(take).ToListAsync(ct);
    }

    /// <summary>Possible duplicates. Name similarity alone is only a hint and never enough to merge.</summary>
    public async Task<List<DuplicateHint>> FindDuplicatesAsync(Donor input, CancellationToken ct = default)
    {
        var hints = new List<DuplicateHint>();
        var q = db.Donors.AsNoTracking().Where(d => d.Id != input.Id && d.MergedIntoDonorId == null && d.IsActive);
        if (!string.IsNullOrWhiteSpace(input.Phone))
            hints.AddRange((await q.Where(d => d.Phone == input.Phone && d.CountryCode == input.CountryCode).ToListAsync(ct)).Select(d => new DuplicateHint(d, "نفس رقم الهاتف", "Same phone", true)));
        if (!string.IsNullOrWhiteSpace(input.Email))
            hints.AddRange((await q.Where(d => d.Email == input.Email).ToListAsync(ct)).Select(d => new DuplicateHint(d, "نفس البريد", "Same email", true)));
        if (!string.IsNullOrWhiteSpace(input.NationalId))
            hints.AddRange((await q.Where(d => d.NationalId == input.NationalId).ToListAsync(ct)).Select(d => new DuplicateHint(d, "نفس رقم الهوية", "Same ID", true)));
        if (!string.IsNullOrWhiteSpace(input.RegistrationNumber))
            hints.AddRange((await q.Where(d => d.RegistrationNumber == input.RegistrationNumber).ToListAsync(ct)).Select(d => new DuplicateHint(d, "نفس رقم السجل", "Same registration", true)));
        if (!string.IsNullOrWhiteSpace(input.NameAr) && input.NameAr.Length >= 4)
            hints.AddRange((await q.Where(d => d.NameAr == input.NameAr).Take(5).ToListAsync(ct)).Select(d => new DuplicateHint(d, "تشابه الاسم فقط", "Name similarity only", false)));
        return hints.DistinctBy(h => h.Donor.Id).ToList();
    }

    public async Task<Donor> GetAsync(int id, CancellationToken ct = default)
    {
        user.Require(Perm.DonorsView);
        return await db.Donors.AsNoTracking().Include(d => d.Representatives).FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw ProjectService.NotFound();
    }

    public async Task<int> SaveAsync(Donor input, CancellationToken ct = default)
    {
        user.Require(Perm.DonorsManage);
        if (input.Privacy != DonorPrivacy.Unknown)
        {
            if (string.IsNullOrWhiteSpace(input.NameAr)) throw new DomainException("الاسم مطلوب.", "Name is required.");
            if (string.IsNullOrWhiteSpace(input.Phone) && string.IsNullOrWhiteSpace(input.Email))
                throw new DomainException("أدخل هاتفًا أو بريدًا للتواصل وإرسال المستندات.", "Enter a phone or email for documents.");
        }
        if (input.Type == DonorType.Organization && string.IsNullOrWhiteSpace(input.OrganizationType))
            throw new DomainException("نوع الجهة مطلوب.", "Organization type is required.");
        Donor d;
        if (input.Id == 0)
        {
            d = new Donor { Number = await numbers.NextAsync("donor", "DNR", 6, false, ct) };
            db.Donors.Add(d);
        }
        else d = await db.Donors.Include(x => x.Representatives).FirstAsync(x => x.Id == input.Id, ct);
        d.Type = input.Type; d.NameAr = input.NameAr.Trim(); d.NameEn = input.NameEn?.Trim(); d.CountryCode = input.CountryCode; d.Phone = input.Phone?.Trim();
        d.Email = input.Email?.Trim(); d.Country = input.Country; d.Address = input.Address; d.NationalId = input.NationalId; d.OrganizationType = input.OrganizationType;
        d.RegistrationNumber = input.RegistrationNumber; d.MarketingConsent = input.MarketingConsent; d.Privacy = input.Privacy;
        // Representatives (organizations)
        d.Representatives.RemoveAll(r => input.Representatives.All(x => x.Id != r.Id));
        foreach (var r in input.Representatives)
        {
            var target = r.Id == 0 ? null : d.Representatives.FirstOrDefault(x => x.Id == r.Id);
            if (target is null) { target = new DonorRepresentative(); d.Representatives.Add(target); }
            target.Name = r.Name; target.Title = r.Title; target.Phone = r.Phone; target.Email = r.Email; target.CanSign = r.CanSign; target.AuthorizationReference = r.AuthorizationReference;
        }
        await db.SaveChangesAsync(ct);
        audit.Log("Collections", input.Id == 0 ? "DonorCreated" : "DonorUpdated", nameof(Donor), d.Id, reference: d.Number);
        await db.SaveChangesAsync(ct);
        return d.Id;
    }

    /// <summary>Merging requires a special permission and keeps history and links.</summary>
    public async Task MergeAsync(int sourceId, int targetId, string reason, CancellationToken ct = default)
    {
        user.Require(Perm.DonorsMerge);
        if (sourceId == targetId) throw new DomainException("اختر سجلين مختلفين.", "Choose two different records.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("سبب الدمج مطلوب.", "Merge reason is required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var source = await db.Donors.FirstAsync(d => d.Id == sourceId, ct);
        var target = await db.Donors.FirstAsync(d => d.Id == targetId, ct);
        var moved = new
        {
            Donations = await db.Donations.Where(d => d.DonorId == sourceId).ExecuteUpdateAsync(s => s.SetProperty(d => d.DonorId, targetId), ct),
            Pledges = await db.Pledges.Where(d => d.DonorId == sourceId).ExecuteUpdateAsync(s => s.SetProperty(d => d.DonorId, targetId), ct),
            Agreements = await db.Agreements.Where(d => d.DonorId == sourceId).ExecuteUpdateAsync(s => s.SetProperty(d => d.DonorId, targetId), ct),
            InKind = await db.InKindDonations.Where(d => d.DonorId == sourceId).ExecuteUpdateAsync(s => s.SetProperty(d => d.DonorId, targetId), ct),
        };
        source.MergedIntoDonorId = targetId; source.IsActive = false;
        target.Phone ??= source.Phone; target.Email ??= source.Email; target.NationalId ??= source.NationalId;
        audit.Log("Collections", "DonorsMerged", nameof(Donor), targetId, new { source.Number }, moved, reason, $"{source.Number} → {target.Number}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public record DonorProfile(Donor Donor, decimal ConfirmedTotal, int Transactions, List<Donation> Recent, List<Agreement> Agreements, List<Pledge> Pledges);

    public async Task<DonorProfile> ProfileAsync(int id, CancellationToken ct = default)
    {
        var d = await GetAsync(id, ct);
        var donations = db.Donations.AsNoTracking().Where(x => x.DonorId == id);
        return new DonorProfile(d,
            await donations.Where(x => x.Status == DonationStatus.Confirmed || x.Status == DonationStatus.PartiallyRefunded).SumAsync(x => (decimal?)(x.Total - x.RefundedAmount), ct) ?? 0,
            await donations.CountAsync(ct),
            await donations.Include(x => x.Receipt).OrderByDescending(x => x.Id).Take(10).ToListAsync(ct),
            await db.Agreements.AsNoTracking().Include(a => a.Installments).Where(a => a.DonorId == id).ToListAsync(ct),
            await db.Pledges.AsNoTracking().Where(p => p.DonorId == id).ToListAsync(ct));
    }
}

public record ProjectCard(ProjectCatalogEntry Entry, bool IsFavorite, int SortOrder, bool Eligible, decimal RemainingCap, string? ReasonAr, string? ReasonEn, decimal Raised);

/// <summary>Collector project cards (C01/C02): only finance-approved projects received from the Projects system.</summary>
public class CatalogService(IEidDb db, ICurrentUser user, EligibilityGateway gateway)
{
    public async Task<List<ProjectCard>> CardsAsync(string? search, int? centerId, int? programId, bool favoritesOnly, CancellationToken ct = default)
    {
        user.Require(Perm.CollectUse);
        var q = db.ProjectCatalog.AsNoTracking().AsQueryable();
        if (centerId is not null) q = q.Where(c => c.CenterId == centerId);
        if (programId is not null) q = q.Where(c => c.ProgramId == programId);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(c => c.NameAr.Contains(search) || c.NameEn.Contains(search) || c.ProgramNameAr.Contains(search) || c.ProgramNameEn.Contains(search) || c.ProjectCode.Contains(search));
        var entries = await q.ToListAsync(ct);
        var prefs = await db.CollectorPreferences.AsNoTracking().Where(p => p.UserId == user.UserId).ToDictionaryAsync(p => p.ProjectId, ct);
        var ids = entries.Select(e => e.ProjectId).ToList();
        var live = await gateway.CheckAsync(ids, ct);
        var raised = await db.DonationLines.Where(l => ids.Contains(l.ProjectId))
            .Join(db.Donations, l => l.DonationId, d => d.Id, (l, d) => new { l.ProjectId, d.Status, Net = l.Amount - l.RefundedAmount - l.ReallocatedAmount })
            .Where(x => x.Status == DonationStatus.Confirmed || x.Status == DonationStatus.PartiallyRefunded || x.Status == DonationStatus.Refunded)
            .GroupBy(x => x.ProjectId).Select(g => new { g.Key, Sum = g.Sum(x => x.Net) }).ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
        var cards = entries.Select(e =>
        {
            var p = prefs.GetValueOrDefault(e.ProjectId);
            var l = live.GetValueOrDefault(e.ProjectId);
            var eligible = e.IsAvailable && (l?.Eligible ?? false);
            return new ProjectCard(e, p?.IsFavorite ?? false, p?.SortOrder ?? int.MaxValue, eligible, l?.RemainingCap ?? 0,
                eligible ? null : (e.IsAvailable ? l?.ReasonAr : e.UnavailableReason), eligible ? null : (e.IsAvailable ? l?.ReasonEn : e.UnavailableReason), raised.GetValueOrDefault(e.ProjectId));
        });
        if (favoritesOnly) cards = cards.Where(c => c.IsFavorite);
        // Favorites first, then the collector's own order, then eligible, then name. New projects stay visible in the general list.
        return cards.OrderByDescending(c => c.IsFavorite).ThenBy(c => c.SortOrder).ThenByDescending(c => c.Eligible).ThenBy(c => c.Entry.NameAr).ToList();
    }

    /// <summary>Project options for pickers (pledges, agreements, in-kind, reallocation): the catalog received from Projects.</summary>
    public async Task<List<ProjectCatalogEntry>> EntriesAsync(bool availableOnly, CancellationToken ct = default)
    {
        if (!user.IsAuthenticated) throw new ForbiddenException(Perm.CollectUse);
        var q = db.ProjectCatalog.AsNoTracking();
        if (availableOnly) q = q.Where(c => c.IsAvailable);
        return await q.OrderBy(c => c.ProjectCode).ToListAsync(ct);
    }

    public async Task ToggleFavoriteAsync(int projectId, CancellationToken ct = default)
    {
        user.Require(Perm.CollectUse);
        var p = await db.CollectorPreferences.FirstOrDefaultAsync(x => x.UserId == user.UserId && x.ProjectId == projectId, ct);
        if (p is null) db.CollectorPreferences.Add(new CollectorPreference { UserId = user.UserId!, ProjectId = projectId, IsFavorite = true, SortOrder = int.MaxValue });
        else p.IsFavorite = !p.IsFavorite;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Saves the collector's own order (drag & drop or up/down buttons). Never affects colleagues.</summary>
    public async Task SaveOrderAsync(IReadOnlyList<int> orderedProjectIds, CancellationToken ct = default)
    {
        user.Require(Perm.CollectUse);
        var prefs = await db.CollectorPreferences.Where(x => x.UserId == user.UserId).ToListAsync(ct);
        for (var i = 0; i < orderedProjectIds.Count; i++)
        {
            var p = prefs.FirstOrDefault(x => x.ProjectId == orderedProjectIds[i]);
            if (p is null) { p = new CollectorPreference { UserId = user.UserId!, ProjectId = orderedProjectIds[i] }; db.CollectorPreferences.Add(p); }
            p.SortOrder = i;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task ResetOrderAsync(CancellationToken ct = default)
    {
        user.Require(Perm.CollectUse);
        await db.CollectorPreferences.Where(x => x.UserId == user.UserId).ExecuteUpdateAsync(s => s.SetProperty(x => x.SortOrder, int.MaxValue), ct);
    }
}
