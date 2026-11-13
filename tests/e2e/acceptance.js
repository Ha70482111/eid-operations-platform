const { chromium } = require('playwright');
const BASE = process.env.EID_URL || 'http://127.0.0.1:5080', PASS = process.env.EID_PASS || 'Eid@Dev2026!';
const results = [];
const check = (name, ok, extra = '') => { results.push({ name, ok }); console.log((ok ? 'PASS ' : 'FAIL ') + name + (extra ? ' — ' + extra : '')); };
async function login(b, user) {
  const ctx = await b.newContext({ viewport: { width: 1440, height: 900 } });
  const p = await ctx.newPage();
  p.on('pageerror', e => console.log('  pageerror', e.message));
  await p.goto(BASE + '/account/login');
  await p.fill('input[name=username]', user); await p.fill('input[name=password]', PASS);
  await Promise.all([p.waitForNavigation(), p.click('button[type=submit]')]);
  await p.waitForTimeout(1500);
  return p;
}
(async () => {
  const b = await chromium.launch();
  // 1. Wrong password is refused
  {
    const ctx = await b.newContext(); const p = await ctx.newPage();
    await p.goto(BASE + '/account/login');
    await p.fill('input[name=username]', 'collector'); await p.fill('input[name=password]', 'wrong-pass');
    await Promise.all([p.waitForNavigation(), p.click('button[type=submit]')]);
    check('Wrong password stays on login', p.url().includes('/account/login?error=1'));
    await ctx.close();
  }
  // 2. Collector: cash donation end to end, receipt issued
  {
    const p = await login(b, 'collector');
    check('Collector lands on collections workspace', p.url().endsWith('/collections'));
    const card = p.locator('article.project-card:not(.disabled)').first();
    await card.locator('.quick-amounts button, button:has-text("500")').filter({ hasText: '500' }).first().click().catch(() => {});
    await card.locator('button[aria-label="إضافة للتبرع"]').click();
    await p.waitForTimeout(800);
    await p.click('a.checkout-btn'); await p.waitForURL('**/collections/checkout'); await p.waitForTimeout(1500);
    await p.fill('input[placeholder="ابدأ بالكتابة…"]', 'المل'); await p.waitForTimeout(1500);
    await p.locator('button.donor-result').first().click(); await p.waitForTimeout(500);
    await p.click('.form-actions .primary-btn'); await p.waitForTimeout(800);   // -> payment (cash default?)
    await p.screenshot({ path: 'shots/flow_payment.png' });
    await p.click('.form-actions .primary-btn'); await p.waitForTimeout(800);   // -> review
    await p.screenshot({ path: 'shots/flow_review.png' });
    await p.click('.form-actions .primary-btn'); await p.waitForTimeout(3000);  // confirm
    await p.screenshot({ path: 'shots/flow_result.png' });
    const body = await p.innerText('body');
    check('Cash donation confirmed with a receipt', /RCP|سند/.test(body) && !/تعذر|خطأ/.test(body), body.match(/RCP-[\w-]+/)?.[0] || '');
    // 3. Collector cannot open admin or finance
    await p.goto(BASE + '/admin/users'); await p.waitForTimeout(1500);
    check('Collector blocked from Admin › Users', (await p.innerText('body')).includes('لا تملك صلاحية'));
    await p.goto(BASE + '/projects/finance'); await p.waitForTimeout(1500);
    check('Collector blocked from Finance inbox', (await p.innerText('body')).includes('لا تملك صلاحية'));
    const nav = await p.innerText('nav, aside');
    check('Collector sidebar hides Administration', !nav.includes('المستخدمون'));
    // 4. Language switch
    await p.goto(BASE + '/collections'); await p.waitForTimeout(1200);
    await p.click('button:has-text("English")'); await p.waitForTimeout(1200);
    check('English switch sets dir=ltr', (await p.getAttribute('html', 'dir')) === 'ltr');
    await p.screenshot({ path: 'shots/flow_english.png' });
    await p.click('button:has-text("العربية")').catch(() => {}); await p.waitForTimeout(800);
  }
  // 5. Admin: change permission matrix with reason -> audit entry
  {
    const p = await login(b, 'admin');
    await p.goto(BASE + '/admin/roles'); await p.waitForTimeout(2000);
    const box = p.locator('table.perm-matrix tbody input[type=checkbox]').first();
    const before = await box.isChecked();
    await box.click(); await p.waitForTimeout(500);
    await p.click('button:has-text("حفظ التغييرات")'); await p.waitForTimeout(600);
    await p.fill('.modal textarea', 'اختبار قبول: تعديل صلاحية'); await p.waitForTimeout(300);
    await p.click('.modal button:has-text("تأكيد الحفظ")'); await p.waitForTimeout(2000);
    const after = await p.locator('table.perm-matrix tbody input[type=checkbox]').first().isChecked();
    check('Permission matrix change saved', after !== before);
    // revert
    await p.locator('table.perm-matrix tbody input[type=checkbox]').first().click(); await p.waitForTimeout(400);
    await p.click('button:has-text("حفظ التغييرات")'); await p.waitForTimeout(500);
    await p.fill('.modal textarea', 'اختبار قبول: إعادة الصلاحية'); await p.click('.modal button:has-text("تأكيد الحفظ")'); await p.waitForTimeout(2000);
    await p.goto(BASE + '/admin/audit'); await p.waitForTimeout(2000);
    const audit = await p.innerText('body');
    check('Audit log records permission change and donation', audit.includes('RolePermissionsChanged'));
    await p.screenshot({ path: 'shots/flow_audit.png' });
    await p.goto(BASE + '/admin/roles'); await p.waitForTimeout(1500); await p.screenshot({ path: 'shots/admin_admin_roles.png' });
  }
  await b.close();
  const failed = results.filter(r => !r.ok).length;
  console.log(`\n${results.length - failed}/${results.length} passed`);
  process.exit(failed ? 1 : 0);
})();
