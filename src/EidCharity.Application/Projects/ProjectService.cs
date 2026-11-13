using System.Text.Json;
using EidCharity.Application.Common;
using EidCharity.Domain;
using EidCharity.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace EidCharity.Application.Projects;

public record ProgramRow(int Id, string Code, string NameAr, string NameEn, string CenterAr, string CenterEn, int CenterId, bool IsStandalone,
    int ProjectCount, decimal ApprovedBudget, decimal AllocatedToProjects, decimal UnallocatedReserve, decimal Confirmed, string Currency, bool IsClosed);

public class ProgramInput
{
    public int Id { get; set; }
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string DescriptionAr { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public int CenterId { get; set; }
    public decimal ApprovedBudget { get; set; }
    public decimal UnallocatedReserve { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
}

public class ProjectInput
{
    public int Id { get; set; }
    /// <summary>Null + Standalone kind ⇒ a program with the same name is created automatically.</summary>
    public int? ProgramId { get; set; }
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
    public string? FollowUpUserId { get; set; }
    public DateOnly? ExecutionStart { get; set; }
    public DateOnly? ExecutionEnd { get; set; }
    public string BeneficiaryCategory { get; set; } = "";
    public int TargetBeneficiaries { get; set; }
    public string BeneficiaryUnit { get; set; } = "شخص";
    public string Currency { get; set; } = Money.DefaultCurrency;
    public decimal ExecutionCost { get; set; }
    public decimal CollectionCap { get; set; }
    public string StudyReference { get; set; } = "";
    public string Visual { get; set; } = "water";
    public string QuickAmounts { get; set; } = "50,100,500,1000";
    public List<int> LicenseIds { get; set; } = [];
}

public record ProjectRow(int Id, string Code, string NameAr, string NameEn, int ProgramId, string ProgramCode, string ProgramNameAr, string ProgramNameEn,
    string CenterAr, string CenterEn, ApprovalStatus Approval, CollectionStatus Collection, ExecutionStatus Execution, SyncStatus Sync,
    decimal ExecutionCost, decimal CollectionCap, string Currency, DateTime? UpdatedAt, DateTime CreatedAt, bool HasPendingChanges);

public class ProjectService(IEidDb db, ICurrentUser user, INumberGenerator numbers, AuditService audit)
{
    // ---------------- Programs ----------------
    public async Task<List<ProgramRow>> ListProgramsAsync(string? search = null, CancellationToken ct = default)
    {
        user.Require(Perm.ProgramsView);
        var q = db.Programs.AsNoTracking().ScopeToCenter(user, p => p.CenterId);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(p => p.NameAr.Contains(search) || p.NameEn.Contains(search) || p.Code.Contains(search)
                             || p.Projects.Any(x => x.NameAr.Contains(search) || x.NameEn.Contains(search) || x.Code.Contains(search)));
        var rows = await q.OrderBy(p => p.Code).Select(p => new ProgramRow(p.Id, p.Code, p.NameAr, p.NameEn, p.Center!.NameAr, p.Center.NameEn, p.CenterId, p.IsStandaloneWrapper,
                p.Projects.Count, p.ApprovedBudget, p.Projects.Sum(x => x.ExecutionCost), p.UnallocatedReserve, 0m, p.Currency, p.IsClosed))
            .ToListAsync(ct);
        // Program totals come only from its projects (no direct collection on a program; each line counted once).
        var ids = rows.Select(r => r.Id).ToList();
        var totals = await ConfirmedByProject().Join(db.Projects, c => c.ProjectId, p => p.Id, (c, p) => new { p.ProgramId, c.Amount })
            .Where(x => ids.Contains(x.ProgramId)).GroupBy(x => x.ProgramId).Select(g => new { g.Key, Sum = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
        return rows.Select(r => r with { Confirmed = totals.GetValueOrDefault(r.Id) }).ToList();
    }

    private IQueryable<CollectionResult> ConfirmedByProject() =>
        db.CollectionResults.Where(c => c.Kind == CollectionResultKind.Confirmed || c.Kind == CollectionResultKind.Refund
                                        || c.Kind == CollectionResultKind.ReallocationIn || c.Kind == CollectionResultKind.ReallocationOut || c.Kind == CollectionResultKind.Correction);

    public async Task<CharityProgram> GetProgramAsync(int id, CancellationToken ct = default)
    {
        user.Require(Perm.ProgramsView);
        var p = await db.Programs.Include(x => x.Center).Include(x => x.Projects).ThenInclude(x => x.Center)
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFound();
        if (!user.CanSeeCenter(p.CenterId)) throw new ForbiddenException(Perm.ScopeAllCenters);
        return p;
    }

    public async Task<int> SaveProgramAsync(ProgramInput input, CancellationToken ct = default)
    {
        user.Require(Perm.ProgramsManage);
        if (string.IsNullOrWhiteSpace(input.NameAr) || string.IsNullOrWhiteSpace(input.NameEn))
            throw new DomainException("اسم البرنامج مطلوب باللغتين.", "Program name is required in both languages.");
        if (!user.CanSeeCenter(input.CenterId)) throw new ForbiddenException(Perm.ScopeAllCenters);
        CharityProgram p;
        if (input.Id == 0)
        {
            p = new CharityProgram { Code = await numbers.NextAsync("program", "PRG", 4, false, ct) };
            db.Programs.Add(p);
        }
        else
        {
            p = await db.Programs.Include(x => x.Projects).FirstOrDefaultAsync(x => x.Id == input.Id, ct) ?? throw NotFound();
            var allocated = p.Projects.Sum(x => x.ExecutionCost);
            // Distribution + reserve may not exceed the program budget.
            if (input.ApprovedBudget > 0 && allocated + input.UnallocatedReserve > input.ApprovedBudget)
                throw new DomainException($"مجموع توزيعات المشاريع ({allocated:N0}) والاحتياطي يتجاوز ميزانية البرنامج.",
                    $"Project allocations ({allocated:N0}) plus reserve exceed the program budget.");
        }
        p.NameAr = input.NameAr.Trim(); p.NameEn = input.NameEn.Trim();
        p.DescriptionAr = input.DescriptionAr; p.DescriptionEn = input.DescriptionEn;
        p.CenterId = input.CenterId; p.ApprovedBudget = input.ApprovedBudget; p.UnallocatedReserve = input.UnallocatedReserve; p.Currency = input.Currency;
        await db.SaveChangesAsync(ct);
        audit.Log("Projects", input.Id == 0 ? "ProgramCreated" : "ProgramUpdated", nameof(CharityProgram), p.Id, newValues: input, reference: p.Code);
        await db.SaveChangesAsync(ct);
        return p.Id;
    }

    // ---------------- Projects ----------------
    public async Task<PagedResult<ProjectRow>> ListProjectsAsync(string? search, ApprovalStatus? approval, int? programId, int page = 1, CancellationToken ct = default)
    {
        user.Require(Perm.ProjectsView);
        var q = db.Projects.AsNoTracking().ScopeToCenter(user, p => p.CenterId);
        if (approval is not null) q = q.Where(p => p.ApprovalStatus == approval);
        if (programId is not null) q = q.Where(p => p.ProgramId == programId);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(p => p.NameAr.Contains(search) || p.NameEn.Contains(search) || p.Code.Contains(search) || p.Program!.NameAr.Contains(search));
        return await q.OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt).Select(ToRow()).PageAsync(page, 20, ct);
    }

    public static System.Linq.Expressions.Expression<Func<Project, ProjectRow>> ToRow() => p => new ProjectRow(
        p.Id, p.Code, p.NameAr, p.NameEn, p.ProgramId, p.Program!.Code, p.Program.NameAr, p.Program.NameEn, p.Center!.NameAr, p.Center.NameEn,
        p.ApprovalStatus, p.CollectionStatus, p.ExecutionStatus, p.MappingSyncStatus, p.ExecutionCost, p.CollectionCap, p.Currency,
        p.UpdatedAt, p.CreatedAt, p.PendingChangesJson != null);

    public async Task<Project> GetProjectAsync(int id, CancellationToken ct = default)
    {
        user.Require(Perm.ProjectsView);
        var p = await db.Projects.Include(x => x.Program).Include(x => x.Center)
                    .Include(x => x.Mappings).ThenInclude(m => m.License)
                    .Include(x => x.Mappings).ThenInclude(m => m.FinancialItem)
                    .Include(x => x.BudgetLines)
                    .Include(x => x.ReviewHistory.OrderByDescending(r => r.Id))
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFound();
        if (!user.CanSeeCenter(p.CenterId)) throw new ForbiddenException(Perm.ScopeAllCenters);
        return p;
    }

    public async Task<int> CreateProjectAsync(ProjectInput input, CancellationToken ct = default)
    {
        user.Require(Perm.ProjectsCreateLicensed);
        Validate(input);
        if (!user.CanSeeCenter(input.CenterId)) throw new ForbiddenException(Perm.ScopeAllCenters);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        int programId;
        if (input.ProgramId is null)
        {
            // Rule: no operational project without a program. A standalone project gets a program with the same name and a separate id.
            if (input.Kind != ProjectKind.Standalone)
                throw new DomainException("لا يمكن حفظ مشروع تشغيلي دون برنامج. اختر برنامجًا أو اجعل المشروع منفردًا.",
                    "An operational project cannot be saved without a program. Choose a program or mark it standalone.");
            var program = new CharityProgram
            {
                Code = await numbers.NextAsync("program", "PRG", 4, false, ct),
                NameAr = input.NameAr.Trim(), NameEn = input.NameEn.Trim(), CenterId = input.CenterId,
                IsStandaloneWrapper = true, ApprovedBudget = input.ExecutionCost, Currency = input.Currency,
                DescriptionAr = input.SummaryAr, DescriptionEn = input.SummaryEn,
            };
            db.Programs.Add(program);
            await db.SaveChangesAsync(ct);
            programId = program.Id;
        }
        else
        {
            var program = await db.Programs.FirstOrDefaultAsync(p => p.Id == input.ProgramId, ct) ?? throw NotFound();
            if (program.IsClosed) throw new DomainException("البرنامج مقفل.", "The program is closed.");
            programId = program.Id;
        }

        var p = new Project { Code = await numbers.NextAsync("project", "PRJ", 4, false, ct), ProgramId = programId };
        Apply(p, input);
        db.Projects.Add(p);
        await db.SaveChangesAsync(ct);
        await SyncProposedLicensesAsync(p, input.LicenseIds, ct);
        audit.Log("Projects", "ProjectCreated", nameof(Project), p.Id, newValues: input, reference: p.Code);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return p.Id;
    }

    public async Task UpdateProjectAsync(ProjectInput input, CancellationToken ct = default)
    {
        user.Require(Perm.ProjectsEdit);
        Validate(input);
        var p = await db.Projects.Include(x => x.Mappings).FirstOrDefaultAsync(x => x.Id == input.Id, ct) ?? throw NotFound();
        if (!user.CanSeeCenter(p.CenterId)) throw new ForbiddenException(Perm.ScopeAllCenters);
        EnsureNotClosed(p);

        if (p.IsEditableByCenter)
        {
            var old = Snapshot(p);
            Apply(p, input);
            await SyncProposedLicensesAsync(p, input.LicenseIds, ct);
            audit.Log("Projects", "ProjectUpdated", nameof(Project), p.Id, old, Snapshot(p), reference: p.Code);
        }
        else if (p.ApprovalStatus == ApprovalStatus.FinanciallyApproved)
        {
            // Non-substantive fields apply immediately; substantive ones wait for finance re-approval.
            var substantive = p.CollectionCap != input.CollectionCap || p.ExecutionScope != input.ExecutionScope || p.Currency != input.Currency
                              || !p.Mappings.Where(m => m.Status != MappingStatus.Superseded).Select(m => m.LicenseId).Order().SequenceEqual(input.LicenseIds.Distinct().Order());
            p.SummaryAr = input.SummaryAr; p.SummaryEn = input.SummaryEn; p.Objectives = input.Objectives; p.Activities = input.Activities;
            p.Location = input.Location; p.FollowUpUserId = input.FollowUpUserId; p.Visual = input.Visual; p.QuickAmounts = input.QuickAmounts;
            p.ExecutionStart = input.ExecutionStart; p.ExecutionEnd = input.ExecutionEnd; p.TargetBeneficiaries = input.TargetBeneficiaries;
            p.BeneficiaryCategory = input.BeneficiaryCategory; p.BeneficiaryUnit = input.BeneficiaryUnit;
            if (substantive)
            {
                p.PendingChangesJson = JsonSerializer.Serialize(input);
                var from = p.ApprovalStatus;
                p.ApprovalStatus = ApprovalStatus.AwaitingFinance;
                p.SubmittedAt = DateTime.UtcNow;
                db.ProjectReviews.Add(new ProjectReviewEntry { ProjectId = p.Id, Action = ReviewAction.SubstantiveChange, FromStatus = from, ToStatus = p.ApprovalStatus, ByUserName = user.DisplayName,
                    Note = "تغيير جوهري بعد الاعتماد (السقف/النطاق/التراخيص) — يبقى الربط الحالي ساريًا حتى اعتماد المالية" });
            }
            audit.Log("Projects", substantive ? "SubstantiveChangeRequested" : "ProjectUpdated", nameof(Project), p.Id, newValues: input, reference: p.Code);
        }
        else
            throw new DomainException("لا يمكن تعديل المشروع أثناء المراجعة المالية.", "The project cannot be edited while under finance review.");

        await db.SaveChangesAsync(ct);
    }

    public async Task SubmitToFinanceAsync(int projectId, CancellationToken ct = default)
    {
        user.Require(Perm.ProjectsSubmit);
        var p = await db.Projects.Include(x => x.Mappings).Include(x => x.BudgetLines).FirstOrDefaultAsync(x => x.Id == projectId, ct) ?? throw NotFound();
        if (!user.CanSeeCenter(p.CenterId)) throw new ForbiddenException(Perm.ScopeAllCenters);
        if (!p.IsEditableByCenter) throw new DomainException("المشروع ليس في حالة تسمح بالإرسال.", "Project is not in a submittable state.");
        var missing = new List<string>();
        if (p.CollectionCap <= 0) missing.Add("سقف التحصيل");
        if (p.ExecutionCost <= 0) missing.Add("تكلفة التنفيذ");
        if (!p.Mappings.Any()) missing.Add("الترخيص");
        if (p.ExecutionStart is null || p.ExecutionEnd is null) missing.Add("مدة التنفيذ");
        if (!await db.Attachments.AnyAsync(a => a.OwnerType == nameof(Project) && a.OwnerId == p.Id && a.Category == AttachmentCategory.License, ct)
            && string.IsNullOrWhiteSpace(p.StudyReference)) missing.Add("مستند الترخيص أو مرجع الدراسة");
        if (missing.Count > 0)
            throw new DomainException("بيانات ناقصة قبل الإرسال للمالية: " + string.Join("، ", missing), "Missing data before submission: " + string.Join(", ", missing));

        var from = p.ApprovalStatus;
        p.ApprovalStatus = ApprovalStatus.AwaitingFinance;
        p.SubmittedAt = DateTime.UtcNow;
        db.ProjectReviews.Add(new ProjectReviewEntry
        {
            ProjectId = p.Id, Action = from == ApprovalStatus.ReturnedToCenter ? ReviewAction.Resubmitted : ReviewAction.Submitted,
            FromStatus = from, ToStatus = p.ApprovalStatus, ByUserName = user.DisplayName,
        });
        audit.Log("Projects", "SubmittedToFinance", nameof(Project), p.Id, reference: p.Code);
        db.Notifications.Add(new Notification { At = DateTime.UtcNow, Permission = Perm.FinanceReview, TitleAr = $"مشروع بانتظار المراجعة المالية: {p.NameAr}",
            TitleEn = $"Project awaiting finance review: {p.NameEn}", Link = $"/projects/finance/review/{p.Id}", Severity = "warning" });
        await db.SaveChangesAsync(ct);
    }

    private async Task SyncProposedLicensesAsync(Project p, List<int> licenseIds, CancellationToken ct)
    {
        var ids = licenseIds.Distinct().ToList();
        var licenses = await db.Licenses.Where(l => ids.Contains(l.Id)).ToListAsync(ct);
        foreach (var m in p.Mappings.Where(m => m.Status == MappingStatus.Draft && !ids.Contains(m.LicenseId)).ToList())
            db.Mappings.Remove(m);
        foreach (var l in licenses.Where(l => p.Mappings.All(m => m.LicenseId != l.Id)))
        {
            if (l.Scope == LicenseScope.ProgramAndProjects && l.ProgramId != null && l.ProgramId != p.ProgramId)
                throw new DomainException($"الترخيص {l.Number} يغطي برنامجًا آخر.", $"License {l.Number} covers another program.");
            p.Mappings.Add(new ProjectLicenseMapping { ProjectId = p.Id, LicenseId = l.Id, PeriodFrom = l.CollectionFrom, PeriodTo = l.CollectionTo, Status = MappingStatus.Draft });
        }
    }

    private static void Validate(ProjectInput i)
    {
        if (string.IsNullOrWhiteSpace(i.NameAr) || string.IsNullOrWhiteSpace(i.NameEn))
            throw new DomainException("اسم المشروع مطلوب باللغتين.", "Project name is required in both languages.");
        if (i.CenterId == 0) throw new DomainException("المركز المسؤول مطلوب.", "Responsible center is required.");
        if (i.ExecutionCost < 0 || i.CollectionCap < 0) throw new DomainException("القيم المالية لا تكون سالبة.", "Amounts cannot be negative.");
        if (i.ExecutionStart is not null && i.ExecutionEnd is not null && i.ExecutionEnd < i.ExecutionStart)
            throw new DomainException("تاريخ نهاية التنفيذ قبل البداية.", "Execution end is before start.");
    }

    internal static void Apply(Project p, ProjectInput i)
    {
        p.Kind = i.Kind; p.NameAr = i.NameAr.Trim(); p.NameEn = i.NameEn.Trim(); p.SummaryAr = i.SummaryAr; p.SummaryEn = i.SummaryEn;
        p.Objectives = i.Objectives; p.Activities = i.Activities; p.ExecutionScope = i.ExecutionScope; p.Location = i.Location;
        p.CenterId = i.CenterId; p.FollowUpUserId = i.FollowUpUserId; p.ExecutionStart = i.ExecutionStart; p.ExecutionEnd = i.ExecutionEnd;
        p.BeneficiaryCategory = i.BeneficiaryCategory; p.TargetBeneficiaries = i.TargetBeneficiaries; p.BeneficiaryUnit = i.BeneficiaryUnit;
        p.Currency = i.Currency; p.ExecutionCost = i.ExecutionCost; p.CollectionCap = i.CollectionCap; p.StudyReference = i.StudyReference;
        p.Visual = i.Visual; p.QuickAmounts = i.QuickAmounts;
    }

    private static object Snapshot(Project p) => new { p.NameAr, p.NameEn, p.ExecutionCost, p.CollectionCap, p.ExecutionScope, p.CenterId, p.ExecutionStart, p.ExecutionEnd };

    public static ProjectInput ToInput(Project p) => new()
    {
        Id = p.Id, ProgramId = p.ProgramId, Kind = p.Kind, NameAr = p.NameAr, NameEn = p.NameEn, SummaryAr = p.SummaryAr, SummaryEn = p.SummaryEn,
        Objectives = p.Objectives, Activities = p.Activities, ExecutionScope = p.ExecutionScope, Location = p.Location, CenterId = p.CenterId,
        FollowUpUserId = p.FollowUpUserId, ExecutionStart = p.ExecutionStart, ExecutionEnd = p.ExecutionEnd, BeneficiaryCategory = p.BeneficiaryCategory,
        TargetBeneficiaries = p.TargetBeneficiaries, BeneficiaryUnit = p.BeneficiaryUnit, Currency = p.Currency, ExecutionCost = p.ExecutionCost,
        CollectionCap = p.CollectionCap, StudyReference = p.StudyReference, Visual = p.Visual, QuickAmounts = p.QuickAmounts,
        LicenseIds = p.Mappings.Where(m => m.Status != MappingStatus.Superseded).Select(m => m.LicenseId).Distinct().ToList(),
    };

    internal static void EnsureNotClosed(Project p)
    {
        if (p.IsClosed) throw new DomainException("المشروع مقفل وللقراءة فقط.", "The project is closed and read-only.");
    }

    internal static DomainException NotFound() => new("السجل غير موجود.", "Record not found.");
}
