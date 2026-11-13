using EidCharity.Domain;
using EidCharity.Domain.Collections;
using EidCharity.Domain.Integration;
using EidCharity.Domain.Projects;

namespace EidCharity.Web.Services;

/// <summary>Every state is shown with text + icon dot + color (never color alone).</summary>
public static class StatusText
{
    public static Tone Of(ApprovalStatus s) => s switch
    {
        ApprovalStatus.Draft => new("neutral", "مسودة", "Draft"),
        ApprovalStatus.AwaitingFinance => new("warning", "بانتظار المالية", "Awaiting finance"),
        ApprovalStatus.UnderFinanceReview => new("warning", "قيد المراجعة المالية", "Under finance review"),
        ApprovalStatus.ReturnedToCenter => new("danger", "معاد للمركز", "Returned to center"),
        ApprovalStatus.FinanciallyApproved => new("success", "معتمد ماليًا", "Financially approved"),
        ApprovalStatus.Rejected => new("danger", "مرفوض", "Rejected"),
        ApprovalStatus.Suspended => new("danger", "موقوف", "Suspended"),
        _ => new("neutral", s.ToString(), s.ToString()),
    };

    public static Tone Of(CollectionStatus s) => s switch
    {
        CollectionStatus.NotAvailable => new("neutral", "غير متاح", "Not available"),
        CollectionStatus.Available => new("success", "متاح للتحصيل", "Available"),
        CollectionStatus.Suspended => new("danger", "موقوف", "Suspended"),
        CollectionStatus.CapReached => new("warning", "بلغ السقف", "Cap reached"),
        CollectionStatus.Closed => new("neutral", "مغلق", "Closed"),
        _ => new("neutral", s.ToString(), s.ToString()),
    };

    public static Tone Of(ExecutionStatus s) => s switch
    {
        ExecutionStatus.NotStarted => new("neutral", "لم يبدأ", "Not started"),
        ExecutionStatus.InProgress => new("success", "جارٍ", "In progress"),
        ExecutionStatus.Stopped => new("danger", "متوقف", "Stopped"),
        ExecutionStatus.Completed => new("success", "مكتمل", "Completed"),
        ExecutionStatus.Cancelled => new("danger", "ملغى", "Cancelled"),
        ExecutionStatus.Closed => new("neutral", "مقفل", "Closed"),
        _ => new("neutral", s.ToString(), s.ToString()),
    };

    public static Tone Of(LicenseStatus s) => s switch
    {
        LicenseStatus.NotStarted => new("neutral", "لم يبدأ", "Not started"),
        LicenseStatus.Active => new("success", "ساري", "Active"),
        LicenseStatus.Expired => new("danger", "منتهي", "Expired"),
        LicenseStatus.Suspended => new("danger", "موقوف", "Suspended"),
        _ => new("neutral", s.ToString(), s.ToString()),
    };

    public static Tone Of(SyncStatus s) => s switch
    {
        SyncStatus.NotSent => new("neutral", "لم يُرسل", "Not sent"),
        SyncStatus.Pending => new("warning", "بانتظار الإرسال", "Pending"),
        SyncStatus.Succeeded => new("success", "الربط ناجح", "Synced"),
        SyncStatus.Failed => new("danger", "فشل الربط", "Sync failed"),
        _ => new("neutral", s.ToString(), s.ToString()),
    };

    public static Tone Of(MappingStatus s) => s switch
    {
        MappingStatus.Draft => new("warning", "مقترح", "Proposed"),
        MappingStatus.Active => new("success", "نشط", "Active"),
        MappingStatus.Suspended => new("danger", "موقوف", "Suspended"),
        MappingStatus.Expired => new("neutral", "منتهي", "Expired"),
        MappingStatus.Superseded => new("neutral", "نسخة سابقة", "Superseded"),
        _ => new("neutral", s.ToString(), s.ToString()),
    };

    public static Tone Of(DisbursementStatus s) => s switch
    {
        DisbursementStatus.Draft => new("neutral", "مسودة", "Draft"),
        DisbursementStatus.Submitted => new("warning", "مقدم", "Submitted"),
        DisbursementStatus.UnderReview => new("warning", "قيد المراجعة", "Under review"),
        DisbursementStatus.ReturnedForEdit => new("danger", "معاد للتعديل", "Returned"),
        DisbursementStatus.Rejected => new("danger", "مرفوض", "Rejected"),
        DisbursementStatus.ApprovedReserved => new("success", "معتمد ومحجوز", "Approved & reserved"),
        DisbursementStatus.PartiallyExecuted => new("warning", "منفذ جزئيًا", "Partially executed"),
        DisbursementStatus.FullyExecuted => new("success", "منفذ كليًا", "Fully executed"),
        DisbursementStatus.Cancelled => new("neutral", "ملغى", "Cancelled"),
        _ => new("neutral", s.ToString(), s.ToString()),
    };

    public static Tone Of(DonationStatus s) => s switch
    {
        DonationStatus.Draft => new("neutral", "مسودة", "Draft"),
        DonationStatus.PendingVerification => new("warning", "قيد التحقق", "Pending verification"),
        DonationStatus.Confirmed => new("success", "مؤكدة", "Confirmed"),
        DonationStatus.Failed => new("danger", "فاشلة", "Failed"),
        DonationStatus.Cancelled => new("neutral", "ملغاة", "Cancelled"),
        DonationStatus.PartiallyRefunded => new("warning", "مستردة جزئيًا", "Partially refunded"),
        DonationStatus.Refunded => new("danger", "مستردة كليًا", "Refunded"),
        _ => new("neutral", s.ToString(), s.ToString()),
    };

    public static Tone Of(AgreementStatus s) => s switch
    {
        AgreementStatus.Draft => new("neutral", "مسودة", "Draft"),
        AgreementStatus.UnderReview => new("warning", "قيد المراجعة", "Under review"),
        AgreementStatus.AwaitingSignature => new("warning", "بانتظار التوقيع", "Awaiting signature"),
        AgreementStatus.Active => new("success", "مفعلة", "Active"),
        AgreementStatus.Amended => new("warning", "معدلة", "Amended"),
        AgreementStatus.Completed => new("success", "مكتملة", "Completed"),
        AgreementStatus.Cancelled => new("neutral", "ملغاة", "Cancelled"),
        _ => new("neutral", s.ToString(), s.ToString()),
    };

    public static Tone Of(InstallmentStatus s) => s switch
    {
        InstallmentStatus.Due => new("neutral", "مستحقة", "Due"),
        InstallmentStatus.PartiallyPaid => new("warning", "مسددة جزئيًا", "Partially paid"),
        InstallmentStatus.Paid => new("success", "مسددة", "Paid"),
        InstallmentStatus.Overdue => new("danger", "متأخرة", "Overdue"),
        InstallmentStatus.Cancelled => new("neutral", "ملغاة", "Cancelled"),
        _ => new("neutral", s.ToString(), s.ToString()),
    };

    public static Tone Of(RequestStatus s) => s switch
    {
        RequestStatus.Submitted => new("warning", "مقدم", "Submitted"),
        RequestStatus.UnderReview => new("warning", "قيد المراجعة", "Under review"),
        RequestStatus.Approved => new("success", "معتمد", "Approved"),
        RequestStatus.Rejected => new("danger", "مرفوض", "Rejected"),
        RequestStatus.Executed => new("success", "منفذ", "Executed"),
        RequestStatus.Cancelled => new("neutral", "ملغى", "Cancelled"),
        _ => new("neutral", s.ToString(), s.ToString()),
    };

    public static Tone Of(CustodyStatus s) => s switch
    {
        CustodyStatus.Open => new("warning", "مفتوحة", "Open"),
        CustodyStatus.HandedOver => new("warning", "مسلّمة بانتظار التأكيد", "Handed over"),
        CustodyStatus.Closed => new("success", "مقفلة", "Closed"),
        _ => new("neutral", s.ToString(), s.ToString()),
    };

    public static Tone Of(ReconStatus s) => s switch
    {
        ReconStatus.Unmatched => new("warning", "غير مطابق", "Unmatched"),
        ReconStatus.Matched => new("success", "مطابق", "Matched"),
        ReconStatus.Difference => new("danger", "فرق", "Difference"),
        ReconStatus.Resolved => new("success", "تمت المعالجة", "Resolved"),
        _ => new("neutral", s.ToString(), s.ToString()),
    };

    public static Tone Of(OutboxState s) => s switch
    {
        OutboxState.Pending => new("warning", "بانتظار الإرسال", "Pending"),
        OutboxState.Succeeded => new("success", "ناجح", "Succeeded"),
        OutboxState.Failed => new("danger", "فاشل", "Failed"),
        _ => new("neutral", s.ToString(), s.ToString()),
    };

    public static Tone Of(PaymentMethod m) => m switch
    {
        PaymentMethod.Cash => new("neutral", "نقدي", "Cash"),
        PaymentMethod.BankTransfer => new("neutral", "تحويل بنكي", "Bank transfer"),
        PaymentMethod.Pos => new("neutral", "نقاط بيع", "POS"),
        PaymentMethod.Online => new("neutral", "قناة إلكترونية", "Online"),
        _ => new("neutral", m.ToString(), m.ToString()),
    };

    public static Tone Of(MilestoneStatus s) => s switch
    {
        MilestoneStatus.Planned => new("neutral", "مخطط", "Planned"),
        MilestoneStatus.InProgress => new("warning", "جارٍ", "In progress"),
        MilestoneStatus.Delayed => new("danger", "متأخر", "Delayed"),
        MilestoneStatus.Done => new("success", "منجز", "Done"),
        MilestoneStatus.Cancelled => new("neutral", "ملغى", "Cancelled"),
        _ => new("neutral", s.ToString(), s.ToString()),
    };

    public static (string Ar, string En) Module(ApprovalModule m) => m switch
    {
        ApprovalModule.Disbursement => ("الصرف", "Disbursement"),
        ApprovalModule.Refund => ("الاسترداد", "Refund"),
        ApprovalModule.Reallocation => ("إعادة التخصيص", "Reallocation"),
        ApprovalModule.BudgetAdjustment => ("تعديل الميزانية", "Budget adjustment"),
        ApprovalModule.ReceiptCorrection => ("تصحيح السند", "Receipt correction"),
        _ => (m.ToString(), m.ToString()),
    };
}
