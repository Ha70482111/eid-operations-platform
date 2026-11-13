using EidCharity.Domain;
using EidCharity.Domain.Collections;
using EidCharity.Domain.Integration;
using EidCharity.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EidCharity.Application;

public interface IEidDb
{
    DbSet<AppUser> Users { get; }
    DbSet<Center> Centers { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RoleProfile> RoleProfiles { get; }
    DbSet<ApprovalLimit> ApprovalLimits { get; }
    DbSet<Delegation> Delegations { get; }
    DbSet<SystemSetting> Settings { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Attachment> Attachments { get; }
    DbSet<Notification> Notifications { get; }

    DbSet<CharityProgram> Programs { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProjectReviewEntry> ProjectReviews { get; }
    DbSet<License> Licenses { get; }
    DbSet<FinancialItem> FinancialItems { get; }
    DbSet<ProjectLicenseMapping> Mappings { get; }
    DbSet<BudgetLine> BudgetLines { get; }
    DbSet<BudgetAdjustment> BudgetAdjustments { get; }
    DbSet<DisbursementRequest> Disbursements { get; }
    DbSet<DisbursementAction> DisbursementActions { get; }
    DbSet<DisbursementPayment> DisbursementPayments { get; }
    DbSet<ExecutionMilestone> Milestones { get; }
    DbSet<BeneficiaryRecord> Beneficiaries { get; }
    DbSet<DonorReport> DonorReports { get; }
    DbSet<CollectionResult> CollectionResults { get; }
    DbSet<ProjectClosure> Closures { get; }

    DbSet<Donor> Donors { get; }
    DbSet<DonorRepresentative> DonorRepresentatives { get; }
    DbSet<Donation> Donations { get; }
    DbSet<DonationLine> DonationLines { get; }
    DbSet<Receipt> Receipts { get; }
    DbSet<Pledge> Pledges { get; }
    DbSet<Agreement> Agreements { get; }
    DbSet<AgreementLine> AgreementLines { get; }
    DbSet<Installment> Installments { get; }
    DbSet<InKindDonation> InKindDonations { get; }
    DbSet<CollectorCustody> Custodies { get; }
    DbSet<ReconciliationItem> ReconciliationItems { get; }
    DbSet<RefundRequest> Refunds { get; }
    DbSet<ReallocationRequest> Reallocations { get; }
    DbSet<ReceiptCorrection> ReceiptCorrections { get; }
    DbSet<CollectorPreference> CollectorPreferences { get; }
    DbSet<ProjectCatalogEntry> ProjectCatalog { get; }

    DbSet<OutboxMessage> Outbox { get; }
    DbSet<InboxMessage> Inbox { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface ICurrentUser
{
    string? UserId { get; }
    string UserName { get; }
    string DisplayName { get; }
    int? CenterId { get; }
    bool IsAuthenticated { get; }
    bool Has(string permission);
    IReadOnlySet<string> Permissions { get; }
}

public static class CurrentUserExtensions
{
    public static void Require(this ICurrentUser u, string permission)
    {
        if (!u.Has(permission)) throw new ForbiddenException(permission);
    }

    public static void RequireAny(this ICurrentUser u, params string[] permissions)
    {
        if (!permissions.Any(u.Has)) throw new ForbiddenException(permissions[0]);
    }

    public static bool AllCenters(this ICurrentUser u) => u.Has(Perm.ScopeAllCenters);

    /// <summary>Center scope: users only see their center unless they hold Scope.AllCenters.</summary>
    public static bool CanSeeCenter(this ICurrentUser u, int centerId) => u.AllCenters() || u.CenterId == centerId;

    public static IQueryable<T> ScopeToCenter<T>(this IQueryable<T> q, ICurrentUser u, System.Linq.Expressions.Expression<Func<T, int>> center)
    {
        if (u.AllCenters()) return q;
        var cid = u.CenterId ?? -1;
        var p = center.Parameters[0];
        var body = System.Linq.Expressions.Expression.Equal(center.Body, System.Linq.Expressions.Expression.Constant(cid));
        return q.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, p));
    }
}

public interface INumberGenerator
{
    /// <summary>Concurrency-safe document numbers, e.g. RCP-2026-000123.</summary>
    Task<string> NextAsync(string key, string prefix, int width = 6, bool withYear = true, CancellationToken ct = default);
}

public interface IFileStore
{
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct = default);
    Task<Stream?> OpenAsync(string storagePath, CancellationToken ct = default);
}

/// <summary>Serialises concurrent confirmations on the same license cap (SQL Server sp_getapplock).</summary>
public interface IResourceLock
{
    Task AcquireAsync(string resource, CancellationToken ct = default);
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int Pages => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

public static class PagingExtensions
{
    public static async Task<PagedResult<T>> PageAsync<T>(this IQueryable<T> q, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T>(items, total, page, pageSize);
    }
}
