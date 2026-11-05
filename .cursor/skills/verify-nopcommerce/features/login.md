# Login

Login lets a registered customer (including the install-time admin) sign in with email and password from `/login`.

## Sub-features

- `login-open` opens the login page.
- `login-success` accepts the verify admin credentials and leaves `/login`.
- `login-reject` keeps the user on login when credentials are wrong.

## How to get to it (user POV)

- Choose Log in from the header.
- Open `/login` directly.

## Driving it with control-nopcommerce

Preconditions:

- Installed storefront.
- Admin email/password from `state.json` (defaults `admin@nopverify.local` / `NopVerify!23456`).
- `control-nopcommerce doctor` passes.

- **Open login.** Go to `/login`. Run `control-nopcommerce browser goto /login` or use `login-proof` which opens it.
- **Sign in.** Submit email and password. Run `control-nopcommerce browser login-proof`. The browser leaves `/login` and the page shows logged-in chrome (Log out / My account / Admin).
- **Reject path.** Fill a wrong password manually with `browser fill` + `browser click` if verifying failure; expect to remain on `/login` with an error. Do not treat a skip of this path as a pass.
- **Proof.** `/opt/cursor/artifacts/verify-nopcommerce/<run-id>/login/after-login.png` shows post-login chrome, not the Returning Customer form.

## Gotchas

- Username mode exists only when store settings enable usernames; sample install uses email.
- Admin and public store share the same login form; landing page after login may be storefront home, not `/Admin`.
- Do not assert login by writing cookies; use the form.
