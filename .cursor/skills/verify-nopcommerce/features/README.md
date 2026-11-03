# nopCommerce verification map

This directory is the maintained source for verifying user-facing behavior of the nopCommerce storefront and admin. Read this index before driving the app, then use the matching feature file as the recipe.

## Baseline preconditions

- Launch with `.cursor/skills/verify-nopcommerce/helpers/control-nopcommerce launch` (isolated port 5100–5199, disposable DB).
- Run `control-nopcommerce install` so sample data is present (unless the feature file says pre-install).
- Run `control-nopcommerce doctor` and require `installed=true`, the run URL, and a non-5000 port.
- Admin credentials from `state.json`: default `admin@nopverify.local` / `NopVerify!23456`.
- Never drive the shared Cloud Agent instance on port 5000 or any instance whose `app_dir` is not under `/tmp/nopcommerce-verify/<run-id>/`.

## Driving conventions

- Start every recipe from the baseline installed storefront unless preconditions say otherwise.
- Prefer ARIA roles, `#small-searchterms`, `.product-title`, `.add-to-cart-button`, and route paths (`/`, `/search`, `/cart/`, `/login`, `/Admin`) over coordinates.
- Treat every command as literal. Keep quoted product names unchanged.
- Run browser actions through `control-nopcommerce browser`.
- Restore cart/account state after mutations when the next recipe needs a clean baseline. Do not remove proof artifacts during cleanup.

## Proof and skip reporting

- Capture the user action and the resulting state, not only the final screen.
- UI proof includes an ARIA snapshot and a screenshot with `.header-logo` visible.
- Mutation proof includes a second user-facing view (e.g. `/cart/` after add-to-cart).
- Record the feature ID and entry point used with every artifact (`markers.json`).
- Report an unreachable path with the attempted command and the unmet precondition.
- Do not report a skipped entry point as verified through a different path.

## Feature entry contract

Each feature file starts with an H1 title and one paragraph describing the user-visible behavior. It then uses exactly four H2 sections in this order.

1. `Sub-features` lists short IDs with one line for each behavior.
2. `How to get to it (user POV)` lists every user entry point.
3. `Driving it with control-nopcommerce` starts with `Preconditions:` and uses labeled bullets that pair each user action with an exact command and observable result.
4. `Gotchas` lists traps that can waste or invalidate a verification run.

Keep implementation details out of the map. Name only user paths, stable handles, required state, commands, and observable proof.

## Features

- [Homepage](./homepage.md) covers the installed storefront home with logo and sample catalog chrome.
- [Search](./search.md) covers header search for sample products and empty results.
- [Add to cart](./add-to-cart.md) covers opening a sample product and confirming it on `/cart/`.
- [Login](./login.md) covers admin email/password sign-in from `/login`.
- [Admin dashboard](./admin.md) covers opening `/Admin` after admin login.
