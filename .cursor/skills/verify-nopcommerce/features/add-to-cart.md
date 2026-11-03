# Add to cart

Add to cart lets a shopper place a catalog product into the shopping cart and confirm the line on `/cart/`.

## Sub-features

- `cart-open-product` opens a sample product details page from search.
- `cart-add` clicks Add to cart on the product details page.
- `cart-confirm` shows the product on `/cart/`.

## How to get to it (user POV)

- From search or category listing, open a product, then choose Add to cart.
- From a product box on home/category, choose the box Add to cart button (simple products only).

## Driving it with control-nopcommerce

Preconditions:

- Installed storefront with sample data.
- Prefer product `Apple MacBook Pro` (simple product). Avoid `Build your own computer` until attribute selection is mapped — it is a grouped/complex product.
- `control-nopcommerce doctor` passes.

- **Find product.** Search and open Apple MacBook Pro. Run `control-nopcommerce browser add-to-cart-proof --product "Apple MacBook Pro"`. The driver opens details, clicks `.add-to-cart-button`, then opens `/cart/`.
- **Confirm cart.** The cart page body includes the product (or a cart line with the product name). Proof writes `product-before-add.png` and `cart-after-add.png`.
- **Proof.** Artifacts under `/opt/cursor/artifacts/verify-nopcommerce/<run-id>/add-to-cart/` include both screenshots plus `cart.aria.txt`.

## Gotchas

- Grouped or attribute-required products need option selection before Add to cart succeeds; do not weaken the journey by using an internal API.
- AJAX add-to-cart may show a bar briefly; the durable proof is `/cart/`, not the toast alone.
- Cart is session/cookie scoped to the Playwright context used by the proof command.
