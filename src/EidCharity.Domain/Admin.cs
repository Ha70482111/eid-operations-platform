namespace EidCharity.Domain;

public class RolePermission
{
    public int Id { get; set; }
    public string RoleId { get; set; } = "";
    public string Permission { get; set; } = "";
}

/// <summary>Extra metadata for a role (Identity roles only carry a name).</summary>
public class RoleProfile
{
    public string RoleId { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string DescriptionAr { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public bool IsSystem { get; set; }
}

public enum ApprovalModule { Disbursement = 1, Refund = 2, Reallocation = 3, BudgetAdjustment = 4, ReceiptCorrection = 5 }

/// <summary>Institutional approval matrix: who may approve how much. No amounts are hard-coded in code.</summary>
public class ApprovalLimit : Entity
{
    public string RoleId { get; set; } = "";
    public ApprovalModule Module { get; set; }
    public int? CenterId { get; set; }
    public string? ExpenseType { get; set; }
    public decimal MaxAmount { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    public bool IsActive { get; set; } = true;
}

/// <summary>Approval delegation with a fixed period.</summary>
public class Delegation : Entity
{
    public string FromUserId { get; set; } = "";
    public string ToUserId { get; set; } = "";
    public ApprovalModule Module { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string Reason { get; set; } = "";
    public bool IsRevoked { get; set; }
}

public class SystemSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string DescriptionAr { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
}

public static class SettingKeys
{
    public const string LicenseExpiryAlertDays = "License.ExpiryAlertDays";
    public const string CapAlertPercents = "License.CapAlertPercents";
    public const string PendingHoldMinutes = "Collections.PendingHoldMinutes";
    public const string InstallmentReminderDays = "Agreements.ReminderDays";
    public const string ReceiptPrefix = "Receipts.Prefix";
    public const string OrganizationNameAr = "Org.NameAr";
    public const string OrganizationNameEn = "Org.NameEn";
    public const string OrganizationPoBox = "Org.POBox";
    public const string OrganizationPhone = "Org.Phone";
    public const string DefaultQuickAmounts = "Collections.DefaultQuickAmounts";
    public const string CustodyMaxCash = "Collections.CustodyMaxCash";
}

/// <summary>Append-only action log. No UI path updates or deletes it.</summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime At { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string Module { get; set; } = "";
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? Reason { get; set; }
    public string? Reference { get; set; }
    public string? IpAddress { get; set; }
}

public enum AttachmentCategory { Study = 1, License = 2, Approval = 3, Agreement = 4, Invoice = 5, Quotation = 6, ReceivingMinutes = 7, ExecutionReport = 8, Photo = 9, Authorization = 10, TransferProof = 11, FinalReport = 12, Other = 99 }

public class Attachment : Entity
{
    public string OwnerType { get; set; } = "";
    public int OwnerId { get; set; }
    public AttachmentCategory Category { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public string StoragePath { get; set; } = "";
    public int Version { get; set; } = 1;
    public bool IsSensitive { get; set; }
    public string? Reference { get; set; }
}

public class Notification
{
    public long Id { get; set; }
    public DateTime At { get; set; }
    public string? UserId { get; set; }
    /// <summary>When set, everyone holding this permission sees it.</summary>
    public string? Permission { get; set; }
    public int? CenterId { get; set; }
    public string TitleAr { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public string? Link { get; set; }
    public string Severity { get; set; } = "info";
    public string? DedupKey { get; set; }
    public bool IsRead { get; set; }
}
