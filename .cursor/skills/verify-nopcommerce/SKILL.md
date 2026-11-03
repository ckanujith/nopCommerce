---
name: verify-nopcommerce
description: Drive the nopCommerce storefront and admin web UI on an isolated local instance (PostgreSQL + Kestrel). Use when proving a user-facing change works end-to-end, reproducing a storefront/admin bug, or capturing screenshots/ARIA evidence — not for unit tests alone.
---

# verify-nopcommerce

Agent-facing recipe for proving nopCommerce behavior through the real web UI. Read cold: this repo is an ASP.NET Core eCommerce app (`Nop.Web`) with a public storefront and `/Admin` area. Primary surface is the **web UI**. Secondary surfaces (Web API plugin, unit tests under `src/Tests`) are out of scope for this skill unless a feature file says otherwise.

## Launch

Never drive the shared Cloud Agent start-script instance on **port 5000**. That process owns the source tree `App_Data` and the `nopcommerce` database. Verification always uses `control-nopcommerce`, which copies a published build into `/tmp/nopcommerce-verify/<run-id>/app`, uses a disposable Postgres DB `nop_verify_*`, and binds **127.0.0.1:5100–5199**.

```bash
# From repo root
chmod +x .cursor/skills/verify-nopcommerce/helpers/control-nopcommerce

# 1) Start isolated instance (publishes Debug Nop.Web on first run into /tmp/nopcommerce-verify-cache/publish)
.cursor/skills/verify-nopcommerce/helpers/control-nopcommerce launch

# Ready when GET /install returns 200 (pre-install) or GET / returns storefront 200 (post-install).
# Log: /tmp/nopcommerce-verify/<run-id>/nopcommerce.log
# State: /tmp/nopcommerce-verify/<run-id>/state.json  (also /tmp/nopcommerce-verify/current)

# 2) Install with sample data (admin@nopverify.local / NopVerify!23456)
.cursor/skills/verify-nopcommerce/helpers/control-nopcommerce install

# The wizard HTML normally restarts via browser JS. The helper detects the restart
# marker, recycles only processes running Nop.Web.dll (never the bash watchdog), and
# waits until homepage 200 with store markers (.header-logo / .product-item).
```

**Verification scaffolding:** publish excludes `App_Data/*.json` (`Nop.Web.csproj`). Launch seeds `App_Data/appsettings.json` into the run directory only (cleared connection string). That file lives under `/tmp/...` and is removed on cleanup.

**Teardown:**

```bash
.cursor/skills/verify-nopcommerce/helpers/control-nopcommerce cleanup
```

Cleanup stops the watchdog and Nop.Web processes whose cwd is this run’s `app_dir`, drops the disposable DB, and deletes `/tmp/nopcommerce-verify/<run-id>`. It does **not** delete evidence under `/opt/cursor/artifacts/verify-nopcommerce/<run-id>/`.

**Isolation:** two runs may execute side by side (distinct ports + DBs + app dirs). Do not point two runs at the same `--run-id`. Refuse any URL on port 5000.

## Doctor

```bash
.cursor/skills/verify-nopcommerce/helpers/control-nopcommerce doctor
```

Read-only. Passes only if:

- `NOP_VERIFY_RUN_ID` / `current` points at a run started by this helper
- watchdog PID is alive
- port is not 5000 and is owned by this run’s `app_dir` when detectable
- pre-install: `/` redirects or serves install; post-install: homepage 200 with store markers

Run doctor first whenever anything looks off. If doctor fails, do not drive.

## Drive

Put helpers on your mental PATH as `control-nopcommerce`. Prefer the feature map under `features/` before improvising.

Harness: Playwright via Node (`helpers/browser.mjs`), wrapped by the bash controller.

```bash
control-nopcommerce browser homepage-proof
control-nopcommerce browser search-proof --query "MacBook"
control-nopcommerce browser add-to-cart-proof --product "Apple MacBook Pro"
control-nopcommerce browser login-proof
control-nopcommerce browser admin-proof
control-nopcommerce browser goto /cart/
control-nopcommerce browser screenshot --path /opt/cursor/artifacts/verify-nopcommerce/$RUN/manual.png --url /
control-nopcommerce browser snapshot --aria --path /opt/cursor/artifacts/verify-nopcommerce/$RUN/manual.aria.txt
```

Stable handles (prefer these over coordinates):

| UI | Handle |
|----|--------|
| Search box | `#small-searchterms` (ARIA label from `Search.SearchBox.Text.Label`) |
| Search submit | `button.search-box-button` |
| Product title link | `.product-title a` |
| Add to cart (details) | `.add-to-cart-button` / `#add-to-cart-button-{id}` |
| Add to cart (catalog box) | `.product-box-add-to-cart-button` |
| Cart page | `/cart/` |
| Login email/password | `#Email` / `#Password` or `input.email` / `input.password` |
| Login submit | `button.login-button` |
| Store logo | `.header-logo` |
| Admin area | `/Admin` (after admin login) |

Default verify admin (set at install): `admin@nopverify.local` / `NopVerify!23456`. Sample catalog includes **Apple MacBook Pro**, **Build your own computer**, categories **Computers** / **Electronics**.

## Evidence

Proof artifacts go to `/opt/cursor/artifacts/verify-nopcommerce/<run-id>/` (also recorded as `evidence_dir` in `state.json`).

Proof standards:

- Exercise the real user path (browser), not EF seeds, admin APIs, or test-only endpoints.
- Capture the **action** and the **resulting state** (e.g. product page before add + cart after).
- Verify side effects where visible (cart line items, logged-in chrome, admin dashboard).
- Include app identity in screenshots (`.header-logo` / nopCommerce chrome).
- Record feature id + entry point with each artifact (`markers.json` from `*-proof` commands).

Mocks: none for core storefront paths. External payment/shipping providers are out of scope unless a feature file names a dry-run boundary — then observe files/network rather than trusting the label.

## Cleanup

```bash
control-nopcommerce cleanup
```

- Kills only the watchdog PID and processes whose cwd is this run’s `app_dir` (never `pkill -f`, never touch :5000).
- Drops `nop_verify_*` database for the run.
- Deletes `/tmp/nopcommerce-verify/<run-id>` scratch.
- **Retains** `/opt/cursor/artifacts/verify-nopcommerce/<run-id>/`.

After cleanup, confirm evidence still exists before declaring success.

## Helpers

| Helper | Invocation |
|--------|------------|
| Controller | `.cursor/skills/verify-nopcommerce/helpers/control-nopcommerce <launch\|doctor\|install\|browser\|http\|cleanup>` |
| Browser | `control-nopcommerce browser …` → `helpers/browser.mjs` |
| Playwright deps | `helpers/package.json` (`npm install` already expected in that directory) |

First-time machine needs: .NET SDK matching `global.json` (10.0.401), PostgreSQL with role `nopcommerce` / password `nopCommerce_db_password` (same as `/opt/cursor/nopcommerce-env`), Node 22+, Chrome/Chromium for Playwright.

Feature recipes: [`features/README.md`](features/README.md).
