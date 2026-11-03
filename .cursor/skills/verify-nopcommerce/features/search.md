# Search

Search lets a shopper find catalog products from the header search box and distinguish matches from an empty result set.

## Sub-features

- `search-open` focuses the header search box.
- `search-match` returns products for a known sample query such as `MacBook`.
- `search-empty` shows a no-result state for a nonsense query.

## How to get to it (user POV)

- Type into the header search field and submit Search.
- Open `/search?q=<query>` directly.

## Driving it with control-nopcommerce

Preconditions:

- Installed storefront with sample data.
- `control-nopcommerce doctor` passes.

- **Match query.** Search for MacBook. Run `control-nopcommerce browser search-proof --query "MacBook"`. The browser reaches a `/search` URL and shows at least one product title (e.g. Apple MacBook Pro).
- **Empty query.** Search for a nonsense term. Run `control-nopcommerce browser search-proof --query "zzznopmatch999"`. Expect a results page whose body has no MacBook product tile (or shows the store’s no-result messaging). If the empty state is missing from the theme, record the unmet UI marker in Gotchas for this run — do not claim `search-empty` passed.
- **Proof.** Capture `/opt/cursor/artifacts/verify-nopcommerce/<run-id>/search/results.png` and `results.aria.txt` with the query visible or reflected in results.

## Gotchas

- Minimum search term length may apply; empty submit can alert instead of navigating.
- Autocomplete is delayed (~500ms) and is not required for proof — submit the form.
- Category dropdown (`cid`) changes advanced search flags; leave it at the default unless testing categories.
