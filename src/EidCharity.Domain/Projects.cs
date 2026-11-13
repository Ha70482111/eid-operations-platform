namespace EidCharity.Domain.Projects;

// ---------------- Independent statuses (analysis §2.4) ----------------
public enum ApprovalStatus { Draft = 0, AwaitingFinance = 1, UnderFinanceReview = 2, ReturnedToCenter = 3, FinanciallyApproved = 4, Rejected = 5, Suspended = 6 }
public enum LicenseStatus { NotStarted = 0, Active = 1, Expired = 2, Suspended = 3 }
public enum CollectionStatus { NotAvailable = 0, Available = 1, Suspended = 2, CapReached = 3, Closed = 4 }
public enum ExecutionStatus { NotStarted = 0, InProgress = 1, Stopped = 2, Completed = 3, Cancelled = 4, Closed = 5 }
public enum SyncStatus { NotSent = 0, Pending = 1, Succeeded = 2, Failed = 3 }
public enum ProjectKind { Child = 1, Standalone = 2 }
public enum LicenseScope { Project = 1, ProgramAndProjects = 2 }

public enum ReviewAction { Submitted = 1, Resubmitted = 2, ReviewStarted = 3, ReturnedToCenter = 4, Mapped = 5, Approved = 6, Rejected = 7, Suspended = 8, Resumed = 9, SubstantiveChange = 10, Closed = 11, Reopened = 12, Cancelled = 13 }

public class CharityProgram : Entity, IBilingual
{
    public string Code { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string DescriptionAr { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public int CenterId { get; set; }
    public Center? Center { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    /// <summary>Approved program budget distributed to projects; a reserve may remain unallocated.</summary>
    public decimal ApprovedBudget { get; set; }
    public decimal UnallocatedReserve { get; set; }
    /// <summary>True when the program was auto-created to wrap a single standalone project.</summary>
    public bool IsStandaloneWrapper { get; set; }
    public bool IsClosed { get; set; }
    public List<Project> Projects { get; set; } = [];
}

public class Project : Entity, IBilingual
{
    public string Code { get; set; } = "";
    public int ProgramId { get; set; }
    public CharityProgram? Program { get; set; }
    public ProjectKind Kind { get; set; } = ProjectKind.Child;
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string SummaryAr { get; set; } = "";
    public string SummaryEn { get; set; } = "";
    public string Objectives { get; set; } = "";
    public string Activities { get; set; } = "";
    public string ExecutionScope { get; set; } = "";
    public string Location { get; set; } = "";
    public int CenterId { get; set; }
    public Center? Center { get; set; }
    public string? FollowUpUserId { get; set; }
    public DateOnly? ExecutionStart { get; set; }
    public DateOnly? ExecutionEnd { get; set; }
    public string BeneficiaryCategory { get; set; } = "";
    public int TargetBeneficiaries { get; set; }
    public string BeneficiaryUnit { get; set; } = "شخص";
    public string Currency { get; set; } = Money.DefaultCurrency;
    public decimal ExecutionCost { get; set; }
    /// <summary>Licensed collection cap for this project (distinct from execution cost).</summary>
    public decimal CollectionCap { get; set; }
    public string StudyReference { get; set; } = "";
    public string Visual { get; set; } = "water";
    public string QuickAmounts { get; set; } = "50,100,500,1000";

    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;
    public CollectionStatus CollectionStatus { get; set; } = CollectionStatus.NotAvailable;
    public ExecutionStatus ExecutionStatus { get; set; } = ExecutionStatus.NotStarted;
    public string? StatusReason { get; set; }
    /// <summary>Incremented each time a new financial mapping version is approved and published.</summary>
    public int MappingVersion { get; set; }
    public SyncStatus MappingSyncStatus { get; set; }
    public DateTime? MappingSyncedAt { get; set; }
    public string? MappingSyncError { get; set; }
    public string? ReviewerId { get; set; }
    public string? ApprovedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    /// <summary>Substantive changes requested after approval; applied only when finance re-approves.</summary>
    public string? PendingChangesJson { get; set; }

    public List<ProjectLicenseMapping> Mappings { get; set; } = [];
    public List<BudgetLine> BudgetLines { get; set; } = [];
    public List<ProjectReviewEntry> ReviewHistory { get; set; } = [];

    public bool IsClosed => ExecutionStatus == ExecutionStatus.Closed;
    public bool IsEditableByCenter => ApprovalStatus is ApprovalStatus.Draft or ApprovalStatus.ReturnedToCenter;
    public IEnumerable<int> QuickAmountValues =>
        QuickAmounts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var v) ? v : 0).Where(v => v > 0);
}

public class ProjectReviewEntry : Entity
{
    public int ProjectId { get; set; }
    public ReviewAction Action { get; set; }
    public string? Note { get; set; }
    public string? ByUserName { get; set; }
    public ApprovalStatus FromStatus { get; set; }
    public ApprovalStatus ToStatus { get; set; }
}

public class License : Entity
{
    public string Number { get; set; } = "";
    public string Issuer { get; set; } = "هيئة تنظيم الأعمال الخيرية";
    public DateOnly IssueDate { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }
    public DateOnly CollectionFrom { get; set; }
    public DateOnly CollectionTo { get; set; }
    public LicenseScope Scope { get; set; } = LicenseScope.Project;
    /// <summary>When scope = program, the program covered (the shared cap is watched once for all its projects).</summary>
    public int? ProgramId { get; set; }
    public CharityProgram? Program { get; set; }
    public decimal Cap { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    public string Channels { get; set; } = "";
    public string Purposes { get; set; } = "";
    public string Conditions { get; set; } = "";
    public bool IsSuspended { get; set; }
    public string? SuspensionReason { get; set; }
    /// <summary>Renewals keep the previous version.</summary>
    public int? PreviousLicenseId { get; set; }
    public int VersionNo { get; set; } = 1;
    public bool IsSuperseded { get; set; }

    public LicenseStatus StatusAt(DateOnly day) =>
        IsSuspended ? LicenseStatus.Suspended
        : day < ValidFrom ? LicenseStatus.NotStarted
        : day > ValidTo ? LicenseStatus.Expired
        : LicenseStatus.Active;

    public bool AllowsCollectionOn(DateOnly day) =>
        StatusAt(day) == LicenseStatus.Active && day >= CollectionFrom && day <= CollectionTo;
}

/// <summary>Financial item for collection (independent from budget lines; not a GL account unless mapped).</summary>
public class FinancialItem : Entity, IBilingual
{
    public string Number { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string? GlMapping { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum MappingStatus { Draft = 0, Active = 1, Suspended = 2, Expired = 3, Superseded = 4 }

/// <summary>Project ↔ financial item ↔ license link, each with its own scope, period, cap and status.</summary>
public class ProjectLicenseMapping : Entity
{
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    /// <summary>Set by finance; the center only proposes the license link.</summary>
    public int? FinancialItemId { get; set; }
    public FinancialItem? FinancialItem { get; set; }
    public int LicenseId { get; set; }
    public License? License { get; set; }
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    /// <summary>Optional sub-cap for this project under this license (0 = license cap only).</summary>
    public decimal LinkCap { get; set; }
    public MappingStatus Status { get; set; } = MappingStatus.Draft;
    public int MappingVersion { get; set; }
    public string? ScopeNote { get; set; }
}

public class BudgetLine : Entity, IBilingual
{
    public int ProjectId { get; set; }
    public string Code { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
}

public enum AdjustmentStatus { Submitted = 1, Approved = 2, Rejected = 3 }

public class BudgetAdjustment : Entity
{
    public int ProjectId { get; set; }
    public int? FromLineId { get; set; }
    public int? ToLineId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    public AdjustmentStatus Status { get; set; } = AdjustmentStatus.Submitted;
    public string? DecidedById { get; set; }
    public string? DecisionNote { get; set; }
}

public enum DisbursementStatus { Draft = 0, Submitted = 1, UnderReview = 2, ReturnedForEdit = 3, Rejected = 4, ApprovedReserved = 5, PartiallyExecuted = 6, FullyExecuted = 7, Cancelled = 8 }
public enum DisbursementStep { SpecialistReview = 1, FinanceCheck = 2, Approval = 3, Execution = 4, Cancellation = 5, Release = 6 }

public class DisbursementRequest : Entity
{
    public string Number { get; set; } = "";
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int BudgetLineId { get; set; }
    public BudgetLine? BudgetLine { get; set; }
    public string Purpose { get; set; } = "";
    public string PayeeName { get; set; } = "";
    public string PayeeDetails { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    public string PaymentMethod { get; set; } = "BankTransfer";
    public string ExpenseType { get; set; } = "Operational";
    public bool IsAdvance { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? InvoiceReference { get; set; }
    public DisbursementStatus Status { get; set; } = DisbursementStatus.Draft;
    public bool SpecialistReviewed { get; set; }
    public bool FinanceChecked { get; set; }
    public decimal ReservedAmount { get; set; }
    public decimal ExecutedAmount { get; set; }
    public decimal ReleasedAmount { get; set; }
    public string RequestedById { get; set; } = "";
    public string? ApprovedById { get; set; }
    public List<DisbursementAction> Actions { get; set; } = [];
    public List<DisbursementPayment> Payments { get; set; } = [];
}

public class DisbursementAction : Entity
{
    public int RequestId { get; set; }
    public DisbursementStep Step { get; set; }
    public string Decision { get; set; } = "";
    public string? Note { get; set; }
    public string? ByUserName { get; set; }
}

public class DisbursementPayment : Entity
{
    public int RequestId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaidOn { get; set; }
    public string Reference { get; set; } = "";
    public string? DocumentReference { get; set; }
}

public enum MilestoneStatus { Planned = 0, InProgress = 1, Delayed = 2, Done = 3, Cancelled = 4 }

public class ExecutionMilestone : Entity, IBilingual
{
    public int ProjectId { get; set; }
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public DateOnly? PlannedStart { get; set; }
    public DateOnly? PlannedEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }
    public string? Responsible { get; set; }
    public string Indicator { get; set; } = "";
    public decimal TargetValue { get; set; }
    public decimal ActualValue { get; set; }
    public MilestoneStatus Status { get; set; }
    public string? DelayReason { get; set; }
    public string? CorrectiveAction { get; set; }
}

public class BeneficiaryRecord : Entity
{
    public int ProjectId { get; set; }
    public string Category { get; set; } = "";
    public string Unit { get; set; } = "شخص";
    /// <summary>Unique beneficiaries (de-duplicated).</summary>
    public int UniqueCount { get; set; }
    /// <summary>Number of benefit occurrences (may exceed unique count).</summary>
    public int Occurrences { get; set; }
    public DateOnly? PeriodFrom { get; set; }
    public DateOnly? PeriodTo { get; set; }
    public string Location { get; set; } = "";
    public string? Notes { get; set; }
}

public enum DonorReportStatus { Planned = 0, Preparing = 1, UnderReview = 2, Sent = 3 }

public class DonorReport : Entity
{
    public int ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string? DonorName { get; set; }
    public DateOnly DueDate { get; set; }
    public string? Responsible { get; set; }
    public DonorReportStatus Status { get; set; }
}

/// <summary>Collection results received from the Collections system (idempotent by key).</summary>
public enum CollectionResultKind { Pledge = 1, Pending = 2, Confirmed = 3, Refund = 4, ReallocationOut = 5, ReallocationIn = 6, InKind = 7, PendingCancelled = 8, Correction = 9 }

public class CollectionResult
{
    public long Id { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
    public DateTime OccurredAt { get; set; }
    public int ProjectId { get; set; }
    public int? FinancialItemId { get; set; }
    public int? LicenseId { get; set; }
    public CollectionResultKind Kind { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    public string? TransactionNumber { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? AgreementNumber { get; set; }
    public int MappingVersion { get; set; }
}

public class ProjectClosure : Entity
{
    public int ProjectId { get; set; }
    public bool ExecutionDocumented { get; set; }
    public bool ExpensesSettled { get; set; }
    public bool OpenRequestsResolved { get; set; }
    public bool PledgesResolved { get; set; }
    public bool DifferencesSettled { get; set; }
    public bool FinalReportApproved { get; set; }
    public string BalanceTreatment { get; set; } = "";
    public string Notes { get; set; } = "";
    public bool IsClosed { get; set; }
}
