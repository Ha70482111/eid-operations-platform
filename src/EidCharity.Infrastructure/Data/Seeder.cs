using EidCharity.Application;
using EidCharity.Application.Collections;
using EidCharity.Application.Projects;
using EidCharity.Domain;
using EidCharity.Domain.Collections;
using EidCharity.Domain.Projects;
using EidCharity.Infrastructure.Jobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EidCharity.Infrastructure.Data;

public static class Seeder
{
    public static async Task SeedAsync(IServiceProvider root, IConfiguration config, ILogger log, CancellationToken ct = default)
    {
        using var scope = root.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<EidDbContext>();
        await db.Database.MigrateAsync(ct);

        await SeedReferenceAsync(sp, db, ct);
        var demo = config.GetValue("Seed:DemoData", false);
        var password = config.GetValue<string>("Seed:DefaultPassword");
        if (string.IsNullOrWhiteSpace(password))
        {
            if (!await db.Users.AnyAsync(ct))
                throw new InvalidOperationException("Seed:DefaultPassword (env Seed__DefaultPassword) must be set for the first start to create the 'admin' account.");
            password = Guid.NewGuid().ToString("N") + "Aa1!"; // existing users are never touched
        }
        var users = await SeedUsersAsync(sp, db, password, demo, ct);
        if (demo && !await db.Projects.AnyAsync(ct))
        {
            log.LogInformation("Seeding demo programs, projects and collections…");
            await SeedDemoAsync(root, users, log, ct);
        }
    }

    private static async Task SeedReferenceAsync(IServiceProvider sp, EidDbContext db, CancellationToken ct)
    {
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in DefaultRoles.All)
        {
            var role = await roles.FindByNameAsync(r.Name);
            if (role is null) { role = new IdentityRole(r.Name); await roles.CreateAsync(role); }
            if (!await db.RoleProfiles.AnyAsync(p => p.RoleId == role.Id, ct))
                db.RoleProfiles.Add(new RoleProfile { RoleId = role.Id, NameAr = r.Ar, NameEn = r.En, DescriptionAr = r.DescAr, DescriptionEn = r.DescEn, IsSystem = true });
            if (!await db.RolePermissions.AnyAsync(p => p.RoleId == role.Id, ct))
                db.RolePermissions.AddRange(r.Permissions.Distinct().Select(p => new RolePermission { RoleId = role.Id, Permission = p }));
            else if (r.Name == DefaultRoles.SystemAdmin)
            {
                var have = await db.RolePermissions.Where(p => p.RoleId == role.Id).Select(p => p.Permission).ToListAsync(ct);
                db.RolePermissions.AddRange(r.Permissions.Except(have).Select(p => new RolePermission { RoleId = role.Id, Permission = p }));
            }
        }

        if (!await db.Centers.AnyAsync(ct))
            db.Centers.AddRange(
                new Center { Code = "DOH", NameAr = "مركز الدوحة", NameEn = "Doha Center", DepartmentAr = "إدارة المراكز", DepartmentEn = "Centers department" },
                new Center { Code = "RAY", NameAr = "مركز الريان", NameEn = "Al Rayyan Center", DepartmentAr = "إدارة المراكز", DepartmentEn = "Centers department" },
                new Center { Code = "WAK", NameAr = "مركز الوكرة", NameEn = "Al Wakrah Center", DepartmentAr = "إدارة المراكز", DepartmentEn = "Centers department" },
                new Center { Code = "HFZ", NameAr = "مركز حفظ النعمة", NameEn = "Hifz Al Naema Center", DepartmentAr = "المراكز التابعة", DepartmentEn = "Affiliated centers" },
                new Center { Code = "QGC", NameAr = "مركز ضيوف قطر", NameEn = "Qatar Guests Center", DepartmentAr = "المراكز التابعة", DepartmentEn = "Affiliated centers" },
                new Center { Code = "ECC", NameAr = "مركز عيد الثقافي", NameEn = "Eid Cultural Center", DepartmentAr = "المراكز التابعة", DepartmentEn = "Affiliated centers" },
                new Center { Code = "WMN", NameAr = "الفرع النسائي", NameEn = "Women's Branch", DepartmentAr = "المراكز التابعة", DepartmentEn = "Affiliated centers" });

        var settings = new (string Key, string Value, string Ar, string En)[]
        {
            (SettingKeys.LicenseExpiryAlertDays, "30", "التنبيه قبل انتهاء الترخيص (أيام)", "License expiry alert (days)"),
            (SettingKeys.CapAlertPercents, "80,95", "نسب التنبيه من سقف الترخيص", "Cap alert percentages"),
            (SettingKeys.PendingHoldMinutes, "60", "مهلة حجز السقف للعمليات الإلكترونية المعلقة (دقائق)", "Online pending hold timeout (minutes)"),
            (SettingKeys.InstallmentReminderDays, "7", "التذكير قبل موعد الدفعة (أيام)", "Installment reminder (days)"),
            (SettingKeys.ReceiptPrefix, "RCP", "بادئة رقم السند", "Receipt prefix"),
            (SettingKeys.OrganizationNameAr, "مؤسسة الشيخ عيد بن محمد آل ثاني الخيرية", "اسم المؤسسة (عربي)", "Organization name (Arabic)"),
            (SettingKeys.OrganizationNameEn, "Sheikh Eid Bin Mohammad Al Thani Charitable Association", "اسم المؤسسة (إنجليزي)", "Organization name (English)"),
            (SettingKeys.OrganizationPoBox, "", "صندوق بريد المؤسسة (يظهر في سند القبض)", "Organization P.O. box (shown on receipts)"),
            (SettingKeys.OrganizationPhone, "", "هاتف المؤسسة (يظهر في سند القبض)", "Organization phone (shown on receipts)"),
            (SettingKeys.DefaultQuickAmounts, "50,100,500,1000", "المبالغ السريعة الافتراضية", "Default quick amounts"),
            (SettingKeys.CustodyMaxCash, "20000", "الحد الأعلى للعهدة النقدية قبل التسليم", "Max cash custody before handover"),
        };
        foreach (var s in settings)
            if (!await db.Settings.AnyAsync(x => x.Key == s.Key, ct))
                db.Settings.Add(new SystemSetting { Key = s.Key, Value = s.Value, DescriptionAr = s.Ar, DescriptionEn = s.En });

        if (!await db.FinancialItems.AnyAsync(ct))
            db.FinancialItems.AddRange(
                new FinancialItem { Number = "4101", NameAr = "تبرعات المياه والإصحاح", NameEn = "Water & sanitation donations" },
                new FinancialItem { Number = "4102", NameAr = "تبرعات الرعاية الصحية", NameEn = "Healthcare donations" },
                new FinancialItem { Number = "4103", NameAr = "تبرعات التعليم", NameEn = "Education donations" },
                new FinancialItem { Number = "4104", NameAr = "تبرعات الرعاية الاجتماعية", NameEn = "Social care donations" },
                new FinancialItem { Number = "4105", NameAr = "تبرعات المشاريع الموسمية", NameEn = "Seasonal project donations" });

        if (!await db.Donors.AnyAsync(d => d.Privacy == DonorPrivacy.Unknown, ct))
            db.Donors.Add(new Donor { Number = "DNR-000000", NameAr = "متبرع مجهول الهوية", NameEn = "Unknown donor", Privacy = DonorPrivacy.Unknown });
        await db.SaveChangesAsync(ct);
    }

    private static async Task<Dictionary<string, AppUser>> SeedUsersAsync(IServiceProvider sp, EidDbContext db, string password, bool demo, CancellationToken ct)
    {
        var um = sp.GetRequiredService<UserManager<AppUser>>();
        var centers = await db.Centers.ToDictionaryAsync(c => c.Code, c => c.Id, ct);
        var list = new List<(string User, string Ar, string En, string Role, string Center, string Title)>
        {
            ("admin", "مدير النظام", "System Admin", DefaultRoles.SystemAdmin, "DOH", "مدير النظام"),
        };
        if (demo)
            list.AddRange(
            [
                ("officer", "خالد المري", "Khalid Al-Marri", DefaultRoles.CenterOfficer, "DOH", "مسؤول المركز"),
                ("followup", "نورة الكواري", "Noora Al-Kuwari", DefaultRoles.ProjectFollowUp, "DOH", "موظف متابعة"),
                ("reviewer", "أحمد السليطي", "Ahmed Al-Sulaiti", DefaultRoles.FinanceReviewer, "DOH", "مراجع مالي"),
                ("financemgr", "سارة النعيمي", "Sara Al-Nuaimi", DefaultRoles.FinanceManager, "DOH", "مدير الإدارة المالية"),
                ("accountant", "يوسف الهاجري", "Yousef Al-Hajri", DefaultRoles.Accountant, "DOH", "أمين الصندوق"),
                ("collector", "مريم العبدالله", "Maryam Al-Abdullah", DefaultRoles.Collector, "DOH", "محصّل"),
                ("collector2", "عبدالله الدوسري", "Abdullah Al-Dosari", DefaultRoles.Collector, "RAY", "محصّل"),
                ("supervisor", "هند الخاطر", "Hind Al-Khater", DefaultRoles.CollectionSupervisor, "DOH", "مشرف التحصيل"),
                ("management", "د. محمد الكبيسي", "Dr. Mohammed Al-Kubaisi", DefaultRoles.Management, "DOH", "الإدارة العليا"),
                ("auditor", "فاطمة المهندي", "Fatima Al-Mohannadi", DefaultRoles.Auditor, "DOH", "التدقيق الداخلي"),
            ]);
        var result = new Dictionary<string, AppUser>();
        foreach (var u in list)
        {
            var user = await um.FindByNameAsync(u.User);
            if (user is null)
            {
                user = new AppUser { UserName = u.User, Email = $"{u.User}@eid.local", EmailConfirmed = true, FullNameAr = u.Ar, FullNameEn = u.En,
                    CenterId = centers[u.Center], JobTitle = u.Title, CreatedAt = DateTime.UtcNow, LockoutEnabled = true };
                var res = await um.CreateAsync(user, password);
                if (!res.Succeeded) throw new InvalidOperationException(string.Join("; ", res.Errors.Select(e => e.Description)));
                await um.AddToRoleAsync(user, u.Role);
            }
            result[u.User] = user;
        }

        if (!await db.ApprovalLimits.AnyAsync(ct))
        {
            var roleId = async (string name) => (await sp.GetRequiredService<RoleManager<IdentityRole>>().FindByNameAsync(name))!.Id;
            // Placeholder values for the demo: the institution sets real limits from Admin → Approval matrix.
            db.ApprovalLimits.AddRange(
                new ApprovalLimit { RoleId = await roleId(DefaultRoles.FinanceManager), Module = ApprovalModule.Disbursement, MaxAmount = 500_000 },
                new ApprovalLimit { RoleId = await roleId(DefaultRoles.FinanceManager), Module = ApprovalModule.Refund, MaxAmount = 100_000 },
                new ApprovalLimit { RoleId = await roleId(DefaultRoles.FinanceManager), Module = ApprovalModule.Reallocation, MaxAmount = 200_000 },
                new ApprovalLimit { RoleId = await roleId(DefaultRoles.FinanceManager), Module = ApprovalModule.BudgetAdjustment, MaxAmount = 500_000 },
                new ApprovalLimit { RoleId = await roleId(DefaultRoles.SystemAdmin), Module = ApprovalModule.Disbursement, MaxAmount = 50_000 },
                new ApprovalLimit { RoleId = await roleId(DefaultRoles.SystemAdmin), Module = ApprovalModule.Refund, MaxAmount = 50_000 },
                new ApprovalLimit { RoleId = await roleId(DefaultRoles.SystemAdmin), Module = ApprovalModule.Reallocation, MaxAmount = 50_000 });
            await db.SaveChangesAsync(ct);
        }
        return result;
    }

    private static SystemCurrentUser As(AppUser u) => new() { UserId = u.Id, UserName = u.UserName!, DisplayName = u.FullNameAr, CenterId = u.CenterId };

    private static T Svc<T>(IServiceProvider sp, ICurrentUser user) where T : class
    {
        // Build the service graph with a specific acting user (all services take ICurrentUser).
        var scoped = new ActingProvider(sp, user);
        return ActivatorUtilities.CreateInstance<T>(scoped);
    }

    private sealed class ActingProvider(IServiceProvider inner, ICurrentUser user) : IServiceProvider
    {
        public object? GetService(Type t)
        {
            if (t == typeof(ICurrentUser)) return user;
            if (t.Namespace?.StartsWith("EidCharity.Application") == true && t.IsClass && !t.IsAbstract)
                return ActivatorUtilities.CreateInstance(this, t);
            return inner.GetService(t);
        }
    }

    private static async Task SeedDemoAsync(IServiceProvider root, Dictionary<string, AppUser> users, ILogger log, CancellationToken ct)
    {
        using var scope = root.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<EidDbContext>();
        var centers = await db.Centers.ToDictionaryAsync(c => c.Code, c => c.Id, ct);
        var items = await db.FinancialItems.ToDictionaryAsync(i => i.Number, i => i.Id, ct);
        var officer = As(users["officer"]);
        officer.CenterId = null; // demo seeding across centers
        var officerAll = new SystemCurrentUser { UserId = users["officer"].Id, UserName = "officer", DisplayName = users["officer"].FullNameAr };
        var finance = new SystemCurrentUser { UserId = users["financemgr"].Id, UserName = "financemgr", DisplayName = users["financemgr"].FullNameAr };

        var projects = Svc<ProjectService>(sp, officerAll);
        var licenses = Svc<LicenseService>(sp, officerAll);
        var review = Svc<FinanceReviewService>(sp, finance);
        var budget = Svc<BudgetService>(sp, officerAll);
        var today = DateOnly.FromDateTime(QatarTime.Now);
        DateOnly D(int days) => today.AddDays(days);

        // Programs
        async Task<int> Program(string ar, string en, string center, decimal budgetAmt) =>
            await projects.SaveProgramAsync(new ProgramInput { NameAr = ar, NameEn = en, CenterId = centers[center], ApprovedBudget = budgetAmt, DescriptionAr = ar }, ct);
        var water = await Program("المياه والإصحاح", "Water & sanitation", "DOH", 600_000);
        var edu = await Program("التعليم", "Education", "RAY", 400_000);
        var health = await Program("الرعاية الصحية", "Health care", "DOH", 1_200_000);
        var social = await Program("الرعاية الاجتماعية", "Social care", "WAK", 500_000);
        var empower = await Program("التمكين الاقتصادي", "Economic empowerment", "RAY", 300_000);

        // Licenses (RACA)
        async Task<int> Lic(string no, decimal cap, int from, int to, LicenseScope scope = LicenseScope.Project, int? program = null, string purpose = "") =>
            await licenses.SaveAsync(new LicenseInput { Number = no, Cap = cap, IssueDate = D(from - 5), ValidFrom = D(from), ValidTo = D(to), CollectionFrom = D(from), CollectionTo = D(to),
                Scope = scope, ProgramId = program, Purposes = purpose, Channels = "نقدي، تحويل، نقاط بيع، إلكتروني", Conditions = "التحصيل داخل دولة قطر عبر القنوات المعتمدة." }, ct);
        var licWater = await Lic("LIC-2026-031", 400_000, -120, 180, purpose: "سقيا الماء");
        var licEdu = await Lic("LIC-2026-021", 150_000, -60, 24, purpose: "حقيبة الطالب");
        var licHealthA = await Lic("LIC-2026-014", 400_000, -150, 12, LicenseScope.ProgramAndProjects, health, "برنامج الرعاية الصحية");
        var licHealthB = await Lic("LIC-2026-029", 300_000, -10, 230, LicenseScope.ProgramAndProjects, health, "برنامج الرعاية الصحية — النصف الثاني");
        var licSocial = await Lic("LIC-2026-008", 200_000, -200, 38, purpose: "ترميم المنازل");
        var licRamadan = await Lic("LIC-2026-040", 250_000, -30, 150, purpose: "إفطار صائم");

        async Task<int> Proj(int? program, string ar, string en, string center, decimal cost, decimal cap, string visual, int[] lics, ProjectKind kind = ProjectKind.Child, int target = 500) =>
            await projects.CreateProjectAsync(new ProjectInput
            {
                ProgramId = program, Kind = kind, NameAr = ar, NameEn = en, CenterId = centers[center], ExecutionCost = cost, CollectionCap = cap, Visual = visual,
                SummaryAr = $"مشروع {ar} ضمن خطة المؤسسة لعام 2026.", SummaryEn = $"{en} project within the 2026 plan.", Objectives = "تحقيق أثر مستدام للمستفيدين",
                Activities = "التوريد، التنفيذ، المتابعة، التقرير الختامي", ExecutionScope = "داخل دولة قطر", Location = "الدوحة",
                ExecutionStart = D(-30), ExecutionEnd = D(200), BeneficiaryCategory = "الأسر المتعففة", TargetBeneficiaries = target, BeneficiaryUnit = "أسرة",
                StudyReference = "دراسة معتمدة من الهيئة", LicenseIds = lics.ToList(),
            }, ct);

        var pWater = await Proj(water, "توفير المياه للأسر", "Family water access", "DOH", 250_000, 250_000, "water", [licWater], target: 1200);
        var pWells = await Proj(water, "حفر الآبار السطحية", "Shallow wells", "DOH", 150_000, 150_000, "water", [licWater], target: 40);
        var pSchool = await Proj(edu, "حقيبة الطالب", "Student school kit", "RAY", 150_000, 150_000, "school", [licEdu], target: 3000);
        var pKidney = await Proj(health, "علاج مرضى الكلى", "Kidney patient care", "DOH", 500_000, 500_000, "health", [licHealthA, licHealthB], target: 120);
        var pHome = await Proj(health, "دعم العلاج المنزلي", "Home treatment support", "DOH", 200_000, 200_000, "health", [licHealthB], target: 80);
        var pRestore = await Proj(social, "ترميم منزل آمن", "Safe home restoration", "WAK", 200_000, 200_000, "home", [licSocial], target: 25);
        var pFamilies = await Proj(empower, "تأهيل الأسر المنتجة", "Productive families", "RAY", 180_000, 180_000, "home", [licEdu], target: 60);
        var pIftar = await Proj(null, "إفطار صائم", "Iftar for the fasting", "DOH", 250_000, 250_000, "school", [licRamadan], ProjectKind.Standalone, 20000);

        foreach (var (pid, lines) in new[] {
            (pWater, new[] { ("توريد خزانات", 150_000m), ("تمديدات وتركيب", 100_000m) }),
            (pSchool, new[] { ("شراء الحقائب والقرطاسية", 120_000m), ("التوزيع", 30_000m) }),
            (pKidney, new[] { ("جلسات غسيل", 400_000m), ("أدوية", 100_000m) }),
            (pRestore, new[] { ("مواد بناء", 140_000m), ("أجور عمالة", 60_000m) }),
            (pIftar, new[] { ("وجبات", 230_000m), ("تشغيل ومتطوعون", 20_000m) }) })
            foreach (var (n, a) in lines) await budget.SaveLineAsync(new BudgetLine { ProjectId = pid, NameAr = n, NameEn = n, Amount = a }, ct);

        // Submit → finance mapping → approval (published to Collections through the outbox)
        var submitter = Svc<ProjectService>(sp, officerAll);
        foreach (var pid in new[] { pWater, pWells, pSchool, pKidney, pHome, pRestore, pFamilies, pIftar }) await submitter.SubmitToFinanceAsync(pid, ct);

        async Task Approve(int pid, string item, params int[] lics)
        {
            await review.StartReviewAsync(pid, ct);
            var ls = new List<MappingLineInput>();
            foreach (var l in lics)
            {
                var lic = await db.Licenses.AsNoTracking().FirstAsync(x => x.Id == l, ct);
                ls.Add(new MappingLineInput { LicenseId = l, PeriodFrom = lic.CollectionFrom, PeriodTo = lic.CollectionTo });
            }
            await review.SaveMappingAsync(pid, items[item], ls, ct);
            await review.ApproveEligibilityAsync(pid, "مطابق للترخيص والدراسة", ct);
        }
        await Approve(pWater, "4101", licWater);
        await Approve(pWells, "4101", licWater);
        await Approve(pSchool, "4103", licEdu);
        await Approve(pKidney, "4102", licHealthA, licHealthB);
        await Approve(pRestore, "4104", licSocial);
        await Approve(pIftar, "4105", licRamadan);
        // Home treatment: under review with the resubmission history shown in the design (P09)
        await review.StartReviewAsync(pHome, ct);
        await review.ReturnToCenterAsync(pHome, "يرجى إرفاق دراسة الجدوى المحدثة.", ct);
        await submitter.SubmitToFinanceAsync(pHome, ct);
        // Productive families: returned to center
        await review.ReturnToCenterAsync(pFamilies, "الترخيص المرفق لا يغطي هذا المشروع — يلزم ترخيص خاص بالتمكين.", ct);

        await OutboxDispatcher.DispatchBatchAsync(root.GetRequiredService<IServiceScopeFactory>(), log, ct);

        // ---------- Collections demo ----------
        var collector = As(users["collector"]);
        var donors = Svc<DonorService>(sp, collector);
        var donation = Svc<DonationService>(sp, collector);
        async Task<int> Donor(string ar, string en, string phone, string? email = null, DonorType type = DonorType.Individual) =>
            await donors.SaveAsync(new Donor { NameAr = ar, NameEn = en, Phone = phone, Email = email, Type = type, OrganizationType = type == DonorType.Organization ? "شركة" : null,
                Representatives = type == DonorType.Organization ? [new DonorRepresentative { Name = "علي المناعي", Title = "المدير التنفيذي", CanSign = true, Phone = "55001122" }] : [] }, ct);
        var dHidden = await donors.SaveAsync(new Donor { NameAr = "عبدالرحمن الملا", NameEn = "Abdulrahman Al-Mulla", Phone = "55123418", Email = "donor@example.qa", Privacy = DonorPrivacy.KnownButHidden }, ct);
        var dA = await Donor("جاسم الأنصاري", "Jassim Al-Ansari", "55443322", "jassim@example.qa");
        var dB = await Donor("لطيفة المسند", "Latifa Al-Misnad", "66778899");
        var dOrg = await Donor("شركة الخير للتجارة", "Al Khair Trading Co.", "44556677", "csr@alkhair.qa", DonorType.Organization);

        async Task Give(int donor, PaymentMethod m, params (int Project, decimal Amount)[] lines)
        {
            var draft = new DonationDraft { DonorId = donor, Method = m, CollectionPoint = "مقر المؤسسة — الدوحة",
                Lines = lines.Select(l => new DraftLine { ProjectId = l.Project, Amount = l.Amount }).ToList() };
            if (m == PaymentMethod.BankTransfer) { draft.BankName = "QNB"; draft.PaymentReference = $"TRF-{Guid.NewGuid().ToString()[..8]}"; draft.TransferDate = today; }
            if (m == PaymentMethod.Pos) { draft.PosTerminal = "POS-DOH-01"; draft.PaymentReference = $"POS-{Guid.NewGuid().ToString()[..8]}"; draft.ChannelResult = "Approved"; }
            await donation.ConfirmAsync(draft, ct);
        }
        await Give(dHidden, PaymentMethod.Cash, (pWater, 600), (pSchool, 400));   // acceptance: 600 + 400 = one receipt of 1,000
        await Give(dA, PaymentMethod.Pos, (pKidney, 5_000));
        await Give(dB, PaymentMethod.Cash, (pWater, 1_000), (pIftar, 500));
        await Give(dA, PaymentMethod.BankTransfer, (pSchool, 650));                // stays pending (C14)
        await Give(dOrg, PaymentMethod.Pos, (pRestore, 120_000));
        await Give(dOrg, PaymentMethod.Pos, (pRestore, 78_000));                   // near cap (198,000 / 200,000)
        await Give(dB, PaymentMethod.Pos, (pKidney, 40_000), (pWater, 20_000));

        // Agreement 100,000 with 60,000 collected (acceptance #6)
        var agreements = Svc<AgreementService>(sp, collector);
        var org = await db.Donors.Include(d => d.Representatives).FirstAsync(d => d.Id == dOrg, ct);
        var agId = await agreements.SaveDraftAsync(new AgreementInput
        {
            DonorId = dOrg, RepresentativeId = org.Representatives[0].Id, Terms = "تقرير ربع سنوي عن الإنجاز، ويُبلغ المتبرع عند تعذر التنفيذ.",
            ReportingTerms = "تقرير كل 3 أشهر", Lines = [new AgreementLineInput { ProjectId = pKidney, Amount = 100_000 }],
            Installments = [new InstallmentInput { DueDate = D(-20), Amount = 60_000 }, new InstallmentInput { DueDate = D(40), Amount = 40_000 }],
        }, ct);
        await agreements.SubmitAsync(agId, ct);
        await Svc<AgreementService>(sp, finance).ApproveAsync(agId, "مطابق للنموذج المعتمد", ct);
        await agreements.MarkSignedAsync(agId, "SIGNED-AGR-SCAN-001", ct);
        await donation.ConfirmAsync(new DonationDraft { DonorId = dOrg, Method = PaymentMethod.Pos, AgreementId = agId, PosTerminal = "POS-DOH-01",
            PaymentReference = "POS-AGR-60000", ChannelResult = "Approved", Lines = [new DraftLine { ProjectId = pKidney, Amount = 60_000 }] }, ct);

        await Svc<AgreementService>(sp, collector).SavePledgeAsync(new Pledge { DonorId = dA, ProjectId = pWells, Amount = 15_000, ExpectedDate = D(30), Notes = "تعهد هاتفي" }, ct);

        await Svc<InKindService>(sp, collector).SaveAsync(new InKindDonation { DonorId = dB, ProjectId = pIftar, ItemDescription = "تمور خلاص", Category = "مواد غذائية",
            Quantity = 400, Unit = "كرتون", Location = "مستودع الدوحة", EstimatedValue = 12_000, ValuationBasis = "سعر السوق المحلي", ExpiryDate = D(180) }, ct);

        await OutboxDispatcher.DispatchBatchAsync(root.GetRequiredService<IServiceScopeFactory>(), log, ct);

        // A disbursement through the full path (draft → reviewed → finance check → approved & reserved → partially executed)
        var follow = As(users["followup"]);
        var disbF = Svc<DisbursementService>(sp, follow);
        var lineId = await db.BudgetLines.Where(b => b.ProjectId == pWater).OrderBy(b => b.Id).Select(b => b.Id).FirstAsync(ct);
        var reqId = await disbF.SaveDraftAsync(new DisbursementInput { ProjectId = pWater, BudgetLineId = lineId, Purpose = "توريد خزانات مياه — الدفعة الأولى",
            PayeeName = "مؤسسة الواحة للخزانات", PayeeDetails = "IBAN QA00 0000 0000 0000", Amount = 15_000, InvoiceReference = "INV-7781" }, ct);
        await disbF.SubmitAsync(reqId, ct);
        await Svc<DisbursementService>(sp, As(users["officer"])).SpecialistReviewAsync(reqId, "approve", "المستندات مكتملة", ct);
        await Svc<DisbursementService>(sp, As(users["reviewer"])).FinanceCheckAsync(reqId, "approve", "التمويل متاح", ct);
        await Svc<DisbursementService>(sp, finance).ApproveAsync(reqId, null, ct);
        await Svc<DisbursementService>(sp, As(users["accountant"])).ExecuteAsync(reqId, 10_000, today, "PAY-REF-0001", "إشعار تحويل", ct);

        log.LogInformation("Demo data seeded.");
    }
}
