const { chromium } = require('playwright');
const BASE = process.env.EID_URL || 'http://127.0.0.1:5080';
const user = process.argv[2] || 'admin', pass = process.env.EID_PASS || 'Eid@Dev2026!';
const routes = (process.argv[3] || '').split(',').filter(Boolean);
(async () => {
  const b = await chromium.launch();
  const ctx = await b.newContext({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1 });
  const p = await ctx.newPage();
  const errs = [];
  p.on('pageerror', e => errs.push('pageerror ' + e.message));
  p.on('console', m => { if (m.type() === 'error') errs.push('console ' + m.text()); });
  await p.goto(BASE + '/account/login');
  await p.fill('input[name=username]', user);
  await p.fill('input[name=password]', pass);
  await Promise.all([p.waitForNavigation(), p.click('button[type=submit]')]);
  console.log('after login:', p.url());
  for (const r of routes) {
    errs.length = 0;
    await p.goto(BASE + r);
    await p.waitForTimeout(2500);
    const txt = await p.evaluate(() => document.querySelector('main, .page')?.innerText?.slice(0, 160).replace(/\s+/g, ' ') || '');
    const bad = await p.evaluate(() => { const e = document.getElementById('blazor-error-ui'); return e && getComputedStyle(e).display !== 'none'; });
    const name = (user + r).replace(/[\/?=&]/g, '_');
    await p.screenshot({ path: `shots/${name}.png`, fullPage: false });
    console.log((bad || errs.length ? 'FAIL ' : 'ok   ') + r + ' | ' + txt + (errs.length ? ' | ' + errs.join(' ;; ').slice(0, 300) : ''));
  }
  await b.close();
})();
