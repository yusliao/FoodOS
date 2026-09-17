import { Link, useNavigate } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Minus, Plus, ShoppingCart, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { getCart, placeOrder, updateCart, type CartLineInput } from "@/api/ordering";
import { useFulfillmentCapabilities } from "@/api/fulfillment";
import { WmsStatusNotice } from "@/components/wms-status";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import {
  EntityEmpty,
  EntityListCard,
  EntityListHeader,
  EntityListRow,
  EntityPageHeader,
  ErrorBand,
} from "@/components/list";
import { describe, formatMoney } from "@/lib/list-helpers";
import { useT } from "@/i18n/locale-provider";
import { useShopStore } from "./store-context";
import { quoteSourceLabel } from "./shop-helpers";
import { usePriceQuotes, useProductsById } from "./use-shop-data";

const DESKTOP_GRID = "grid-cols-[1fr_90px_120px_110px_40px]";

export function ShopCartPage() {
  const t = useT();
  const capabilities = useFulfillmentCapabilities();
  const canPlace = capabilities.isSuccess && capabilities.data.readiness === "ready" && capabilities.data.acceptsOrders === true;
  const navigate = useNavigate();
  const { store } = useShopStore();
  const queryClient = useQueryClient();

  const cartQuery = useQuery({
    queryKey: ["ordering", "cart", store?.id],
    queryFn: () => getCart(store!.id),
    enabled: !!store,
  });

  const lines = cartQuery.data?.lines ?? [];
  const products = useProductsById(lines.map((l) => l.productId));
  const quotes = usePriceQuotes(
    store?.customerOrgId,
    lines.map((l) => ({ productId: l.productId, quantity: l.quantity })),
  );

  const saveMutation = useMutation({
    mutationFn: (input: { storeId: string; lines: CartLineInput[] }) =>
      updateCart(input.storeId, input.lines),
    onSuccess: (_id, input) => {
      queryClient.invalidateQueries({ queryKey: ["ordering", "cart", input.storeId] });
    },
    onError: (err) =>
      toast.error(t("shop.updateFailed", "Could not update cart"), { description: describe(err) }),
  });

  const placeMutation = useMutation({
    mutationFn: (input: { storeId: string; idempotencyKey: string }) =>
      placeOrder(input.storeId, input.idempotencyKey),
    onSuccess: (orderId, input) => {
      toast.success(t("shop.placed", "Order placed"));
      queryClient.invalidateQueries({ queryKey: ["ordering", "cart", input.storeId] });
      queryClient.invalidateQueries({ queryKey: ["ordering", "orders"] });
      navigate(`/shop/orders/${orderId}`);
    },
    onError: (err) =>
      toast.error(t("shop.placeFailed", "Could not place order"), { description: describe(err) }),
  });

  const replaceLines = (next: CartLineInput[]) => {
    if (!store) return;
    saveMutation.mutate({ storeId: store.id, lines: next.filter((l) => l.quantity > 0) });
  };

  const setQty = (productId: string, quantity: number) => {
    const next = lines.map((l) => ({ productId: l.productId, quantity: l.quantity }));
    const idx = next.findIndex((l) => l.productId === productId);
    if (idx < 0) return;
    next[idx] = { productId, quantity };
    replaceLines(next);
  };

  const removeLine = (productId: string) => {
    replaceLines(lines.filter((l) => l.productId !== productId).map((l) => ({ productId: l.productId, quantity: l.quantity })));
  };

  const subtotal = lines.reduce((sum, line) => {
    const quote = quotes.byProductId.get(line.productId);
    return quote ? sum + quote.unitPrice * line.quantity : sum;
  }, 0);
  const currency = quotes.byProductId.values().next().value?.currency ?? "USD";

  return (
    <div className="space-y-4 sm:space-y-6">
      {!canPlace && <WmsStatusNotice />}
      <EntityPageHeader
        icon={ShoppingCart}
        title={t("shop.cartTitle", "Cart")}
        total={lines.length || null}
        unit={t("shop.lineUnit", "line")}
        description={t("shop.cartDescription", "Quoted prices follow the selected store's contract.")}
      />

      {cartQuery.isError ? <ErrorBand message={describe(cartQuery.error)} /> : null}

      {!store ? (
        <p className="text-[13px] text-[var(--color-muted-foreground)]">
          {t("shop.needStore", "Select a store to see contract prices and place orders.")}
        </p>
      ) : cartQuery.isLoading ? (
        <Skeleton className="h-40 w-full rounded-xl" />
      ) : lines.length === 0 ? (
        <EntityEmpty
          icon={ShoppingCart}
          title={t("shop.emptyCart", "Cart is empty")}
          body={t("shop.emptyCartBody", "Add products from the catalog to place an order.")}
          action={
            <Button asChild>
              <Link to="/shop/catalog">{t("shop.browseCatalog", "Browse catalog")}</Link>
            </Button>
          }
        />
      ) : (
        <div className="space-y-4">
          <EntityListCard>
            <EntityListHeader className={DESKTOP_GRID}>
              <span>{t("shop.colProduct", "Product")}</span>
              <span>{t("shop.quantity", "Qty")}</span>
              <span>{t("shop.colPrice", "Your price")}</span>
              <span>{t("shop.lineTotal", "Line total")}</span>
              <span />
            </EntityListHeader>
            {lines.map((line, i) => {
              const product = products.byId.get(line.productId);
              const quote = quotes.byProductId.get(line.productId);
              return (
                <EntityListRow key={line.id || line.productId} className={DESKTOP_GRID} isLast={i === lines.length - 1}>
                  <div className="min-w-0">
                    <Link
                      to={`/shop/products/${line.productId}`}
                      className="truncate text-[14px] font-medium hover:text-[var(--color-primary)]"
                    >
                      {product?.name ?? line.productId}
                    </Link>
                    <div className="font-mono text-[11px] text-[var(--color-muted-foreground)]">
                      {product?.sku ?? ""}
                    </div>
                  </div>
                  <div className="flex items-center gap-1">
                    <Button
                      type="button"
                      variant="outline"
                      size="icon-xs"
                      aria-label={t("shop.decreaseQty", "Decrease quantity")}
                      onClick={() => setQty(line.productId, Math.max(0, line.quantity - 1))}
                      disabled={saveMutation.isPending}
                    >
                      <Minus className="size-3" />
                    </Button>
                    <Input
                      aria-label={t("shop.quantity", "Qty")}
                      type="number"
                      min={1}
                      value={line.quantity}
                      onChange={(e) =>
                        setQty(line.productId, Math.max(1, Number.parseInt(e.target.value, 10) || 1))
                      }
                      className="h-8 w-14 text-center tabular-nums"
                    />
                    <Button
                      type="button"
                      variant="outline"
                      size="icon-xs"
                      aria-label={t("shop.increaseQty", "Increase quantity")}
                      onClick={() => setQty(line.productId, line.quantity + 1)}
                      disabled={saveMutation.isPending}
                    >
                      <Plus className="size-3" />
                    </Button>
                  </div>
                  <div>
                    {quote ? (
                      <>
                        <div data-testid="quoted-price" className="font-display text-[14px] font-semibold tabular-nums">
                          {formatMoney(quote.unitPrice, quote.currency)}
                        </div>
                        <div className="text-[10px] font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)]">
                          {quoteSourceLabel(quote.source)}
                        </div>
                      </>
                    ) : (
                      <Skeleton className="h-4 w-16" />
                    )}
                  </div>
                  <div className="font-display text-[14px] font-semibold tabular-nums">
                    {quote ? formatMoney(quote.unitPrice * line.quantity, quote.currency) : "—"}
                  </div>
                  <button
                    type="button"
                    aria-label={t("shop.removeLine", "Remove line")}
                    onClick={() => removeLine(line.productId)}
                    className="grid size-8 place-items-center rounded-md text-[var(--color-muted-foreground)] hover:bg-[var(--color-muted)] hover:text-[var(--color-destructive)]"
                  >
                    <Trash2 className="size-3.5" />
                  </button>
                </EntityListRow>
              );
            })}
          </EntityListCard>

          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <div className="text-[11px] font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)]">
                {t("shop.subtotal", "Subtotal")}
              </div>
              <div className="font-display text-[22px] font-semibold tabular-nums">
                {formatMoney(subtotal, currency)}
              </div>
            </div>
            <Button
              disabled={!canPlace || !store || lines.length === 0 || placeMutation.isPending}
              onClick={() =>
                store &&
                canPlace && placeMutation.mutate({ storeId: store.id, idempotencyKey: crypto.randomUUID() })
              }
            >
              {placeMutation.isPending
                ? t("shop.placing", "Placing…")
                : t("shop.placeOrder", "Place order")}
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
