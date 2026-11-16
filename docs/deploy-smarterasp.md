# النشر على SmarterASP.NET (خطوة بخطوة)

الدليل ده معمول لباقة **Premium** على SmarterASP.NET مع قاعدة **MSSQL**. هنبدأ على **الرابط المؤقت** اللي SmarterASP بيديهولك، وبعدين نربط الدومين بـ Cloudflare.

> النسخة الجاهزة للرفع: `eid-platform-smarterasp.zip`. دي نسخة **مستقلة (Self-contained)**، يعني فيها .NET 10 جواها، ومش محتاجة السيرفر يكون عليه .NET 10.

---

## الجزء الأول: التشغيل على الرابط المؤقت

### 1) إنشاء قاعدة البيانات
1. ادخل لوحة تحكم SmarterASP، وافتح **Databases › MSSQL**.
2. اعمل قاعدة جديدة (مثلًا اسمها `eid`)، واختار كلمة مرور قوية لمستخدم القاعدة.
3. بعد الإنشاء هيظهرلك **Connection String** فيه أربع حاجات: السيرفر (زي `SQL8xxx.site4now.net`) واسم القاعدة (زي `db_xxxxxx_eid`) واسم المستخدم (زي `db_xxxxxx_eid_admin`) وكلمة المرور.
   **ما تبعتش كلمة المرور لحد في الشات.** هتكتبها بنفسك في الخطوة 3.

### 2) رفع الملفات
1. من **Websites** افتح الموقع. هتلاقي **الرابط المؤقت** (زي `http://xxxx-001-site1.xtempurl.com`) ومسار المجلد الرئيسي للموقع.
2. افتح **File Manager**، وادخل المجلد الرئيسي للموقع، وامسح الملفات الافتراضية لو موجودة (زي `default.aspx` أو `index.html`).
3. ارفع `eid-platform-smarterasp.zip`، وبعدين اختار **Unzip / Extract** في نفس المجلد.
   بعد الفك لازم تشوف `web.config` و `EidCharity.Web.exe` ومجلد `wwwroot` **مباشرة** في المجلد الرئيسي، مش جوه مجلد فرعي.
   *(بديل: ترفع الملفات بعد فكها على جهازك عن طريق FTP، وبيانات الـ FTP في لوحة التحكم.)*

### 3) كتابة الإعدادات
افتح `appsettings.Production.json` من File Manager (Edit) واملأ القيم دي:

```json
{
  "ConnectionStrings": {
    "Eid": "Server=SQL8xxx.site4now.net;Database=db_xxxxxx_eid;User Id=db_xxxxxx_eid_admin;Password=كلمة_مرور_القاعدة;TrustServerCertificate=True"
  },
  "Seed": { "DemoData": false, "DefaultPassword": "كلمة مرور قوية لحساب admin" },
  "Storage": { "Root": "App_Data/files" },
  "DataProtection": { "KeysPath": "App_Data/keys" }
}
```

- `DefaultPassword` هي كلمة المرور الأولى لحساب `admin`. لازم تكون 8 حروف على الأقل وفيها حرف كبير وحرف صغير ورقم ورمز (زي `Eid@Admin2026!`).
- لو عاوز بيانات تجريبية للتدريب خلّي `DemoData` تساوي `true`. ده بيعمل كمان مستخدمين تجريبيين (`collector` و`reviewer` وغيرهم) بنفس كلمة مرور `admin`، فما تستخدموش في التشغيل الفعلي.
- احفظ الملف.

### 4) صلاحية الكتابة على مجلد App_Data
من File Manager (أو **Permissions** في لوحة التحكم) ادّي صلاحية **Write** لمجلد `App_Data`. فيه بتتحفظ المرفقات ومفاتيح التشفير.

### 5) أول تشغيل
1. افتح الرابط المؤقت. أول مرة ممكن ياخد من دقيقة لدقيقتين، لأن التطبيق بيعمل الجداول كلها في القاعدة ويجهز الأدوار والمراكز وحساب `admin`.
2. ادخل باسم المستخدم `admin` وكلمة المرور اللي كتبتها في `DefaultPassword`.
3. من **الإدارة › المستخدمون** غيّر كلمة مرور `admin`، وابدأ تعمل حسابات الموظفين وتديهم أدوارهم.
4. للتأكد إن كل حاجة تمام افتح `/health` على نفس الرابط، ولازم يظهر `Healthy`.

> بعد أول تشغيل ناجح تقدر تفضّي قيمة `DefaultPassword` في الملف. هي بتستخدم مرة واحدة بس لما القاعدة تكون فاضية.

---

## لو ظهرت مشكلة

| الرسالة | السبب والحل |
|---|---|
| **HTTP Error 500.30** | التطبيق ما قدرش يبدأ. غالبًا الـ Connection String غلط، أو `DefaultPassword` فاضية في أول تشغيل. راجع الخطوة 3. |
| عاوز تشوف تفاصيل الخطأ | في `web.config` غيّر `stdoutLogEnabled="false"` لـ `"true"`، وافتح الموقع، وبعدين اقرأ الملف اللي في مجلد `logs`. **رجّعها `false` بعد ما تخلص.** |
| **500.34** (mixing hosting models) أو **500.31 / 500.32** | النسخة الحالية بتستخدم `hostingModel="outofprocess"`، وده المناسب لـ SmarterASP. لو لقيت `inprocess` في `web.config` غيّرها لـ `outofprocess`. |
| الصفحة بتفتح بس الأزرار مش شغالة، أو بيظهر "Reconnecting…" | تأكد إن **WebSockets** مفعّلة للموقع من لوحة التحكم، لو الاختيار ده موجود. لو مش موجود ابعت لدعم SmarterASP وقولهم إن التطبيق Blazor Server ومحتاج WebSockets. |
| بطء أول فتح بعد فترة | الاستضافة المشتركة بتوقف التطبيق لما محدش يستخدمه. ممكن تستخدم خدمة مراقبة مجانية (زي UptimeRobot) تفتح `/health` كل 5 دقايق. ده كمان بيخلي إرسال رسائل الربط بين النظامين يكمل في الخلفية من غير تأخير. |

---

## الجزء التاني: ربط الدومين عن طريق Cloudflare (بعدين)

1. **في SmarterASP:** ضيف الدومين (مثلًا `ops.eidcharity.net`) للموقع من **Domain / Domain Pointer** (أو Site Bindings)، واكتب عنوان **IP** الموقع اللي ظاهر في لوحة التحكم.
2. **في Cloudflare › DNS:** ضيف سجل `A` باسم `ops`، وقيمته عنوان الـ IP ده، وخلّيه **Proxied** (السحابة البرتقالي).
3. **في Cloudflare › SSL/TLS:**
   - لو فعّلت شهادة SSL للموقع من لوحة SmarterASP، اختار **Full**.
   - لو لسه ما فيش شهادة على SmarterASP، اختار **Flexible** مؤقتًا، وبعد ما تفعّل الشهادة حوّلها لـ **Full**.
4. **في Cloudflare › Network:** خلّي **WebSockets = On**.
5. **في Cloudflare › Speed › Optimization:** خلّي **Rocket Loader = Off**، لأنه بيبوّظ سكربت Blazor.
6. افتح `https://ops.eidcharity.net` وجرّب الدخول.

---

## تحديث النسخة بعدين
1. خد نسخة احتياطية من `appsettings.Production.json` ومن مجلد `App_Data`.
2. ارفع الـ zip الجديد وفكّه فوق القديم.
3. رجّع `appsettings.Production.json` بتاعك (وما تمسحش `App_Data`).
4. أي تعديلات جديدة على القاعدة بتتطبق لوحدها عند التشغيل.

## النسخ الاحتياطي
- **القاعدة:** من **Databases › MSSQL › Backup** في لوحة التحكم.
- **المرفقات:** حمّل مجلد `App_Data` من File Manager أو FTP.

---

## للمطوّر: عمل الـ zip من الكود
```bash
dotnet publish src/EidCharity.Web -c Release -r win-x64 --self-contained true -p:DebugType=none -o publish
# بعدين اضغط محتوى مجلد publish في zip
```
أو من Visual Studio: **Publish › Import Profile**، واستخدم ملف Web Deploy اللي بتنزّله من لوحة SmarterASP، واختار Deployment mode = Self-contained و Target runtime = win-x64.
