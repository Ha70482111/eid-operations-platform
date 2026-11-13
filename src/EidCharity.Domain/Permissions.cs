namespace EidCharity.Domain;

public record PermissionDef(string Key, string Group, string Ar, string En, bool Sensitive = false);

/// <summary>
/// Fine-grained permissions. Every sensitive operation in the analysis has its own permission,
/// enforced on the server in the service layer (not only by hiding buttons).
/// </summary>
public static class Perm
{
    // ---------- Internal projects ----------
    public const string ProgramsView = "Projects.Programs.View";
    public const string ProgramsManage = "Projects.Programs.Manage";
    public const string ProjectsView = "Projects.Projects.View";
    public const string ProjectsCreateLicensed = "Projects.Projects.CreateLicensed";
    public const string ProjectsEdit = "Projects.Projects.Edit";
    public const string ProjectsSubmit = "Projects.Projects.Submit";
    public const string LicensesView = "Projects.Licenses.View";
    public const string LicensesManage = "Projects.Licenses.Manage";
    public const string FinanceReview = "Projects.Finance.Review";
    public const string FinanceItemsManage = "Projects.Finance.Items";
    public const string FinanceMap = "Projects.Finance.Map";
    public const string FinanceApproveEligibility = "Projects.Finance.ApproveEligibility";
    public const string FinanceSuspendEligibility = "Projects.Finance.SuspendEligibility";
    public const string BudgetView = "Projects.Budget.View";
    public const string BudgetManage = "Projects.Budget.Manage";
    public const string BudgetApproveAdjustment = "Projects.Budget.ApproveAdjustment";
    public const string DisbCreate = "Projects.Disbursement.Create";
    public const string DisbReview = "Projects.Disbursement.Review";
    public const string DisbFinanceCheck = "Projects.Disbursement.FinanceCheck";
    public const string DisbApprove = "Projects.Disbursement.Approve";
    public const string DisbExecute = "Projects.Disbursement.Execute";
    public const string DisbCancel = "Projects.Disbursement.Cancel";
    public const string ExecutionManage = "Projects.Execution.Manage";
    public const string BeneficiariesManage = "Projects.Beneficiaries.Manage";
    public const string BeneficiariesViewSensitive = "Projects.Beneficiaries.ViewSensitive";
    public const string ProjectSuspend = "Projects.Projects.Suspend";
    public const string ProjectClose = "Projects.Closure.Close";
    public const string ProjectReopen = "Projects.Closure.Reopen";
    public const string ProjectsReports = "Projects.Reports.View";
    public const string ProjectsExport = "Projects.Reports.Export";
    public const string RevenueView = "Projects.Revenue.View";

    // ---------- Collections ----------
    public const string CollectUse = "Collections.Workspace.Use";
    public const string DonorsView = "Collections.Donors.View";
    public const string DonorsManage = "Collections.Donors.Manage";
    public const string DonorsMerge = "Collections.Donors.Merge";
    public const string DonationsConfirm = "Collections.Donations.Confirm";
    public const string DonationsViewAll = "Collections.Donations.ViewAll";
    public const string DonationsVerify = "Collections.Donations.VerifyPending";
    public const string ReceiptsReprint = "Collections.Receipts.Reprint";
    public const string ReceiptsCorrect = "Collections.Receipts.Correct";
    public const string ReceiptsCorrectApprove = "Collections.Receipts.CorrectApprove";
    public const string PledgesManage = "Collections.Pledges.Manage";
    public const string AgreementsManage = "Collections.Agreements.Manage";
    public const string AgreementsApprove = "Collections.Agreements.Approve";
    public const string AgreementsReschedule = "Collections.Agreements.Reschedule";
    public const string InKindManage = "Collections.InKind.Manage";
    public const string InKindApproveValuation = "Collections.InKind.ApproveValuation";
    public const string CustodyHandover = "Collections.Custody.Handover";
    public const string CustodyConfirm = "Collections.Custody.Confirm";
    public const string ReconciliationManage = "Collections.Reconciliation.Manage";
    public const string RefundsRequest = "Collections.Refunds.Request";
    public const string RefundsApprove = "Collections.Refunds.Approve";
    public const string RefundsExecute = "Collections.Refunds.Execute";
    public const string ReallocationRequest = "Collections.Reallocation.Request";
    public const string ReallocationApprove = "Collections.Reallocation.Approve";
    public const string CollectionsReports = "Collections.Reports.View";
    public const string CollectionsExport = "Collections.Reports.Export";

    // ---------- Administration ----------
    public const string AdminUsers = "Admin.Users";
    public const string AdminRoles = "Admin.Roles";
    public const string AdminCenters = "Admin.Centers";
    public const string AdminSettings = "Admin.Settings";
    public const string AdminApprovalMatrix = "Admin.ApprovalMatrix";
    public const string AdminDelegations = "Admin.Delegations";
    public const string AuditView = "Admin.Audit.View";
    public const string IntegrationMonitor = "Admin.Integration.Monitor";
    public const string ScopeAllCenters = "Scope.AllCenters";

    public static readonly IReadOnlyList<PermissionDef> All =
    [
        new(ProgramsView, "projects", "عرض البرامج", "View programs"),
        new(ProgramsManage, "projects", "إنشاء وتعديل البرامج", "Manage programs"),
        new(ProjectsView, "projects", "عرض المشاريع", "View projects"),
        new(ProjectsCreateLicensed, "projects", "إدخال مشروع معتمد من هيئة تنظيم الأعمال الخيرية", "Enter RACA-licensed project", true),
        new(ProjectsEdit, "projects", "تعديل بيانات المشروع", "Edit project data", true),
        new(ProjectsSubmit, "projects", "إرسال المشروع للمالية", "Submit project to finance"),
        new(LicensesView, "projects", "عرض التراخيص", "View licenses"),
        new(LicensesManage, "projects", "إدارة التراخيص", "Manage licenses", true),
        new(RevenueView, "projects", "عرض الإيرادات ونتائج التحصيل", "View revenue & collection results"),
        new(FinanceReview, "finance", "المراجعة المالية للمشاريع", "Finance review of projects"),
        new(FinanceItemsManage, "finance", "إنشاء وإدارة البنود المالية", "Manage financial items", true),
        new(FinanceMap, "finance", "ربط المشروع بالبند والتراخيص", "Map project to item & licenses", true),
        new(FinanceApproveEligibility, "finance", "اعتماد الإتاحة للتحصيل", "Approve collection eligibility", true),
        new(FinanceSuspendEligibility, "finance", "إيقاف الإتاحة", "Suspend eligibility", true),
        new(BudgetView, "budget", "عرض الميزانيات", "View budgets"),
        new(BudgetManage, "budget", "إعداد الميزانية وبنودها", "Manage budgets", true),
        new(BudgetApproveAdjustment, "budget", "اعتماد تعديل أو نقل بنود الميزانية", "Approve budget adjustments", true),
        new(DisbCreate, "disbursement", "إنشاء طلب صرف", "Create disbursement request"),
        new(DisbReview, "disbursement", "مراجعة المختص لطلب الصرف", "Specialist review of disbursement"),
        new(DisbFinanceCheck, "disbursement", "تحقق المالية من التمويل والسيولة", "Finance funding check"),
        new(DisbApprove, "disbursement", "اعتماد الصرف", "Approve disbursement", true),
        new(DisbExecute, "disbursement", "تنفيذ الدفع", "Execute payment", true),
        new(DisbCancel, "disbursement", "إلغاء طلب وتحرير الحجز", "Cancel request & release reserve", true),
        new(ExecutionManage, "execution", "متابعة التنفيذ والمراحل", "Manage execution & milestones"),
        new(BeneficiariesManage, "execution", "إدارة المستفيدين", "Manage beneficiaries"),
        new(BeneficiariesViewSensitive, "execution", "الاطلاع على بيانات المستفيدين الحساسة", "View sensitive beneficiary data", true),
        new(ProjectSuspend, "execution", "إيقاف أو إلغاء المشروع", "Suspend or cancel project", true),
        new(ProjectClose, "execution", "إقفال المشروع", "Close project", true),
        new(ProjectReopen, "execution", "إعادة فتح مشروع مقفل", "Reopen closed project", true),
        new(ProjectsReports, "reports", "تقارير المشاريع", "Project reports"),
        new(ProjectsExport, "reports", "تصدير تقارير المشاريع", "Export project reports", true),

        new(CollectUse, "collections", "استخدام واجهة المحصّل", "Use collector workspace"),
        new(DonorsView, "collections", "عرض المتبرعين", "View donors"),
        new(DonorsManage, "collections", "إضافة وتعديل المتبرعين", "Manage donors"),
        new(DonorsMerge, "collections", "دمج المتبرعين", "Merge donors", true),
        new(DonationsConfirm, "collections", "تأكيد التبرع وإصدار السند", "Confirm donation & issue receipt", true),
        new(DonationsViewAll, "collections", "عرض كل العمليات (ليس عملياتي فقط)", "View all transactions"),
        new(DonationsVerify, "collections", "تأكيد استلام العمليات المعلقة", "Verify pending transactions", true),
        new(ReceiptsReprint, "collections", "إعادة طباعة وإرسال السند", "Reprint & send receipt"),
        new(ReceiptsCorrect, "collections", "طلب تصحيح أو إلغاء سند", "Request receipt correction", true),
        new(ReceiptsCorrectApprove, "collections", "اعتماد تصحيح أو إلغاء سند", "Approve receipt correction", true),
        new(PledgesManage, "agreements", "إدارة التعهدات", "Manage pledges"),
        new(AgreementsManage, "agreements", "إنشاء وتعديل الاتفاقيات", "Manage agreements"),
        new(AgreementsApprove, "agreements", "اعتماد وتفعيل الاتفاقيات", "Approve agreements", true),
        new(AgreementsReschedule, "agreements", "إعادة جدولة الدفعات", "Reschedule installments", true),
        new(InKindManage, "inkind", "تسجيل التبرعات العينية", "Record in-kind donations"),
        new(InKindApproveValuation, "inkind", "اعتماد تقييم العيني", "Approve in-kind valuation", true),
        new(CustodyHandover, "custody", "تسليم العهدة النقدية", "Hand over cash custody"),
        new(CustodyConfirm, "custody", "تأكيد استلام العهدة وتسويتها", "Confirm & settle custody", true),
        new(ReconciliationManage, "custody", "مطابقة القنوات", "Channel reconciliation", true),
        new(RefundsRequest, "refunds", "طلب استرداد", "Request refund"),
        new(RefundsApprove, "refunds", "اعتماد الاسترداد", "Approve refund", true),
        new(RefundsExecute, "refunds", "تنفيذ الاسترداد", "Execute refund", true),
        new(ReallocationRequest, "refunds", "طلب إعادة تخصيص", "Request reallocation"),
        new(ReallocationApprove, "refunds", "اعتماد إعادة التخصيص", "Approve reallocation", true),
        new(CollectionsReports, "reports", "تقارير التحصيل", "Collection reports"),
        new(CollectionsExport, "reports", "تصدير تقارير التحصيل", "Export collection reports", true),

        new(AdminUsers, "admin", "إدارة المستخدمين", "Manage users", true),
        new(AdminRoles, "admin", "إدارة الأدوار والصلاحيات", "Manage roles & permissions", true),
        new(AdminCenters, "admin", "إدارة المراكز", "Manage centers"),
        new(AdminSettings, "admin", "إعدادات النظام", "System settings", true),
        new(AdminApprovalMatrix, "admin", "مصفوفة حدود الاعتماد", "Approval matrix", true),
        new(AdminDelegations, "admin", "التفويضات", "Delegations", true),
        new(AuditView, "admin", "سجل الإجراءات", "Audit log"),
        new(IntegrationMonitor, "admin", "متابعة الربط بين النظامين وإعادة الإرسال", "Integration monitor & retry", true),
        new(ScopeAllCenters, "admin", "نطاق كل المراكز", "Scope: all centers", true),
    ];

    public static readonly IReadOnlyDictionary<string, (string Ar, string En)> Groups = new Dictionary<string, (string, string)>
    {
        ["projects"] = ("البرامج والمشاريع", "Programs & projects"),
        ["finance"] = ("المراجعة المالية والإتاحة", "Finance review & eligibility"),
        ["budget"] = ("الميزانية", "Budget"),
        ["disbursement"] = ("الصرف", "Disbursement"),
        ["execution"] = ("التنفيذ والإقفال", "Execution & closure"),
        ["collections"] = ("التحصيل والسندات", "Collections & receipts"),
        ["agreements"] = ("التعهدات والاتفاقيات", "Pledges & agreements"),
        ["inkind"] = ("العيني", "In-kind"),
        ["custody"] = ("العهد والمطابقة", "Custody & reconciliation"),
        ["refunds"] = ("الاسترداد وإعادة التخصيص", "Refunds & reallocation"),
        ["reports"] = ("التقارير", "Reports"),
        ["admin"] = ("الإدارة", "Administration"),
    };
}

public record RoleSeed(string Name, string Ar, string En, string DescAr, string DescEn, string[] Permissions);

/// <summary>Default roles. Admins can change every role's permissions from the Admin area.</summary>
public static class DefaultRoles
{
    public const string SystemAdmin = "SystemAdmin";
    public const string CenterOfficer = "CenterOfficer";
    public const string ProjectFollowUp = "ProjectFollowUp";
    public const string FinanceReviewer = "FinanceReviewer";
    public const string FinanceManager = "FinanceManager";
    public const string Accountant = "Accountant";
    public const string Collector = "Collector";
    public const string CollectionSupervisor = "CollectionSupervisor";
    public const string Management = "Management";
    public const string Auditor = "Auditor";

    public static IReadOnlyList<RoleSeed> All =>
    [
        new(SystemAdmin, "مدير النظام", "System administrator", "كل الصلاحيات", "All permissions", Perm.All.Select(p => p.Key).ToArray()),
        new(CenterOfficer, "مسؤول المركز", "Center officer", "يسجل البرامج والمشاريع المرخصة ويرسلها للمالية", "Registers licensed programs/projects and submits to finance",
        [
            Perm.ProgramsView, Perm.ProgramsManage, Perm.ProjectsView, Perm.ProjectsCreateLicensed, Perm.ProjectsEdit, Perm.ProjectsSubmit,
            Perm.LicensesView, Perm.LicensesManage, Perm.BudgetView, Perm.BudgetManage, Perm.DisbCreate, Perm.DisbReview, Perm.ExecutionManage,
            Perm.BeneficiariesManage, Perm.RevenueView, Perm.ProjectsReports
        ]),
        new(ProjectFollowUp, "موظف متابعة", "Follow-up officer", "يتابع التنفيذ والمستفيدين ويقدم طلبات الصرف", "Tracks execution, beneficiaries, raises disbursements",
        [
            Perm.ProgramsView, Perm.ProjectsView, Perm.LicensesView, Perm.BudgetView, Perm.DisbCreate, Perm.ExecutionManage, Perm.BeneficiariesManage, Perm.RevenueView
        ]),
        new(FinanceReviewer, "مراجع مالي", "Finance reviewer", "يراجع ويربط البند والتراخيص", "Reviews and maps item & licenses",
        [
            Perm.ProgramsView, Perm.ProjectsView, Perm.LicensesView, Perm.FinanceReview, Perm.FinanceItemsManage, Perm.FinanceMap,
            Perm.BudgetView, Perm.DisbFinanceCheck, Perm.RevenueView, Perm.ProjectsReports, Perm.ScopeAllCenters, Perm.DonationsVerify,
            Perm.ReconciliationManage, Perm.DonationsViewAll
        ]),
        new(FinanceManager, "مدير الإدارة المالية", "Finance manager", "يعتمد الإتاحة والصرف والاسترداد", "Approves eligibility, disbursements, refunds",
        [
            Perm.ProgramsView, Perm.ProjectsView, Perm.LicensesView, Perm.FinanceReview, Perm.FinanceItemsManage, Perm.FinanceMap,
            Perm.FinanceApproveEligibility, Perm.FinanceSuspendEligibility, Perm.BudgetView, Perm.BudgetApproveAdjustment,
            Perm.DisbFinanceCheck, Perm.DisbApprove, Perm.DisbCancel, Perm.ProjectClose, Perm.ProjectReopen, Perm.ProjectSuspend,
            Perm.RevenueView, Perm.ProjectsReports, Perm.ProjectsExport, Perm.ScopeAllCenters,
            Perm.RefundsApprove, Perm.ReallocationApprove, Perm.ReceiptsCorrectApprove, Perm.AgreementsApprove, Perm.InKindApproveValuation,
            Perm.CollectionsReports, Perm.CollectionsExport, Perm.DonationsViewAll, Perm.AuditView
        ]),
        new(Accountant, "محاسب / أمين صندوق", "Accountant / treasurer", "ينفذ المدفوعات ويستلم العهد", "Executes payments and receives custody",
        [
            Perm.ProjectsView, Perm.BudgetView, Perm.DisbExecute, Perm.CustodyConfirm, Perm.ReconciliationManage, Perm.RefundsExecute,
            Perm.DonationsVerify, Perm.DonationsViewAll, Perm.ScopeAllCenters, Perm.RevenueView
        ]),
        new(Collector, "محصّل", "Collector", "يستقبل التبرعات ويصدر السندات", "Receives donations and issues receipts",
        [
            Perm.CollectUse, Perm.DonorsView, Perm.DonorsManage, Perm.DonationsConfirm, Perm.ReceiptsReprint, Perm.ReceiptsCorrect,
            Perm.PledgesManage, Perm.AgreementsManage, Perm.InKindManage, Perm.CustodyHandover, Perm.RefundsRequest, Perm.ReallocationRequest
        ]),
        new(CollectionSupervisor, "مشرف التحصيل", "Collection supervisor", "يشرف على المحصلين والمطابقة والدمج", "Supervises collectors, reconciliation, merges",
        [
            Perm.CollectUse, Perm.DonorsView, Perm.DonorsManage, Perm.DonorsMerge, Perm.DonationsConfirm, Perm.DonationsViewAll, Perm.DonationsVerify,
            Perm.ReceiptsReprint, Perm.ReceiptsCorrect, Perm.PledgesManage, Perm.AgreementsManage, Perm.AgreementsReschedule, Perm.InKindManage,
            Perm.CustodyHandover, Perm.CustodyConfirm, Perm.ReconciliationManage, Perm.RefundsRequest, Perm.ReallocationRequest,
            Perm.CollectionsReports, Perm.CollectionsExport
        ]),
        new(Management, "الإدارة العليا", "Executive management", "اطلاع على اللوحات والتقارير", "Dashboards & reports (read-only)",
        [
            Perm.ProgramsView, Perm.ProjectsView, Perm.LicensesView, Perm.BudgetView, Perm.RevenueView, Perm.ProjectsReports, Perm.CollectionsReports,
            Perm.DonationsViewAll, Perm.ScopeAllCenters
        ]),
        new(Auditor, "الرقابة والتدقيق", "Internal audit", "اطلاع وسجل الإجراءات", "Read-only with audit log",
        [
            Perm.ProgramsView, Perm.ProjectsView, Perm.LicensesView, Perm.BudgetView, Perm.RevenueView, Perm.ProjectsReports, Perm.CollectionsReports,
            Perm.DonationsViewAll, Perm.DonorsView, Perm.AuditView, Perm.IntegrationMonitor, Perm.ScopeAllCenters
        ]),
    ];
}
