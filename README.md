# منصة عمليات مؤسسة عيد الخيرية
**Eid Charity Operations Platform — Internal Projects & Collections**

نظامان مترابطان في تطبيق واحد:

- **المشاريع الداخلية**: البرامج والمشاريع، المراجعة المالية والربط بالبنود والتراخيص، الميزانية والمناقلات، الصرف بمسار اعتماد كامل، التنفيذ والمراحل والمستفيدين، الإقفال، والتقارير.
- **التحصيل**: مساحة تحصيل بالمشاريع المتاحة، التبرع والسند، التحقق من التحويلات، المتبرعون والدمج، التعهدات، الاتفاقيات والدفعات، العيني، العهدة والمطابقة، الاسترداد وإعادة التخصيص وتصحيح السندات، والتقارير.
- **الإدارة**: المستخدمون، الأدوار ومصفوفة الصلاحيات (66 صلاحية × 10 أدوار افتراضية)، المراكز، مصفوفة حدود الاعتماد، التفويضات، الإعدادات، سجل الإجراءات، ومتابعة الربط بين النظامين.

الواجهة عربي/إنجليزي (RTL/LTR) بتتبدل من غير إعادة تحميل، على تصميم Figma وهوية عيد (العنابي #8A0047، الذهبي #9B782D، خط لوسيل).

## التقنيات

| الطبقة | التقنية |
|---|---|
| الواجهة | Blazor Web App (.NET 10) – Interactive Server |
| المنطق | Clean Architecture: Domain / Application / Infrastructure / Web |
| البيانات | SQL Server عبر EF Core 10 (مخططات: identity, admin, audit, prj, col, integ) |
| الأمان | ASP.NET Core Identity، صلاحيات كـ Claims تُعاد مراجعتها كل دقيقة، قفل بعد 5 محاولات، Antiforgery، سجل إجراءات |
| الربط بين النظامين | Outbox/Inbox مع إعادة محاولة ومنع تكرار |
| النشر | Docker Compose (App + SQL Server + cloudflared) خلف Cloudflare Tunnel |

## التشغيل المحلي

```bash
# SQL Server محلي
docker run -d --name eidsql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='Dev_Passw0rd!2026' -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest

# التطبيق (Development: بيانات تجريبية + مستخدمين تجريبيين)
cd src/EidCharity.Web
dotnet run --urls http://localhost:5080
```

مستخدمو بيئة التطوير (كلمة المرور `Eid@Dev2026!` من `appsettings.Development.json`):

| المستخدم | الدور |
|---|---|
| admin | مدير النظام |
| officer | مسؤول المركز |
| followup | موظف متابعة المشاريع |
| reviewer | مراجع مالي |
| financemgr | مدير الإدارة المالية |
| accountant | محاسب / أمين صندوق |
| collector, collector2 | محصّل |
| supervisor | مشرف التحصيل |
| management | الإدارة العليا |
| auditor | الرقابة والتدقيق |

## الاختبارات

```bash
dotnet test                                   # اختبارات سلامة مصفوفة الصلاحيات والقواعد
node tests/e2e/smoke.js admin "/collections,/projects,/admin/roles"   # فتح الشاشات
node tests/e2e/acceptance.js                  # سيناريوهات قبول على بيئة التطوير
```

سيناريوهات القبول: رفض كلمة مرور خاطئة، تبرع نقدي كامل وإصدار سند، منع المحصّل من الإدارة والمالية، إخفاء القوائم حسب الصلاحية، تبديل اللغة، تعديل مصفوفة الصلاحيات بسبب وظهوره في سجل الإجراءات.

## النشر

- استضافة مشتركة (SmarterASP.NET / IIS): [docs/deploy-smarterasp.md](docs/deploy-smarterasp.md)
- سيرفر خاص (Docker + Cloudflare Tunnel): [docs/deploy-cloudflare.md](docs/deploy-cloudflare.md)

## التوثيق

- [مسار العمليات ومسارات الاعتماد](docs/workflows.md)
- [الأدوار والصلاحيات (المصفوفة الكاملة)](docs/roles-and-permissions.md)
- [النشر على SmarterASP.NET (استضافة Windows/IIS)](docs/deploy-smarterasp.md)
- [النشر على سيرفر خاص بـ Docker + Cloudflare Tunnel](docs/deploy-cloudflare.md)

## Migrations

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> -p src/EidCharity.Infrastructure -s src/EidCharity.Web -o Data/Migrations
```
تُطبَّق تلقائيًا عند التشغيل (`Database:MigrateAndSeed=true`).
