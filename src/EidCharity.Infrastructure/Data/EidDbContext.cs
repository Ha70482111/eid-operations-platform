using EidCharity.Application;
using EidCharity.Domain;
using EidCharity.Domain.Collections;
using EidCharity.Domain.Integration;
using EidCharity.Domain.Projects;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EidCharity.Infrastructure.Data;

public class NumberSequence
{
    public string Key { get; set; } = "";
    public long Next { get; set; }
}

public class EidDbContext(DbContextOptions<EidDbContext> options) : IdentityDbContext<AppUser>(options), IEidDb
{
    public DbSet<Center> Centers => Set<Center>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RoleProfile> RoleProfiles => Set<RoleProfile>();
    public DbSet<ApprovalLimit> ApprovalLimits => Set<ApprovalLimit>();
    public DbSet<Delegation> Delegations => Set<Delegation>();
    public DbSet<SystemSetting> Settings => Set<SystemSetting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    public DbSet<CharityProgram> Programs => Set<CharityProgram>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectReviewEntry> ProjectReviews => Set<ProjectReviewEntry>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<FinancialItem> FinancialItems => Set<FinancialItem>();
    public DbSet<ProjectLicenseMapping> Mappings => Set<ProjectLicenseMapping>();
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
    public DbSet<BudgetAdjustment> BudgetAdjustments => Set<BudgetAdjustment>();
    public DbSet<DisbursementRequest> Disbursements => Set<DisbursementRequest>();
    public DbSet<DisbursementAction> DisbursementActions => Set<DisbursementAction>();
    public DbSet<DisbursementPayment> DisbursementPayments => Set<DisbursementPayment>();
    public DbSet<ExecutionMilestone> Milestones => Set<ExecutionMilestone>();
    public DbSet<BeneficiaryRecord> Beneficiaries => Set<BeneficiaryRecord>();
    public DbSet<DonorReport> DonorReports => Set<DonorReport>();
    public DbSet<CollectionResult> CollectionResults => Set<CollectionResult>();
    public DbSet<ProjectClosure> Closures => Set<ProjectClosure>();

    public DbSet<Donor> Donors => Set<Donor>();
    public DbSet<DonorRepresentative> DonorRepresentatives => Set<DonorRepresentative>();
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<DonationLine> DonationLines => Set<DonationLine>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<Pledge> Pledges => Set<Pledge>();
    public DbSet<Agreement> Agreements => Set<Agreement>();
    public DbSet<AgreementLine> AgreementLines => Set<AgreementLine>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<InKindDonation> InKindDonations => Set<InKindDonation>();
    public DbSet<CollectorCustody> Custodies => Set<CollectorCustody>();
    public DbSet<ReconciliationItem> ReconciliationItems => Set<ReconciliationItem>();
    public DbSet<RefundRequest> Refunds => Set<RefundRequest>();
    public DbSet<ReallocationRequest> Reallocations => Set<ReallocationRequest>();
    public DbSet<ReceiptCorrection> ReceiptCorrections => Set<ReceiptCorrection>();
    public DbSet<CollectorPreference> CollectorPreferences => Set<CollectorPreference>();
    public DbSet<ProjectCatalogEntry> ProjectCatalog => Set<ProjectCatalogEntry>();

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<InboxMessage> Inbox => Set<InboxMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<decimal>().HavePrecision(18, 2);
        b.Properties<string>().HaveMaxLength(400);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ---------- Identity (schema: identity) ----------
        b.Entity<AppUser>(e => { e.ToTable("Users", "identity"); e.Property(x => x.PasswordHash).HasMaxLength(1000); e.Property(x => x.SecurityStamp).HasMaxLength(1000); e.Property(x => x.ConcurrencyStamp).HasMaxLength(1000); });
        b.Entity<IdentityRole>().ToTable("Roles", "identity");
        b.Entity<IdentityUserRole<string>>().ToTable("UserRoles", "identity");
        b.Entity<IdentityUserClaim<string>>().ToTable("UserClaims", "identity");
        b.Entity<IdentityUserLogin<string>>().ToTable("UserLogins", "identity");
        b.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims", "identity");
        b.Entity<IdentityUserToken<string>>().ToTable("UserTokens", "identity");

        // ---------- Admin ----------
        Tracked<Center>(b, "Centers", "admin").HasIndex(x => x.Code).IsUnique();
        b.Entity<RolePermission>(e => { e.ToTable("RolePermissions", "admin"); e.HasIndex(x => new { x.RoleId, x.Permission }).IsUnique(); e.Property(x => x.Permission).HasMaxLength(100); });
        b.Entity<RoleProfile>(e => { e.ToTable("RoleProfiles", "admin"); e.HasKey(x => x.RoleId); });
        Tracked<ApprovalLimit>(b, "ApprovalLimits", "admin");
        Tracked<Delegation>(b, "Delegations", "admin");
        b.Entity<SystemSetting>(e => { e.ToTable("Settings", "admin"); e.HasKey(x => x.Key); e.Property(x => x.Key).HasMaxLength(100); });
        b.Entity<NumberSequence>(e => { e.ToTable("NumberSequences", "admin"); e.HasKey(x => x.Key); e.Property(x => x.Key).HasMaxLength(100); });
        Tracked<Attachment>(b, "Attachments", "admin").HasIndex(x => new { x.OwnerType, x.OwnerId });
        b.Entity<Notification>(e => { e.ToTable("Notifications", "admin"); e.HasIndex(x => x.DedupKey); e.HasIndex(x => new { x.UserId, x.IsRead }); });
        b.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs", "audit");
            e.Property(x => x.OldValues).HasMaxLength(-1); e.Property(x => x.NewValues).HasMaxLength(-1); e.Property(x => x.Reason).HasMaxLength(2000);
            e.HasIndex(x => x.At); e.HasIndex(x => new { x.EntityType, x.EntityId });
        });

        // ---------- Internal projects (schema: prj) ----------
        Tracked<CharityProgram>(b, "Programs", "prj", e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.Center).WithMany().HasForeignKey(x => x.CenterId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.DescriptionAr).HasMaxLength(4000); e.Property(x => x.DescriptionEn).HasMaxLength(4000);
        });
        Tracked<Project>(b, "Projects", "prj", e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => new { x.ApprovalStatus, x.CenterId });
            e.HasOne(x => x.Program).WithMany(x => x.Projects).HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Center).WithMany().HasForeignKey(x => x.CenterId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.ReviewHistory).WithOne().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            foreach (var p in new[] { nameof(Project.SummaryAr), nameof(Project.SummaryEn), nameof(Project.Objectives), nameof(Project.Activities), nameof(Project.ExecutionScope) })
                e.Property(p).HasMaxLength(4000);
            e.Property(x => x.PendingChangesJson).HasMaxLength(-1);
            e.Ignore(x => x.QuickAmountValues);
        });
        Tracked<ProjectReviewEntry>(b, "ProjectReviews", "prj").Property(x => x.Note).HasMaxLength(4000);
        Tracked<License>(b, "Licenses", "prj", e =>
        {
            e.HasIndex(x => x.Number);
            e.HasOne(x => x.Program).WithMany().HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Conditions).HasMaxLength(4000);
        });
        Tracked<FinancialItem>(b, "FinancialItems", "prj").HasIndex(x => x.Number).IsUnique();
        Tracked<ProjectLicenseMapping>(b, "ProjectLicenseMappings", "prj", e =>
        {
            e.HasOne(x => x.Project).WithMany(x => x.Mappings).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.License).WithMany().HasForeignKey(x => x.LicenseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.FinancialItem).WithMany().HasForeignKey(x => x.FinancialItemId).OnDelete(DeleteBehavior.Restrict);
        });
        Tracked<BudgetLine>(b, "BudgetLines", "prj").HasOne<Project>().WithMany(x => x.BudgetLines).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        Tracked<BudgetAdjustment>(b, "BudgetAdjustments", "prj");
        Tracked<DisbursementRequest>(b, "DisbursementRequests", "prj", e =>
        {
            e.HasIndex(x => x.Number).IsUnique();
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.BudgetLine).WithMany().HasForeignKey(x => x.BudgetLineId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Actions).WithOne().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Purpose).HasMaxLength(2000);
        });
        Tracked<DisbursementAction>(b, "DisbursementActions", "prj").Property(x => x.Note).HasMaxLength(2000);
        Tracked<DisbursementPayment>(b, "DisbursementPayments", "prj").HasIndex(x => x.Reference).IsUnique();
        Tracked<ExecutionMilestone>(b, "ExecutionMilestones", "prj");
        Tracked<BeneficiaryRecord>(b, "Beneficiaries", "prj");
        Tracked<DonorReport>(b, "DonorReports", "prj");
        Tracked<ProjectClosure>(b, "Closures", "prj").HasIndex(x => x.ProjectId).IsUnique();
        b.Entity<CollectionResult>(e =>
        {
            e.ToTable("CollectionResults", "prj");
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.HasIndex(x => new { x.ProjectId, x.Kind });
        });

        // ---------- Collections (schema: col) ----------
        Tracked<Donor>(b, "Donors", "col", e =>
        {
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.Phone); e.HasIndex(x => x.Email); e.HasIndex(x => x.NameAr);
            e.HasMany(x => x.Representatives).WithOne().HasForeignKey(x => x.DonorId).OnDelete(DeleteBehavior.Cascade);
        });
        Tracked<DonorRepresentative>(b, "DonorRepresentatives", "col");
        Tracked<Donation>(b, "Donations", "col", e =>
        {
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.HasIndex(x => new { x.CollectorUserId, x.Status, x.ConfirmedAt });
            e.HasOne(x => x.Donor).WithMany().HasForeignKey(x => x.DonorId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.DonationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Receipt).WithOne(x => x.Donation).HasForeignKey<Receipt>(x => x.DonationId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.NetAmount); e.Ignore(x => x.RefundableAmount);
        });
        Tracked<DonationLine>(b, "DonationLines", "col", e => { e.HasIndex(x => new { x.LicenseId, x.ProjectId }); e.HasIndex(x => x.ProjectId); });
        Tracked<Receipt>(b, "Receipts", "col", e =>
        {
            e.HasIndex(x => x.Number).IsUnique();
            // One active receipt per transaction; replacements reference the cancelled original.
            e.HasIndex(x => x.DonationId).IsUnique().HasFilter("[IsCancelled] = 0");
        });
        Tracked<Pledge>(b, "Pledges", "col").HasOne(x => x.Donor).WithMany().HasForeignKey(x => x.DonorId).OnDelete(DeleteBehavior.Restrict);
        Tracked<Agreement>(b, "Agreements", "col", e =>
        {
            e.HasIndex(x => x.Number).IsUnique();
            e.HasOne(x => x.Donor).WithMany().HasForeignKey(x => x.DonorId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.AgreementId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Installments).WithOne().HasForeignKey(x => x.AgreementId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Terms).HasMaxLength(-1);
            e.Ignore(x => x.Collected); e.Ignore(x => x.Remaining);
        });
        Tracked<AgreementLine>(b, "AgreementLines", "col");
        Tracked<Installment>(b, "Installments", "col");
        Tracked<InKindDonation>(b, "InKindDonations", "col").HasOne(x => x.Donor).WithMany().HasForeignKey(x => x.DonorId).OnDelete(DeleteBehavior.Restrict);
        Tracked<CollectorCustody>(b, "Custodies", "col").HasIndex(x => new { x.CollectorUserId, x.Status });
        Tracked<ReconciliationItem>(b, "ReconciliationItems", "col");
        Tracked<RefundRequest>(b, "RefundRequests", "col").HasOne(x => x.Donation).WithMany().HasForeignKey(x => x.DonationId).OnDelete(DeleteBehavior.Restrict);
        Tracked<ReallocationRequest>(b, "ReallocationRequests", "col");
        Tracked<ReceiptCorrection>(b, "ReceiptCorrections", "col");
        b.Entity<CollectorPreference>(e => { e.ToTable("CollectorPreferences", "col"); e.HasIndex(x => new { x.UserId, x.ProjectId }).IsUnique(); });
        b.Entity<ProjectCatalogEntry>(e => { e.ToTable("ProjectCatalog", "col"); e.HasKey(x => x.ProjectId); e.Property(x => x.ProjectId).ValueGeneratedNever(); });

        // ---------- Integration (schema: integ) ----------
        b.Entity<OutboxMessage>(e =>
        {
            e.ToTable("Outbox", "integ");
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.HasIndex(x => new { x.State, x.NextAttemptAt });
            e.Property(x => x.Payload).HasMaxLength(-1); e.Property(x => x.LastError).HasMaxLength(4000);
        });
        b.Entity<InboxMessage>(e => { e.ToTable("Inbox", "integ"); e.HasKey(x => x.IdempotencyKey); });
    }

    private static EntityTypeBuilder<T> Tracked<T>(ModelBuilder b, string table, string schema, Action<EntityTypeBuilder<T>>? configure = null) where T : Entity
    {
        var e = b.Entity<T>();
        e.ToTable(table, schema);
        e.Property(x => x.RowVersion).IsRowVersion();
        configure?.Invoke(e);
        return e;
    }
}
