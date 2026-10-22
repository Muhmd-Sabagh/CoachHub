// Browser smoke test (P10A): seeds a running API, then signs in as each role in headless Chromium at phone and
// desktop widths, opens every screen, and records stuck loading states, console errors, failed requests and a
// screenshot per page. It needs the API and the Angular dev server running; see README.md.
import { chromium } from 'playwright';
import { mkdir, writeFile } from 'node:fs/promises';

const web = process.env.SMOKE_WEB ?? 'http://localhost:65194';
const api = process.env.SMOKE_API ?? 'http://localhost:5197';
const admin = { login: process.env.SMOKE_ADMIN_EMAIL ?? 'admin@smoke.test', password: process.env.SMOKE_ADMIN_PASSWORD };
const out = process.env.SMOKE_OUT ?? 'smoke-results';
const tag = Date.now().toString(36).slice(-5);
const password = `Smoke-${tag}-Passw0rd!`;
const today = new Date().toISOString().slice(0, 10);

if (!admin.password) throw new Error('Set SMOKE_ADMIN_PASSWORD to the bootstrap administrator password.');

async function call(token, method, path, body) {
  const response = await fetch(api + path, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok) throw new Error(`${method} ${path} → ${response.status}: ${await response.text()}`);
  const text = await response.text();
  return text ? JSON.parse(text) : null;
}
const login = async (who) => (await call(null, 'POST', '/api/v1/auth/login', who)).accessToken;

/** Two coaches, a client with a login, and a little data on each screen. */
async function seed() {
  const adminToken = await login(admin);
  const coachA = { email: `coach.a.${tag}@smoke.test`, password };
  const coachB = { email: `coach.b.${tag}@smoke.test`, password };
  await call(adminToken, 'POST', '/api/v1/admin/coaches', { displayName: `Coach A ${tag}`, email: coachA.email, userName: null, password });
  await call(adminToken, 'POST', '/api/v1/admin/coaches', { displayName: `Coach B ${tag}`, email: coachB.email, userName: null, password });
  const coachToken = await login({ login: coachA.email, password });
  const client = await call(coachToken, 'POST', '/api/v1/clients', { name: `Smoke Client ${tag}`, phone: null, email: `client.${tag}@smoke.test`, joinDate: today });
  const clientLogin = { email: `client.${tag}@smoke.test`, password };
  await call(adminToken, 'POST', '/api/v1/accounts', { email: clientLogin.email, userName: null, displayName: `Smoke Client ${tag}`, password, role: 'Client', clientId: client.id });
  const clientToken = await login({ login: clientLogin.email, password });
  await call(clientToken, 'PUT', `/api/v1/me/progress/weight/${today}`, { weightKg: 81.2, notes: 'smoke' });
  await call(clientToken, 'PUT', `/api/v1/me/progress/steps/${today}`, { steps: 8400, notes: null });
  await call(clientToken, 'POST', '/api/v1/me/chat/messages', { text: 'Hello coach (smoke test)', mediaIds: [] });
  await call(coachToken, 'POST', `/api/v1/clients/${client.id}/chat/messages`, { text: 'Hi! (smoke test)', mediaIds: [] });
  await call(coachToken, 'PUT', `/api/v1/clients/${client.id}/calendar/training-days`, { weekdays: ['Monday', 'Wednesday', 'Friday'] });
  await call(coachToken, 'POST', '/api/v1/calendar/events', { clientId: client.id, kind: 'Event', title: 'Smoke event', date: today, visibleToClient: true });
  return { coachA, coachB, clientLogin, clientId: client.id };
}

const staffPages = (clientId) => [
  '/dashboard', '/clients', `/clients/${clientId}`, '/subscriptions', '/assessments', '/fitness-tests', '/calendar', '/messages',
  '/check-ins', '/nutrition', '/training', '/plans', '/operations', '/audit', '/settings', '/workspace', '/account',
];
const adminPages = (clientId) => [...staffPages(clientId), '/coaches'];
const clientPages = ['/app', '/app/nutrition', '/app/training', '/app/progress', '/app/tests', '/app/calendar', '/app/chat', '/app/files',
  '/app/forms', '/app/account'];
// The client profile's tabs, opened by their buttons.
const profileTabs = ['Overview', 'Profile', 'Training', 'Nutrition', 'Food diary', 'Progress', 'Measurements', 'Photos', 'Check-ins', 'Tests',
  'Calendar', 'Reports', 'Messages', 'Files'];

const viewports = { phone: { width: 390, height: 844 }, desktop: { width: 1366, height: 900 } };

async function visit(page, role, viewport, path, results) {
  const errors = [];
  const failed = [];
  const onConsole = (message) => { if (message.type() === 'error') errors.push(message.text()); };
  const onError = (error) => errors.push(String(error));
  const onResponse = (response) => { if (response.url().includes('/api/') && response.status() >= 500) failed.push(`${response.status()} ${response.url()}`); };
  page.on('console', onConsole); page.on('pageerror', onError); page.on('response', onResponse);
  let stuck = false;
  try {
    await page.goto(web + path, { waitUntil: 'networkidle', timeout: 30000 });
    // No user input: a screen must show its data on its own (P10A).
    await page.waitForTimeout(1500);
    stuck = await page.getByText(/^\s*Loading/i).first().isVisible().catch(() => false);
  } catch (error) {
    errors.push(`navigation: ${error.message.split('\n')[0]}`);
  }
  const file = `${role}-${viewport}-${path.replace(/[^a-z0-9]+/gi, '_').replace(/^_|_$/g, '') || 'home'}.png`;
  await page.screenshot({ path: `${out}/${file}`, fullPage: true }).catch(() => undefined);
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1).catch(() => false);
  page.off('console', onConsole); page.off('pageerror', onError); page.off('response', onResponse);
  results.push({ role, viewport, path, stuck, overflow, errors, failed, screenshot: file });
}

async function signIn(page, who) {
  await page.goto(web + '/login', { waitUntil: 'networkidle' });
  await page.locator('input[formcontrolname="login"]').fill(who.email ?? who.login);
  await page.locator('input[formcontrolname="password"]').fill(who.password);
  await Promise.all([page.waitForURL((url) => !url.pathname.startsWith('/login'), { timeout: 20000 }), page.locator('button[type="submit"]').click()]);
}

await mkdir(out, { recursive: true });
const seeded = await seed();
const browser = await chromium.launch();
const results = [];
const roles = [
  { role: 'admin', who: admin, pages: adminPages(seeded.clientId) },
  { role: 'coachA', who: seeded.coachA, pages: staffPages(seeded.clientId) },
  // Coach B must not see Coach A's client: the profile should say so, never show it.
  { role: 'coachB', who: seeded.coachB, pages: ['/dashboard', '/clients', `/clients/${seeded.clientId}`, '/messages', '/calendar'] },
  { role: 'client', who: seeded.clientLogin, pages: clientPages },
];
for (const { role, who, pages } of roles) {
  for (const [viewport, size] of Object.entries(viewports)) {
    const context = await browser.newContext({ viewport: size });
    const page = await context.newPage();
    await signIn(page, who);
    for (const path of pages) await visit(page, role, viewport, path, results);
    if (role === 'coachA') {
      await page.goto(`${web}/clients/${seeded.clientId}`, { waitUntil: 'networkidle' });
      for (const tab of profileTabs) {
        const button = page.getByRole('tab', { name: tab, exact: true }).first();
        if (!(await button.isVisible().catch(() => false))) { results.push({ role, viewport, path: `profile tab ${tab}`, stuck: false, overflow: false, errors: ['tab not found'], failed: [], screenshot: '' }); continue; }
        await button.click();
        await page.waitForLoadState('networkidle');
        await page.waitForTimeout(1200);
        const stuck = await page.getByText(/^\s*Loading/i).first().isVisible().catch(() => false);
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1);
        const file = `${role}-${viewport}-profile-${tab.replace(/\W+/g, '_')}.png`;
        await page.screenshot({ path: `${out}/${file}`, fullPage: true });
        results.push({ role, viewport, path: `profile tab ${tab}`, stuck, overflow, errors: [], failed: [], screenshot: file });
      }
    }
    await context.close();
  }
}
await browser.close();

const problems = results.filter((x) => x.stuck || x.overflow || x.errors.length || x.failed.length);
await writeFile(`${out}/results.json`, JSON.stringify({ seeded: { clientId: seeded.clientId, tag }, results }, null, 2));
console.log(`Visited ${results.length} pages; ${problems.length} with findings.`);
for (const p of problems) console.log(`- ${p.role} ${p.viewport} ${p.path}: ${[p.stuck && 'stuck on Loading', p.overflow && 'horizontal scroll', ...p.errors, ...p.failed].filter(Boolean).join(' | ')}`);
