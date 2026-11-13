namespace EidCharity.Domain.Collections;

public enum DonorType { Individual = 1, Organization = 2 }
public enum DonorPrivacy { Normal = 0, KnownButHidden = 1, Unknown = 2 }

public class Donor : Entity
{
    public string Number { get; set; } = "";
    public DonorType Type { get; set; } = DonorType.Individual;
    public string NameAr { get; set; } = "";
    public string? NameEn { get; set; }
    public string CountryCode { get; set; } = "+974";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Country { get; set; } = "قطر";
    public string? Address { get; set; }
    public string? NationalId { get; set; }
    public string? OrganizationType { get; set; }
    public string? RegistrationNumber { get; set; }
    /// <summary>Marketing consent is separate from transactional documents.</summary>
    public bool MarketingConsent { get; set; }
    public DonorPrivacy Privacy { get; set; }
    public int? MergedIntoDonorId { get; set; }
    public bool IsActive { get; set; } = true;
    public List<DonorRepresentative> Representatives { get; set; } = [];

    public string DisplayName(bool ar) => Privacy switch
    {
        DonorPrivacy.Unknown => ar ? "متبرع مجهول" : "Unknown donor",
        DonorPrivacy.KnownButHidden => ar ? "فاعل خير" : "Anonymous benefactor",
        _ => ar || string.IsNullOrWhiteSpace(NameEn) ? NameAr : NameEn!
    };
}

public class DonorRepresentative : Entity
{
    public int DonorId { get; set; }
    public string Name { get; set; } = "";
    public string? Title { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool CanSign { get; set; }
    public string? AuthorizationReference { get; set; }
}

public enum PaymentMethod { Cash = 1, BankTransfer = 2, Pos = 3, Online = 4 }
public enum DonationStatus { Draft = 0, PendingVerification = 1, Confirmed = 2, Failed = 3, Cancelled = 4, PartiallyRefunded = 5, Refunded = 6 }
public enum SettlementStatus { NotApplicable = 0, Unsettled = 1, Settled = 2, Difference = 3 }

/// <summary>A donation transaction. Always allocated to projects (never to a program).</summary>
public class Donation : Entity
{
    public string Number { get; set; } = "";
    public int? DonorId { get; set; }
    public Donor? Donor { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public DonationStatus Status { get; set; } = DonationStatus.Draft;
    public decimal Total { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    public decimal ChannelFee { get; set; }
    public decimal RefundedAmount { get; set; }
    public string CollectorUserId { get; set; } = "";
    public string? CollectorName { get; set; }
    public int CenterId { get; set; }
    public string CollectionPoint { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedById { get; set; }
    public string? PaymentReference { get; set; }
    public string? BankName { get; set; }
    public DateOnly? TransferDate { get; set; }
    public string? PosTerminal { get; set; }
    public string? PosResult { get; set; }
    public SettlementStatus Settlement { get; set; }
    /// <summary>Client-generated key; a repeated click or notification never creates a second transaction.</summary>
    public string IdempotencyKey { get; set; } = "";
    public int? AgreementId { get; set; }
    public int? InstallmentId { get; set; }
    public int? CustodyId { get; set; }
    public string? Notes { get; set; }
    public string? FailureReason { get; set; }
    public List<DonationLine> Lines { get; set; } = [];
    public Receipt? Receipt { get; set; }

    public decimal NetAmount => Total - RefundedAmount;
    public decimal RefundableAmount => Status is DonationStatus.Confirmed or DonationStatus.PartiallyRefunded ? Total - RefundedAmount : 0;
}

/// <summary>Allocation line: project + financial item + eligible license, with the mapping version used.</summary>
public class DonationLine : Entity
{
    public int DonationId { get; set; }
    public int ProjectId { get; set; }
    public int ProgramId { get; set; }
    public int FinancialItemId { get; set; }
    public int LicenseId { get; set; }
    public int MappingVersion { get; set; }
    public string ProjectCode { get; set; } = "";
    public string ProjectNameAr { get; set; } = "";
    public string ProjectNameEn { get; set; } = "";
    public string FinancialItemNumber { get; set; } = "";
    public string LicenseNumber { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal ReallocatedAmount { get; set; }
    public string? Restrictions { get; set; }
}

public class Receipt : Entity
{
    public string Number { get; set; } = "";
    public int DonationId { get; set; }
    public Donation? Donation { get; set; }
    public DateTime IssuedAt { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    public bool IsCancelled { get; set; }
    public string? CancelReason { get; set; }
    public int? ReplacesReceiptId { get; set; }
    public int PrintCount { get; set; }
    public DateTime? LastPrintedAt { get; set; }
    public string? AgreementNumber { get; set; }
}

public enum PledgeStatus { Open = 0, PartiallyFulfilled = 1, Fulfilled = 2, Cancelled = 3, ConvertedToAgreement = 4 }

public class Pledge : Entity
{
    public string Number { get; set; } = "";
    public int DonorId { get; set; }
    public Donor? Donor { get; set; }
    public int ProjectId { get; set; }
    public decimal Amount { get; set; }
    public decimal FulfilledAmount { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    public DateOnly? ExpectedDate { get; set; }
    public PledgeStatus Status { get; set; }
    public string? Notes { get; set; }
}

public enum AgreementStatus { Draft = 0, UnderReview = 1, AwaitingSignature = 2, Active = 3, Amended = 4, Completed = 5, Cancelled = 6 }

public class Agreement : Entity
{
    public string Number { get; set; } = "";
    public int DonorId { get; set; }
    public Donor? Donor { get; set; }
    public int? RepresentativeId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    public AgreementStatus Status { get; set; }
    public string Terms { get; set; } = "";
    public string ReportingTerms { get; set; } = "";
    public int Version { get; set; } = 1;
    public string? AmendmentReason { get; set; }
    public string? ApprovedById { get; set; }
    public DateTime? SignedAt { get; set; }
    public string? SignedCopyReference { get; set; }
    public List<AgreementLine> Lines { get; set; } = [];
    public List<Installment> Installments { get; set; } = [];

    public decimal Collected => Installments.Sum(i => i.PaidAmount);
    public decimal Remaining => Total - Collected;
}

public class AgreementLine : Entity
{
    public int AgreementId { get; set; }
    public int ProjectId { get; set; }
    public string ProjectNameAr { get; set; } = "";
    public string ProjectNameEn { get; set; } = "";
    public decimal Amount { get; set; }
}

public enum InstallmentStatus { Due = 0, PartiallyPaid = 1, Paid = 2, Overdue = 3, Cancelled = 4 }

public class Installment : Entity
{
    public int AgreementId { get; set; }
    public int Seq { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public InstallmentStatus Status { get; set; }
    public DateOnly? PreviousDueDate { get; set; }
    public string? RescheduleReason { get; set; }

    public InstallmentStatus Compute(DateOnly today) =>
        Status == InstallmentStatus.Cancelled ? Status
        : PaidAmount >= Amount ? InstallmentStatus.Paid
        : DueDate < today ? InstallmentStatus.Overdue
        : PaidAmount > 0 ? InstallmentStatus.PartiallyPaid
        : InstallmentStatus.Due;
}

public enum InKindStatus { Received = 0, ValuationPending = 1, Valued = 2, InStock = 3, Distributed = 4 }

public class InKindDonation : Entity
{
    public string Number { get; set; } = "";
    public int? DonorId { get; set; }
    public Donor? Donor { get; set; }
    public int ProjectId { get; set; }
    public string ProjectNameAr { get; set; } = "";
    public string ItemDescription { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
    public string Condition { get; set; } = "جديد";
    public DateTime ReceivedAt { get; set; }
    public string Location { get; set; } = "";
    public string ReceivedBy { get; set; } = "";
    public DateOnly? ExpiryDate { get; set; }
    public string? StorageNotes { get; set; }
    /// <summary>Estimated value never enters cash liquidity.</summary>
    public decimal EstimatedValue { get; set; }
    public string? ValuationBasis { get; set; }
    public bool ValuationApproved { get; set; }
    public decimal DistributedQuantity { get; set; }
    public InKindStatus Status { get; set; }
}

public enum CustodyStatus { Open = 0, HandedOver = 1, Closed = 2 }
public enum HandoverType { Treasury = 1, BankDeposit = 2 }

public class CollectorCustody : Entity
{
    public string Number { get; set; } = "";
    public string CollectorUserId { get; set; } = "";
    public string CollectorName { get; set; } = "";
    public DateTime OpenedAt { get; set; }
    public CustodyStatus Status { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal HandedOverAmount { get; set; }
    public decimal Difference { get; set; }
    public string? DifferenceReason { get; set; }
    public HandoverType? HandoverType { get; set; }
    public string? DepositReference { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public string? ConfirmedById { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}

public enum ReconChannel { Receipts = 1, Custody = 2, BankTransfer = 3, Pos = 4, Online = 5, Fees = 6, Chargebacks = 7 }
public enum ReconStatus { Unmatched = 0, Matched = 1, Difference = 2, Resolved = 3 }

public class ReconciliationItem : Entity
{
    public ReconChannel Channel { get; set; }
    public int? DonationId { get; set; }
    public string ExternalReference { get; set; } = "";
    public decimal ExpectedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Fee { get; set; }
    public ReconStatus Status { get; set; }
    public string? Responsible { get; set; }
    public string? Note { get; set; }
}

public enum RequestStatus { Submitted = 1, UnderReview = 2, Approved = 3, Rejected = 4, Executed = 5, Cancelled = 6 }

public class RefundRequest : Entity
{
    public string Number { get; set; } = "";
    public int DonationId { get; set; }
    public Donation? Donation { get; set; }
    public int? DonationLineId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    public bool IsChargeback { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Submitted;
    public string RequestedById { get; set; } = "";
    public string? DecidedById { get; set; }
    public string? DecisionNote { get; set; }
    public string? ExecutionReference { get; set; }
    public DateTime? ExecutedAt { get; set; }
}

public class ReallocationRequest : Entity
{
    public string Number { get; set; } = "";
    public int DonationLineId { get; set; }
    public int FromProjectId { get; set; }
    public int ToProjectId { get; set; }
    public decimal Amount { get; set; }
    public string Justification { get; set; } = "";
    public bool DonorConsentObtained { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Submitted;
    public string RequestedById { get; set; } = "";
    public string? DecidedById { get; set; }
    public string? DecisionNote { get; set; }
}

public enum CorrectionType { Correct = 1, Cancel = 2 }

public class ReceiptCorrection : Entity
{
    public int ReceiptId { get; set; }
    public CorrectionType Type { get; set; }
    public string Reason { get; set; } = "";
    public string? CorrectedDonorNote { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Submitted;
    public string RequestedById { get; set; } = "";
    public string? DecidedById { get; set; }
}

/// <summary>Per-collector card ordering/favorites. Never affects colleagues or permissions.</summary>
public class CollectorPreference
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int ProjectId { get; set; }
    public int SortOrder { get; set; }
    public bool IsFavorite { get; set; }
}

/// <summary>Read-model replica of eligible projects published by the Projects system.</summary>
public class ProjectCatalogEntry
{
    public int ProjectId { get; set; }
    public int ProgramId { get; set; }
    public int CenterId { get; set; }
    public string ProjectCode { get; set; } = "";
    public string ProgramCode { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string ProgramNameAr { get; set; } = "";
    public string ProgramNameEn { get; set; } = "";
    public string CenterNameAr { get; set; } = "";
    public string CenterNameEn { get; set; } = "";
    public string FinancialItemNumber { get; set; } = "";
    public string FinancialItemNameAr { get; set; } = "";
    public string Visual { get; set; } = "water";
    public string QuickAmounts { get; set; } = "50,100,500,1000";
    public decimal CollectionCap { get; set; }
    public bool IsAvailable { get; set; }
    public string? UnavailableReason { get; set; }
    public int MappingVersion { get; set; }
    public DateTime UpdatedAt { get; set; }
}
