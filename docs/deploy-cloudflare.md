# النشر على سيرفر وربطه بـ Cloudflare

اخترنا **Cloudflare Tunnel** لأن Blazor Server بيحتاج اتصال WebSocket دائم، وده مش متاح على Cloudflare Pages/Workers. الـ Tunnel كمان بيخلّي السيرفر من غير أي بورت مفتوح للإنترنت.

## المتطلبات

- سيرفر Linux (أو Windows) عليه Docker و Docker Compose. مقترح: 4 GB RAM على الأقل (SQL Server وحده محتاج 2 GB).
- دومين مضاف على حساب Cloudflare.

## الخطوات

1. **إنشاء الـ Tunnel**
   Cloudflare Dashboard › Zero Trust › Networks › Tunnels › *Create a tunnel* › Cloudflared.
   اختار اسم (مثلًا `eid-platform`) وانسخ الـ **Token** من خطوة *Install connector*.

2. **ربط الدومين بالتطبيق**
   في نفس الـ Tunnel › *Public Hostname* › Add:
   - Subdomain: `ops` (مثال) · Domain: `eidcharity.net`
   - Service: `HTTP` ‑ `app:8080`

3. **تجهيز السيرفر**
   ```bash
   git clone <repo-url> eid-platform && cd eid-platform/deploy
   cp .env.example .env
   nano .env      # SQL_SA_PASSWORD, ADMIN_INITIAL_PASSWORD, CLOUDFLARE_TUNNEL_TOKEN
   docker compose up -d --build
   docker compose logs -f app   # انتظر "Application started"
   ```
   أول تشغيل بيعمل الـ Database والجداول تلقائيًا (EF Migrations) وينشئ الأدوار العشرة والمراكز ومستخدم `admin` بكلمة المرور اللي في `ADMIN_INITIAL_PASSWORD`.

4. **الدخول**: افتح `https://ops.eidcharity.net` وادخل بـ `admin`، وغيّر كلمة المرور من الإدارة › المستخدمون، وبعدين أنشئ حسابات الموظفين.

## إعدادات مهمة في Cloudflare

- **SSL/TLS** › Full.
- **Network** › WebSockets: **On** (افتراضيًا مفعّل).
- يفضّل **Zero Trust › Access** لتقييد الدخول على إيميلات المؤسسة (طبقة حماية إضافية قبل شاشة الدخول).
- ما تفعّلش *Rocket Loader* (بيكسر سكربت Blazor).

## النسخ الاحتياطي

```bash
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SQL_SA_PASSWORD" -C \
  -Q "BACKUP DATABASE EidCharity TO DISK='/var/opt/mssql/backup/EidCharity.bak' WITH INIT, COMPRESSION"
docker compose cp sqlserver:/var/opt/mssql/backup/EidCharity.bak ./backups/EidCharity-$(date +%F).bak
```
المرفقات ومفاتيح التشفير في الـ volume `appdata` (`/app/data`) ولازم تتاخد في النسخة الاحتياطية برضه.

## استخدام SQL Server موجود عندكم

لو عندكم SQL Server جاهز، احذف خدمة `sqlserver` من `docker-compose.yml` وغيّر `ConnectionStrings__Eid` لعنوان السيرفر. المستخدم محتاج صلاحية `db_owner` على قاعدة `EidCharity` (لإنشاء الجداول أول مرة).

## التحديث لإصدار جديد

```bash
git pull && cd deploy && docker compose up -d --build
```
الـ Migrations الجديدة بتتطبق تلقائيًا عند التشغيل.
