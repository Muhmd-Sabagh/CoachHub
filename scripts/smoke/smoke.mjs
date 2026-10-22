// Browser smoke test (P10A): seeds a running API, then signs in as each role in headless Chromium at phone and
// desktop widths, opens every screen, and records stuck loading states, console errors, failed requests and a
// screenshot per page. P18 added the Arabic (right-to-left) runs and basic accessibility checks. It needs the API
// and the Angular dev server running; see README.md.
import { chromium } from 'playwright';
import { mkdir, writeFile } from 'node:fs/promises';

const web = process.env.SMOKE_WEB ?? 'http://localhost:65194';
const api = process.env.SMOKE_API ?? 'http://localhost:5197';
const admin = { login: process.env.SMOKE_ADMIN_EMAIL ?? 'admin@smoke.test', password: process.env.SMOKE_ADMIN_PASSWORD };
const out = process.env.SMOKE_OUT ?? 'smoke-results';
const tag = Date.now().toString(36).slice(-5);
const password = `Smoke-${tag}-Passw0rd!`;
const today = new Date().toISOString().slice(0, 10);
const languages = (process.env.SMOKE_LANGS ?? 'en,ar').split(',').map((x) => x.trim()).filter(Boolean);

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
  '/check-ins', '/nutrition', '/training', '/plans', '/operations', '/audit', '/settings/branding', '/settings/reference-data', '/account',
];
const adminPages = (clientId) => [...staffPages(clientId), '/coaches'];
const clientPages = ['/app', '/app/nutrition', '/app/training', '/app/progress', '/app/tests', '/app/calendar', '/app/chat', '/app/files',
  '/app/forms', '/app/account'];
// The client profile's tabs, opened by their buttons.
const profileTabs = ['Overview', 'Profile', 'Training', 'Nutrition', 'Food diary', 'Progress', 'Measurements', 'Photos', 'Check-ins', 'Tests',
  'Calendar', 'Reports', 'Messages', 'Files'];

const viewports = { phone: { width: 390, height: 844 }, desktop: { width: 1366, height: 900 } };

/**
 * Basic accessibility checks on what is visible (P18): form controls without an accessible name, buttons and links
 * without a name, images without alt text, and the document language and direction.
 */
function accessibility(language) {
  const visible = (el) => { const r = el.getBoundingClientRect(); return r.width > 0 && r.height > 0 && getComputedStyle(el).visibility !== 'hidden'; };
  const named = (el) => Boolean((el.getAttribute('aria-label') ?? '').trim()
    || (el.getAttribute('aria-labelledby') ?? '').split(' ').some((id) => document.getElementById(id)?.textContent?.trim())
    || (el.labels && [...el.labels].some((l) => l.textContent.trim()))
    || (el.getAttribute('title') ?? '').trim());
  const issues = [];
  for (const el of document.querySelectorAll('input:not([type=hidden]), select, textarea')) {
    if (visible(el) && !named(el) && !(el.getAttribute('placeholder') ?? '').trim()) issues.push(`unlabelled ${el.tagName.toLowerCase()}${el.name ? ` "${el.name}"` : ''}`);
  }
  for (const el of document.querySelectorAll('button, a[href]')) {
    if (visible(el) && !el.textContent.trim() && !named(el) && !el.querySelector('img[alt]:not([alt=""]), [aria-label]')) issues.push(`unnamed ${el.tagName.toLowerCase()}.${el.className}`);
  }
  for (const el of document.querySelectorAll('img')) if (visible(el) && !el.hasAttribute('alt')) issues.push(`image without alt ${el.src.slice(-40)}`);
  if (document.documentElement.lang !== language) issues.push(`html lang is "${document.documentElement.lang}"`);
  if (document.documentElement.dir !== (language === 'ar' ? 'rtl' : 'ltr')) issues.push(`html dir is "${document.documentElement.dir}"`);
  return [...new Set(issues)];
}

/**
 * English left on an Arabic page (P19): visible text and labels with Latin words, outside elements marked
 * translate="no" (names, user text and other data). Product names, units and abbreviations are allowed.
 */
function untranslated() {
  const allowed = /\b(RPE|RIR|e1RM|1RM|PDF|CSV|ZIP|JPEG|PNG|WebP|Word|Excel|PowerPoint|IP|YouTube|Apple Health|Health Connect|English|CoachHub|EN|kg|ml|cm|bpm|Chrome|Firefox|Edge|Safari|Windows|Android|iOS|macOS|Linux)\b/g;
  const english = (text) => /[A-Za-z]{3,}/.test(text.replace(allowed, ' '));
  const visible = (el) => { const r = el.getBoundingClientRect(); return r.width > 0 && r.height > 0 && getComputedStyle(el).visibility !== 'hidden'; };
  const exempt = (el) => !el || el.closest('[translate="no"], script, style, noscript, option, svg, code');
  const found = new Set();
  const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    const text = node.textContent.trim();
    if (text && english(text) && !exempt(node.parentElement) && visible(node.parentElement)) found.add(text.slice(0, 60));
  }
  for (const el of document.querySelectorAll('[aria-label], [placeholder], [title], img[alt]')) {
    if (exempt(el) || !visible(el)) continue;
    for (const name of ['aria-label', 'placeholder', 'title', 'alt']) {
      const value = (el.getAttribute(name) ?? '').trim();
      if (value && english(value)) found.add(`${name}="${value.slice(0, 50)}"`);
    }
  }
  return [...found];
}

/** "Loading…" in either language: a screen still showing it after the wait is stuck (P10A). */
const loading = /^\s*(Loading|جارٍ التحميل)/i;

/** Roles whose screens must be fully Arabic (P19: the client area; P21 adds the staff roles). */
const translatedRoles = (process.env.SMOKE_TRANSLATED_ROLES ?? 'client').split(',').map((x) => x.trim());

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
    stuck = await page.getByText(loading).first().isVisible().catch(() => false);
  } catch (error) {
    errors.push(`navigation: ${error.message.split('\n')[0]}`);
  }
  const file = `${role}-${page.language}-${viewport}-${path.replace(/[^a-z0-9]+/gi, '_').replace(/^_|_$/g, '') || 'home'}.png`;
  await page.screenshot({ path: `${out}/${file}`, fullPage: true }).catch(() => undefined);
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1).catch(() => false);
  const a11y = await page.evaluate(accessibility, page.language).catch(() => []);
  const english = page.language === 'ar' && translatedRoles.includes(role) ? await page.evaluate(untranslated).catch(() => []) : [];
  // The client area is mobile first (P18): on a phone its tables must fit, not scroll sideways inside their card.
  if (role === 'client' && viewport === 'phone') {
    const wide = await page.evaluate(() => [...document.querySelectorAll('.table-scroll')]
      .filter((x) => x.getBoundingClientRect().width > 0 && x.scrollWidth > x.clientWidth + 1).length).catch(() => 0);
    if (wide) a11y.push(`${wide} table(s) scroll sideways on a phone`);
  }
  page.off('console', onConsole); page.off('pageerror', onError); page.off('response', onResponse);
  results.push({ role, language: page.language, viewport, path, stuck, overflow, a11y, english, errors, failed, screenshot: file });
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
const only = process.env.SMOKE_ROLES?.split(',').map((x) => x.trim());
for (const language of languages) for (const { role, who, pages } of roles.filter((x) => !only || only.includes(x.role))) {
  for (const [viewport, size] of Object.entries(viewports)) {
    const context = await browser.newContext({ viewport: size, locale: language });
    // The app keeps the chosen language in localStorage (I18nService); Arabic switches the document to right-to-left.
    await context.addInitScript((value) => localStorage.setItem('coachhub.language', value), language);
    const page = await context.newPage();
    page.language = language;
    await signIn(page, who);
    for (const path of pages) await visit(page, role, viewport, path, results);
    if (role === 'coachA') {
      await page.goto(`${web}/clients/${seeded.clientId}`, { waitUntil: 'networkidle' });
      for (const tab of profileTabs) {
        const button = page.getByRole('tab', { name: tab, exact: true }).first();
        if (!(await button.isVisible().catch(() => false))) { results.push({ role, language, viewport, path: `profile tab ${tab}`, stuck: false, overflow: false, a11y: [], english: [], errors: ['tab not found'], failed: [], screenshot: '' }); continue; }
        await button.click();
        await page.waitForLoadState('networkidle');
        await page.waitForTimeout(1200);
        const stuck = await page.getByText(loading).first().isVisible().catch(() => false);
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1);
        const a11y = await page.evaluate(accessibility, language).catch(() => []);
        const file = `${role}-${language}-${viewport}-profile-${tab.replace(/\W+/g, '_')}.png`;
        await page.screenshot({ path: `${out}/${file}`, fullPage: true });
        results.push({ role, language, viewport, path: `profile tab ${tab}`, stuck, overflow, a11y, english: [], errors: [], failed: [], screenshot: file });
      }
    }
    await context.close();
  }
}
await browser.close();

const problems = results.filter((x) => x.stuck || x.overflow || x.a11y.length || x.english.length || x.errors.length || x.failed.length);
await writeFile(`${out}/results.json`, JSON.stringify({ seeded: { clientId: seeded.clientId, tag }, results }, null, 2));
console.log(`Visited ${results.length} pages; ${problems.length} with findings.`);
for (const p of problems) console.log(`- ${p.role} ${p.language} ${p.viewport} ${p.path}: ${[p.stuck && 'stuck on Loading', p.overflow && 'horizontal scroll', ...p.a11y, ...p.english.map((x) => `English: ${x}`), ...p.errors, ...p.failed].filter(Boolean).join(' | ')}`);
