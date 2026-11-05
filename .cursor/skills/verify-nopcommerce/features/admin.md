# Admin dashboard

Admin dashboard is the back-office home at `/Admin` for users with administrator permission.

## Sub-features

- `admin-gate` redirects anonymous users from `/Admin` to login.
- `admin-home` shows the admin dashboard after admin login.
- `admin-nav` exposes catalog or configuration navigation in the admin shell.

## How to get to it (user POV)

- After admin login, open `/Admin` or choose the Administration link when shown.
- Bookmark `/Admin` while authenticated.

## Driving it with control-nopcommerce

Preconditions:

- Installed storefront.
- Successful `login-proof` with the verify admin (or perform login in the same browser session).
- `control-nopcommerce doctor` passes.

- **Anonymous + authenticated in one session.** Run `control-nopcommerce browser admin-proof`. The driver first opens `/Admin` (expect login redirect), then signs in as the verify admin, then reopens `/Admin` and shows admin chrome (Dashboard / administration / catalog).
- **Proof.** Artifacts under `/opt/cursor/artifacts/verify-nopcommerce/<run-id>/admin/` include `anonymous.png`, `dashboard.png`, and `dashboard.aria.txt`.

## Gotchas

- Use `admin-proof` (single Playwright session). Separate `login-proof` then `goto /Admin` commands do not share cookies.
- Admin routes are under area `Admin`; public `.header-logo` alone is insufficient proof of admin.
