# Homepage

Homepage is the public storefront root after installation: store branding, navigation, and sample catalog content the shopper sees first.

## Sub-features

- `home-load` serves `/` with HTTP 200 and does not redirect to `/install`.
- `home-brand` shows the store header logo.
- `home-catalog-chrome` shows product tiles and/or topic blocks from sample data.

## How to get to it (user POV)

- Open the store root URL in a browser.
- Follow the site logo link from an inner page back to home.

## Driving it with control-nopcommerce

Preconditions:

- `control-nopcommerce doctor` reports `installed=true` and a URL on port 5100–5199.
- Sample data was installed (`InstallSampleData=true`).

- **Open home.** Navigate to `/`. Run `control-nopcommerce browser homepage-proof`. The page URL has no `/install`, status is 200, and `.header-logo` is present.
- **Confirm catalog chrome.** Observe product tiles or topic content. The proof writes `markers.json` with `productCount` and `hasLogo=true`.
- **Proof.** Artifacts land under `/opt/cursor/artifacts/verify-nopcommerce/<run-id>/homepage/` as `home.png` and `home.aria.txt`, both showing store chrome.

## Gotchas

- Before `install`, `/` redirects to `/install`. That is not a homepage pass.
- The shared instance on `:5000` may look identical — doctor must pass for *this* run first.
- Homepage can return 200 while CSS/images still load; `homepage-proof` waits for `.header-logo` / `.product-item` / `.topic-block`.
