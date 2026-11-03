#!/usr/bin/env node
/**
 * Playwright driver for verify-nopcommerce.
 * Invoked via: control-nopcommerce browser <subcommand> ...
 */
import { chromium } from 'playwright';
import fs from 'node:fs';
import path from 'node:path';

const url = process.env.NOP_VERIFY_URL;
const evidenceDir = process.env.NOP_VERIFY_EVIDENCE_DIR;
const adminEmail = process.env.NOP_VERIFY_ADMIN_EMAIL || 'admin@nopverify.local';
const adminPassword = process.env.NOP_VERIFY_ADMIN_PASSWORD || 'NopVerify!23456';

if (!url || !evidenceDir) {
  console.error('NOP_VERIFY_URL and NOP_VERIFY_EVIDENCE_DIR are required');
  process.exit(1);
}

function usage() {
  console.log(`Usage: control-nopcommerce browser <command>

Commands:
  goto <path>                         Navigate to path on the verify instance
  click --role <role> --name <name>   Click by ARIA role+name (CSS fallback: --selector)
  fill --role|--selector ... --value  Fill an input
  press --key <key>
  wait --text <text> [--timeout ms]
  snapshot --aria --path <file>
  screenshot --path <file>
  homepage-proof [--outdir <dir>]     Drive homepage feature and capture proof
  search-proof --query <q> [--outdir]
  add-to-cart-proof --product <name> [--outdir]
  login-proof [--email] [--password] [--outdir]
  admin-proof [--email] [--password] [--outdir]  Login + /Admin in one session
`);
}

async function withPage(fn) {
  const browser = await chromium.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-dev-shm-usage'],
  });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
    ignoreHTTPSErrors: true,
  });
  const page = await context.newPage();
  try {
    return await fn(page);
  } finally {
    await browser.close();
  }
}

function ensureDir(p) {
  fs.mkdirSync(path.dirname(p), { recursive: true });
}

async function resolveLocator(page, args) {
  if (args.selector) return page.locator(args.selector).first();
  if (args.role && args.name) return page.getByRole(args.role, { name: args.name }).first();
  if (args.label) return page.getByLabel(args.label).first();
  throw new Error('need --selector or --role/--name or --label');
}

function parseArgs(argv) {
  const out = { _: [] };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a.startsWith('--')) {
      const key = a.slice(2);
      const val = argv[i + 1] && !argv[i + 1].startsWith('--') ? argv[++i] : true;
      out[key] = val;
    } else {
      out._.push(a);
    }
  }
  return out;
}

async function ariaSnapshot(page) {
  // Prefer Playwright's accessibility snapshot when available.
  try {
    return await page.locator('body').ariaSnapshot();
  } catch {
    return await page.evaluate(() => document.body?.innerText?.slice(0, 20000) || '');
  }
}

async function cmdGoto(args) {
  const p = args._[0] || '/';
  await withPage(async (page) => {
    const target = p.startsWith('http') ? p : `${url}${p.startsWith('/') ? p : `/${p}`}`;
    const resp = await page.goto(target, { waitUntil: 'domcontentloaded', timeout: 60000 });
    console.log(JSON.stringify({ ok: true, status: resp?.status(), url: page.url() }));
  });
}

async function cmdClick(args) {
  await withPage(async (page) => {
    await page.goto(url, { waitUntil: 'domcontentloaded' });
    if (args.url) await page.goto(args.url.startsWith('http') ? args.url : `${url}${args.url}`, { waitUntil: 'domcontentloaded' });
    const loc = await resolveLocator(page, args);
    await loc.click({ timeout: 15000 });
    console.log(JSON.stringify({ ok: true, url: page.url() }));
  });
}

async function cmdFill(args) {
  await withPage(async (page) => {
    const start = args.url ? (args.url.startsWith('http') ? args.url : `${url}${args.url}`) : url;
    await page.goto(start, { waitUntil: 'domcontentloaded' });
    const loc = await resolveLocator(page, args);
    await loc.fill(String(args.value ?? ''), { timeout: 15000 });
    console.log(JSON.stringify({ ok: true }));
  });
}

async function cmdScreenshot(args) {
  const out = args.path || path.join(evidenceDir, 'screenshot.png');
  ensureDir(out);
  await withPage(async (page) => {
    await page.goto(args.url ? (args.url.startsWith('http') ? args.url : `${url}${args.url}`) : url, {
      waitUntil: 'networkidle',
      timeout: 60000,
    });
    await page.screenshot({ path: out, fullPage: true });
    console.log(JSON.stringify({ ok: true, path: out, pageUrl: page.url() }));
  });
}

async function cmdSnapshot(args) {
  const out = args.path || path.join(evidenceDir, 'aria.txt');
  ensureDir(out);
  await withPage(async (page) => {
    await page.goto(args.url ? (args.url.startsWith('http') ? args.url : `${url}${args.url}`) : url, {
      waitUntil: 'domcontentloaded',
      timeout: 60000,
    });
    const snap = await ariaSnapshot(page);
    fs.writeFileSync(out, snap);
    console.log(JSON.stringify({ ok: true, path: out }));
  });
}

async function homepageProof(args) {
  const outdir = args.outdir || path.join(evidenceDir, 'homepage');
  fs.mkdirSync(outdir, { recursive: true });
  await withPage(async (page) => {
    const resp = await page.goto(url + '/', { waitUntil: 'networkidle', timeout: 90000 });
    if (page.url().includes('/install')) {
      throw new Error('homepage redirected to /install — run control-nopcommerce install first');
    }
    const status = resp?.status();
    await page.waitForSelector('.header-logo, .master-wrapper-content, .product-item, .topic-block', { timeout: 30000 });
    const title = await page.title();
    const hasLogo = await page.locator('.header-logo').count();
    const productCount = await page.locator('.product-item, .product-title').count();
    const text = await page.locator('body').innerText();
    const markers = {
      status,
      title,
      hasLogo: hasLogo > 0,
      productCount,
      mentionsNop: /nopcommerce|your store|computers|electronics|apparel/i.test(text),
    };
    if (status !== 200 || !markers.hasLogo) {
      throw new Error(`homepage proof failed: ${JSON.stringify(markers)}`);
    }
    const shot = path.join(outdir, 'home.png');
    const aria = path.join(outdir, 'home.aria.txt');
    await page.screenshot({ path: shot, fullPage: true });
    fs.writeFileSync(aria, await ariaSnapshot(page));
    fs.writeFileSync(path.join(outdir, 'markers.json'), JSON.stringify(markers, null, 2));
    console.log(JSON.stringify({ ok: true, feature: 'homepage', outdir, markers, shot, aria }));
  });
}

async function searchProof(args) {
  const query = args.query || 'MacBook';
  const outdir = args.outdir || path.join(evidenceDir, 'search');
  fs.mkdirSync(outdir, { recursive: true });
  await withPage(async (page) => {
    await page.goto(url + '/', { waitUntil: 'domcontentloaded', timeout: 60000 });
    const box = page.locator('#small-searchterms');
    await box.fill(query);
    await Promise.all([
      page.waitForURL(/\/search/, { timeout: 30000 }),
      page.locator('button.search-box-button, .search-box-button').first().click(),
    ]);
    await page.waitForSelector('.product-item, .no-result, .search-results, .item-grid', { timeout: 30000 });
    const body = await page.locator('body').innerText();
    const matched = body.toLowerCase().includes(query.toLowerCase()) || (await page.locator('.product-title').count()) > 0;
    if (!matched) throw new Error(`search proof failed for query=${query}`);
    const shot = path.join(outdir, 'results.png');
    const aria = path.join(outdir, 'results.aria.txt');
    await page.screenshot({ path: shot, fullPage: true });
    fs.writeFileSync(aria, await ariaSnapshot(page));
    fs.writeFileSync(path.join(outdir, 'markers.json'), JSON.stringify({ query, url: page.url(), matched }, null, 2));
    console.log(JSON.stringify({ ok: true, feature: 'search', outdir, query, pageUrl: page.url() }));
  });
}

async function addToCartProof(args) {
  const product = args.product || 'Apple MacBook Pro';
  const outdir = args.outdir || path.join(evidenceDir, 'add-to-cart');
  fs.mkdirSync(outdir, { recursive: true });
  await withPage(async (page) => {
    await page.goto(url + '/', { waitUntil: 'domcontentloaded', timeout: 60000 });
    // Search then open product
    await page.locator('#small-searchterms').fill(product);
    await Promise.all([
      page.waitForURL(/\/search/, { timeout: 30000 }),
      page.locator('button.search-box-button, .search-box-button').first().click(),
    ]);
    const productLink = page.locator('.product-title a', { hasText: product }).first();
    await productLink.click();
    await page.waitForSelector('.product-name, .add-to-cart-button, .product-box-add-to-cart-button', { timeout: 30000 });
    const beforeShot = path.join(outdir, 'product-before-add.png');
    await page.screenshot({ path: beforeShot, fullPage: true });

    const addBtn = page.locator('.add-to-cart-button, .product-box-add-to-cart-button').first();
    await addBtn.click();
    // Wait for flyout/bar or cart qty change
    await page.waitForTimeout(1500);
    await page.goto(url + '/cart/', { waitUntil: 'domcontentloaded', timeout: 60000 });
    await page.waitForSelector('.cart, .order-summary-content, .table-wrapper', { timeout: 30000 });
    const cartText = await page.locator('body').innerText();
    if (!cartText.toLowerCase().includes(product.toLowerCase().split(' ')[0].toLowerCase()) &&
        !(await page.locator('.product-name, .product a').count())) {
      throw new Error(`cart does not show product after add: ${product}`);
    }
    const shot = path.join(outdir, 'cart-after-add.png');
    const aria = path.join(outdir, 'cart.aria.txt');
    await page.screenshot({ path: shot, fullPage: true });
    fs.writeFileSync(aria, await ariaSnapshot(page));
    fs.writeFileSync(path.join(outdir, 'markers.json'), JSON.stringify({ product, cartUrl: page.url() }, null, 2));
    console.log(JSON.stringify({ ok: true, feature: 'add-to-cart', outdir, product, cartUrl: page.url() }));
  });
}

async function loginProof(args) {
  const email = args.email || adminEmail;
  const password = args.password || adminPassword;
  const outdir = args.outdir || path.join(evidenceDir, 'login');
  fs.mkdirSync(outdir, { recursive: true });
  await withPage(async (page) => {
    await page.goto(url + '/login', { waitUntil: 'domcontentloaded', timeout: 60000 });
    await page.locator('#Email, input.email, input[name="Email"]').first().fill(email);
    await page.locator('#Password, input.password, input[name="Password"]').first().fill(password);
    await Promise.all([
      page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 60000 }).catch(() => null),
      page.locator('button.login-button, .login-button').first().click(),
    ]);
    // Admin users land on storefront; account link or admin link should appear
    const body = await page.locator('body').innerText();
    const loggedIn = /log out|logout|my account|admin/i.test(body) && !/returning customer/i.test(await page.title());
    const stillLogin = page.url().includes('/login');
    if (stillLogin && /login was unsuccessful|no customer account/i.test(body)) {
      throw new Error('login failed: credentials rejected');
    }
    const shot = path.join(outdir, 'after-login.png');
    const aria = path.join(outdir, 'after-login.aria.txt');
    await page.screenshot({ path: shot, fullPage: true });
    fs.writeFileSync(aria, await ariaSnapshot(page));
    fs.writeFileSync(path.join(outdir, 'markers.json'), JSON.stringify({ email, url: page.url(), loggedIn, stillLogin }, null, 2));
    if (stillLogin) throw new Error('still on /login after submit');
    console.log(JSON.stringify({ ok: true, feature: 'login', outdir, email, pageUrl: page.url() }));
  });
}

async function adminProof(args) {
  const email = args.email || adminEmail;
  const password = args.password || adminPassword;
  const outdir = args.outdir || path.join(evidenceDir, 'admin');
  fs.mkdirSync(outdir, { recursive: true });
  await withPage(async (page) => {
    // Anonymous gate in this same browser before login cookies exist
    await page.goto(url + '/Admin', { waitUntil: 'domcontentloaded', timeout: 60000 });
    const anonUrl = page.url();
    await page.screenshot({ path: path.join(outdir, 'anonymous.png'), fullPage: true });
    fs.writeFileSync(path.join(outdir, 'anonymous.markers.json'), JSON.stringify({ url: anonUrl }, null, 2));

    await page.goto(url + '/login', { waitUntil: 'domcontentloaded', timeout: 60000 });
    await page.locator('#Email, input.email, input[name="Email"]').first().fill(email);
    await page.locator('#Password, input.password, input[name="Password"]').first().fill(password);
    await Promise.all([
      page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 60000 }).catch(() => null),
      page.locator('button.login-button, .login-button').first().click(),
    ]);
    if (page.url().includes('/login')) throw new Error('admin-proof: login failed');

    await page.goto(url + '/Admin', { waitUntil: 'domcontentloaded', timeout: 90000 });
    await page.waitForTimeout(1000);
    const body = await page.locator('body').innerText();
    const title = await page.title();
    const looksAdmin = /dashboard|administration|nopcommerce|catalog|configuration|sales/i.test(`${title}\n${body}`);
    if (!looksAdmin || page.url().toLowerCase().includes('/login')) {
      throw new Error(`admin-proof: /Admin did not show admin chrome (${page.url()})`);
    }
    const shot = path.join(outdir, 'dashboard.png');
    const aria = path.join(outdir, 'dashboard.aria.txt');
    await page.screenshot({ path: shot, fullPage: true });
    fs.writeFileSync(aria, await ariaSnapshot(page));
    fs.writeFileSync(path.join(outdir, 'markers.json'), JSON.stringify({ email, url: page.url(), title, looksAdmin }, null, 2));
    console.log(JSON.stringify({ ok: true, feature: 'admin', outdir, pageUrl: page.url() }));
  });
}

const argv = process.argv.slice(2);
const cmd = argv[0];
const args = parseArgs(argv.slice(1));

const map = {
  goto: cmdGoto,
  click: cmdClick,
  fill: cmdFill,
  screenshot: cmdScreenshot,
  snapshot: cmdSnapshot,
  'homepage-proof': homepageProof,
  'search-proof': searchProof,
  'add-to-cart-proof': addToCartProof,
  'login-proof': loginProof,
  'admin-proof': adminProof,
  help: async () => usage(),
  '--help': async () => usage(),
};

if (!cmd || !map[cmd]) {
  usage();
  process.exit(cmd ? 1 : 0);
}

map[cmd](args).catch((err) => {
  console.error(String(err?.stack || err));
  process.exit(1);
});
