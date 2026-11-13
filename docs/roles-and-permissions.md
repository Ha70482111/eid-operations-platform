# الأدوار والصلاحيات — Roles & permissions

> يُولَّد هذا الملف من `src/EidCharity.Domain/Permissions.cs`. هذه هي **الإعدادات الافتراضية** عند أول تشغيل، ومدير النظام يقدر يعدّلها من شاشة **الإدارة › الأدوار والصلاحيات** (كل تعديل يحتاج سبب ويتسجل في سجل الإجراءات).

- عدد الصلاحيات: **66** موزعة على **12** مجموعة.
- عدد الأدوار الافتراضية: **10**.
- الصلاحيات المعلَّمة ⚠️ حساسة.

## الأدوار

| الدور | Role | الوصف | عدد الصلاحيات |
|---|---|---|---|
| مدير النظام | System administrator (`SystemAdmin`) | كل الصلاحيات | 66 |
| مسؤول المركز | Center officer (`CenterOfficer`) | يسجل البرامج والمشاريع المرخصة ويرسلها للمالية | 16 |
| موظف متابعة | Follow-up officer (`ProjectFollowUp`) | يتابع التنفيذ والمستفيدين ويقدم طلبات الصرف | 8 |
| مراجع مالي | Finance reviewer (`FinanceReviewer`) | يراجع ويربط البند والتراخيص | 14 |
| مدير الإدارة المالية | Finance manager (`FinanceManager`) | يعتمد الإتاحة والصرف والاسترداد | 29 |
| محاسب / أمين صندوق | Accountant / treasurer (`Accountant`) | ينفذ المدفوعات ويستلم العهد | 10 |
| محصّل | Collector (`Collector`) | يستقبل التبرعات ويصدر السندات | 12 |
| مشرف التحصيل | Collection supervisor (`CollectionSupervisor`) | يشرف على المحصلين والمطابقة والدمج | 20 |
| الإدارة العليا | Executive management (`Management`) | اطلاع على اللوحات والتقارير | 9 |
| الرقابة والتدقيق | Internal audit (`Auditor`) | اطلاع وسجل الإجراءات | 12 |

## المصفوفة

الأعمدة: **1** مدير النظام · **2** مسؤول المركز · **3** موظف متابعة · **4** مراجع مالي · **5** مدير الإدارة المالية · **6** محاسب / أمين صندوق · **7** محصّل · **8** مشرف التحصيل · **9** الإدارة العليا · **10** الرقابة والتدقيق


### البرامج والمشاريع — Programs & projects

| الصلاحية | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| عرض البرامج<br><sub>`Projects.Programs.View`</sub> | ✓ | ✓ | ✓ | ✓ | ✓ |  |  |  | ✓ | ✓ |
| إنشاء وتعديل البرامج<br><sub>`Projects.Programs.Manage`</sub> | ✓ | ✓ |  |  |  |  |  |  |  |  |
| عرض المشاريع<br><sub>`Projects.Projects.View`</sub> | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |  |  | ✓ | ✓ |
| إدخال مشروع معتمد من هيئة تنظيم الأعمال الخيرية ⚠️<br><sub>`Projects.Projects.CreateLicensed`</sub> | ✓ | ✓ |  |  |  |  |  |  |  |  |
| تعديل بيانات المشروع ⚠️<br><sub>`Projects.Projects.Edit`</sub> | ✓ | ✓ |  |  |  |  |  |  |  |  |
| إرسال المشروع للمالية<br><sub>`Projects.Projects.Submit`</sub> | ✓ | ✓ |  |  |  |  |  |  |  |  |
| عرض التراخيص<br><sub>`Projects.Licenses.View`</sub> | ✓ | ✓ | ✓ | ✓ | ✓ |  |  |  | ✓ | ✓ |
| إدارة التراخيص ⚠️<br><sub>`Projects.Licenses.Manage`</sub> | ✓ | ✓ |  |  |  |  |  |  |  |  |
| عرض الإيرادات ونتائج التحصيل<br><sub>`Projects.Revenue.View`</sub> | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |  |  | ✓ | ✓ |

### المراجعة المالية والإتاحة — Finance review & eligibility

| الصلاحية | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| المراجعة المالية للمشاريع<br><sub>`Projects.Finance.Review`</sub> | ✓ |  |  | ✓ | ✓ |  |  |  |  |  |
| إنشاء وإدارة البنود المالية ⚠️<br><sub>`Projects.Finance.Items`</sub> | ✓ |  |  | ✓ | ✓ |  |  |  |  |  |
| ربط المشروع بالبند والتراخيص ⚠️<br><sub>`Projects.Finance.Map`</sub> | ✓ |  |  | ✓ | ✓ |  |  |  |  |  |
| اعتماد الإتاحة للتحصيل ⚠️<br><sub>`Projects.Finance.ApproveEligibility`</sub> | ✓ |  |  |  | ✓ |  |  |  |  |  |
| إيقاف الإتاحة ⚠️<br><sub>`Projects.Finance.SuspendEligibility`</sub> | ✓ |  |  |  | ✓ |  |  |  |  |  |

### الميزانية — Budget

| الصلاحية | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| عرض الميزانيات<br><sub>`Projects.Budget.View`</sub> | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |  |  | ✓ | ✓ |
| إعداد الميزانية وبنودها ⚠️<br><sub>`Projects.Budget.Manage`</sub> | ✓ | ✓ |  |  |  |  |  |  |  |  |
| اعتماد تعديل أو نقل بنود الميزانية ⚠️<br><sub>`Projects.Budget.ApproveAdjustment`</sub> | ✓ |  |  |  | ✓ |  |  |  |  |  |

### الصرف — Disbursement

| الصلاحية | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| إنشاء طلب صرف<br><sub>`Projects.Disbursement.Create`</sub> | ✓ | ✓ | ✓ |  |  |  |  |  |  |  |
| مراجعة المختص لطلب الصرف<br><sub>`Projects.Disbursement.Review`</sub> | ✓ | ✓ |  |  |  |  |  |  |  |  |
| تحقق المالية من التمويل والسيولة<br><sub>`Projects.Disbursement.FinanceCheck`</sub> | ✓ |  |  | ✓ | ✓ |  |  |  |  |  |
| اعتماد الصرف ⚠️<br><sub>`Projects.Disbursement.Approve`</sub> | ✓ |  |  |  | ✓ |  |  |  |  |  |
| تنفيذ الدفع ⚠️<br><sub>`Projects.Disbursement.Execute`</sub> | ✓ |  |  |  |  | ✓ |  |  |  |  |
| إلغاء طلب وتحرير الحجز ⚠️<br><sub>`Projects.Disbursement.Cancel`</sub> | ✓ |  |  |  | ✓ |  |  |  |  |  |

### التنفيذ والإقفال — Execution & closure

| الصلاحية | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| متابعة التنفيذ والمراحل<br><sub>`Projects.Execution.Manage`</sub> | ✓ | ✓ | ✓ |  |  |  |  |  |  |  |
| إدارة المستفيدين<br><sub>`Projects.Beneficiaries.Manage`</sub> | ✓ | ✓ | ✓ |  |  |  |  |  |  |  |
| الاطلاع على بيانات المستفيدين الحساسة ⚠️<br><sub>`Projects.Beneficiaries.ViewSensitive`</sub> | ✓ |  |  |  |  |  |  |  |  |  |
| إيقاف أو إلغاء المشروع ⚠️<br><sub>`Projects.Projects.Suspend`</sub> | ✓ |  |  |  | ✓ |  |  |  |  |  |
| إقفال المشروع ⚠️<br><sub>`Projects.Closure.Close`</sub> | ✓ |  |  |  | ✓ |  |  |  |  |  |
| إعادة فتح مشروع مقفل ⚠️<br><sub>`Projects.Closure.Reopen`</sub> | ✓ |  |  |  | ✓ |  |  |  |  |  |

### التحصيل والسندات — Collections & receipts

| الصلاحية | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| استخدام واجهة المحصّل<br><sub>`Collections.Workspace.Use`</sub> | ✓ |  |  |  |  |  | ✓ | ✓ |  |  |
| عرض المتبرعين<br><sub>`Collections.Donors.View`</sub> | ✓ |  |  |  |  |  | ✓ | ✓ |  | ✓ |
| إضافة وتعديل المتبرعين<br><sub>`Collections.Donors.Manage`</sub> | ✓ |  |  |  |  |  | ✓ | ✓ |  |  |
| دمج المتبرعين ⚠️<br><sub>`Collections.Donors.Merge`</sub> | ✓ |  |  |  |  |  |  | ✓ |  |  |
| تأكيد التبرع وإصدار السند ⚠️<br><sub>`Collections.Donations.Confirm`</sub> | ✓ |  |  |  |  |  | ✓ | ✓ |  |  |
| عرض كل العمليات (ليس عملياتي فقط)<br><sub>`Collections.Donations.ViewAll`</sub> | ✓ |  |  | ✓ | ✓ | ✓ |  | ✓ | ✓ | ✓ |
| تأكيد استلام العمليات المعلقة ⚠️<br><sub>`Collections.Donations.VerifyPending`</sub> | ✓ |  |  | ✓ |  | ✓ |  | ✓ |  |  |
| إعادة طباعة وإرسال السند<br><sub>`Collections.Receipts.Reprint`</sub> | ✓ |  |  |  |  |  | ✓ | ✓ |  |  |
| طلب تصحيح أو إلغاء سند ⚠️<br><sub>`Collections.Receipts.Correct`</sub> | ✓ |  |  |  |  |  | ✓ | ✓ |  |  |
| اعتماد تصحيح أو إلغاء سند ⚠️<br><sub>`Collections.Receipts.CorrectApprove`</sub> | ✓ |  |  |  | ✓ |  |  |  |  |  |

### التعهدات والاتفاقيات — Pledges & agreements

| الصلاحية | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| إدارة التعهدات<br><sub>`Collections.Pledges.Manage`</sub> | ✓ |  |  |  |  |  | ✓ | ✓ |  |  |
| إنشاء وتعديل الاتفاقيات<br><sub>`Collections.Agreements.Manage`</sub> | ✓ |  |  |  |  |  | ✓ | ✓ |  |  |
| اعتماد وتفعيل الاتفاقيات ⚠️<br><sub>`Collections.Agreements.Approve`</sub> | ✓ |  |  |  | ✓ |  |  |  |  |  |
| إعادة جدولة الدفعات ⚠️<br><sub>`Collections.Agreements.Reschedule`</sub> | ✓ |  |  |  |  |  |  | ✓ |  |  |

### العيني — In-kind

| الصلاحية | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| تسجيل التبرعات العينية<br><sub>`Collections.InKind.Manage`</sub> | ✓ |  |  |  |  |  | ✓ | ✓ |  |  |
| اعتماد تقييم العيني ⚠️<br><sub>`Collections.InKind.ApproveValuation`</sub> | ✓ |  |  |  | ✓ |  |  |  |  |  |

### العهد والمطابقة — Custody & reconciliation

| الصلاحية | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| تسليم العهدة النقدية<br><sub>`Collections.Custody.Handover`</sub> | ✓ |  |  |  |  |  | ✓ | ✓ |  |  |
| تأكيد استلام العهدة وتسويتها ⚠️<br><sub>`Collections.Custody.Confirm`</sub> | ✓ |  |  |  |  | ✓ |  | ✓ |  |  |
| مطابقة القنوات ⚠️<br><sub>`Collections.Reconciliation.Manage`</sub> | ✓ |  |  | ✓ |  | ✓ |  | ✓ |  |  |

### الاسترداد وإعادة التخصيص — Refunds & reallocation

| الصلاحية | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| طلب استرداد<br><sub>`Collections.Refunds.Request`</sub> | ✓ |  |  |  |  |  | ✓ | ✓ |  |  |
| اعتماد الاسترداد ⚠️<br><sub>`Collections.Refunds.Approve`</sub> | ✓ |  |  |  | ✓ |  |  |  |  |  |
| تنفيذ الاسترداد ⚠️<br><sub>`Collections.Refunds.Execute`</sub> | ✓ |  |  |  |  | ✓ |  |  |  |  |
| طلب إعادة تخصيص<br><sub>`Collections.Reallocation.Request`</sub> | ✓ |  |  |  |  |  | ✓ | ✓ |  |  |
| اعتماد إعادة التخصيص ⚠️<br><sub>`Collections.Reallocation.Approve`</sub> | ✓ |  |  |  | ✓ |  |  |  |  |  |

### التقارير — Reports

| الصلاحية | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| تقارير المشاريع<br><sub>`Projects.Reports.View`</sub> | ✓ | ✓ |  | ✓ | ✓ |  |  |  | ✓ | ✓ |
| تصدير تقارير المشاريع ⚠️<br><sub>`Projects.Reports.Export`</sub> | ✓ |  |  |  | ✓ |  |  |  |  |  |
| تقارير التحصيل<br><sub>`Collections.Reports.View`</sub> | ✓ |  |  |  | ✓ |  |  | ✓ | ✓ | ✓ |
| تصدير تقارير التحصيل ⚠️<br><sub>`Collections.Reports.Export`</sub> | ✓ |  |  |  | ✓ |  |  | ✓ |  |  |

### الإدارة — Administration

| الصلاحية | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| إدارة المستخدمين ⚠️<br><sub>`Admin.Users`</sub> | ✓ |  |  |  |  |  |  |  |  |  |
| إدارة الأدوار والصلاحيات ⚠️<br><sub>`Admin.Roles`</sub> | ✓ |  |  |  |  |  |  |  |  |  |
| إدارة المراكز<br><sub>`Admin.Centers`</sub> | ✓ |  |  |  |  |  |  |  |  |  |
| إعدادات النظام ⚠️<br><sub>`Admin.Settings`</sub> | ✓ |  |  |  |  |  |  |  |  |  |
| مصفوفة حدود الاعتماد ⚠️<br><sub>`Admin.ApprovalMatrix`</sub> | ✓ |  |  |  |  |  |  |  |  |  |
| التفويضات ⚠️<br><sub>`Admin.Delegations`</sub> | ✓ |  |  |  |  |  |  |  |  |  |
| سجل الإجراءات<br><sub>`Admin.Audit.View`</sub> | ✓ |  |  |  | ✓ |  |  |  |  | ✓ |
| متابعة الربط بين النظامين وإعادة الإرسال ⚠️<br><sub>`Admin.Integration.Monitor`</sub> | ✓ |  |  |  |  |  |  |  |  | ✓ |
| نطاق كل المراكز ⚠️<br><sub>`Scope.AllCenters`</sub> | ✓ |  |  | ✓ | ✓ | ✓ |  |  | ✓ | ✓ |
