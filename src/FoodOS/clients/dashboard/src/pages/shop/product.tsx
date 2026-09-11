import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Minus, Package, Plus, ShoppingCart, Snowflake, Thermometer } from "lucide-react";
import { toast } from "sonner";
import { getProductById } from "@/api/catalog";
import { getCart, updateCart, type CartLineInput } from "@/api/ordering";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import {
  EntityDetailBack,
  EntityDetailHero,
  EntityDetailMeta,
  EntityDetailSection,
  EntityDetailStat,
  EntityStatusBadge,
  ErrorBand,
} from "@/components/list";
import { describe, formatMoney } from "@/lib/list-helpers";
import { useT } from "@/i18n/locale-provider";
import { useShopStore } from "./store-context";
import { quoteSourceLabel, zoneLabel } from "./shop-helpers";
import { useAvailableQty, usePriceQuotes } from "./use-shop-data";

export function ShopProductPage() {
  const t = useT();
  const { productId = "" } = useParams<{ productId: string }>();
  const { store } = useShopStore();
  const [qty, setQty] = useState(1);

  const productQuery = useQuery({
    queryKey: ["catalog", "products", productId],
    queryFn: () => getProductById(productId),
    enabled: !!productId,
  });

  const quotes = usePriceQuotes(store?.customerOrgId, [
    { productId, quantity: qty > 0 ? qty : 1 },
  ]);
  const quote = quotes.byProductId.get(productId);
  const product = productQuery.data;
  const availability = useAvailableQty(
    store?.defaultWarehouseId,
    productId,
    product?.temperatureZone,
  );
  const available = availability.data?.available;
  const out = available !== undefined && available <= 0;

  const cartQuery = useQuery({
    queryKey: ["ordering", "cart", store?.id],
    queryFn: () => getCart(store!.id),
    enabled: !!store,
  });

  const queryClient = useQueryClient();
  const addMutation = useMutation({
    mutationFn: (input: { storeId: string; lines: CartLineInput[] }) =>
      updateCart(input.storeId, input.lines),
    onSuccess: (_id, input) => {
      toast.success(t("shop.addedToCart", "Added to cart"));
      queryClient.invalidateQueries({ queryKey: ["ordering", "cart", input.storeId] });
    },
    onError: (err) =>
      toast.error(t("shop.addFailed", "Could not update cart"), { description: describe(err) }),
  });

  const onAdd = () => {
    if (!store || !productId || qty <= 0) return;
    const current = cartQuery.data?.lines ?? [];
    const existing = current.find((l) => l.productId === productId);
    const lines: CartLineInput[] = existing
      ? current.map((l) =>
          l.productId === productId
            ? { productId: l.productId, quantity: l.quantity + qty }
            : { productId: l.productId, quantity: l.quantity },
        )
      : [
          ...current.map((l) => ({ productId: l.productId, quantity: l.quantity })),
          { productId, quantity: qty },
        ];
    addMutation.mutate({ storeId: store.id, lines });
  };

  const clampedMax = available !== undefined ? Math.max(0, available) : undefined;
  const canAdd = !!store && !out && qty > 0 && (clampedMax === undefined || qty <= clampedMax);

  return (
    <div className="space-y-5">
      <EntityDetailBack to="/shop/catalog" label={t("shop.backToCatalog", "Back to catalog")} />

      {productQuery.isError ? <ErrorBand message={describe(productQuery.error)} /> : null}

      {productQuery.isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-10 w-64" />
          <Skeleton className="h-24 w-full" />
        </div>
      ) : product ? (
        <>
          <EntityDetailHero
            title={product.name}
            subtitle={product.sku}
            badges={
              quote ? (
                <span data-testid="quoted-price" className="font-display text-[15px] font-semibold tabular-nums">
                  {formatMoney(quote.unitPrice, quote.currency)}
                </span>
              ) : quotes.isLoading ? (
                <Skeleton className="h-5 w-16" />
              ) : (
                <span className="text-[12px] text-[var(--color-muted-foreground)]">
                  {t("shop.priceUnavailable", "Price unavailable")}
                </span>
              )
            }
            stats={
              <>
                <EntityDetailStat
                  icon={Package}
                  label={t("shop.colPrice", "Your price")}
                  value={
                    quote
                      ? `${formatMoney(quote.unitPrice, quote.currency)} · ${quoteSourceLabel(quote.source)}`
                      : quotes.isLoading
                        ? "…"
                        : "—"
                  }
                  tone="primary"
                />
                <EntityDetailStat
                  icon={Thermometer}
                  label={t("shop.zone", "Zone")}
                  value={zoneLabel(product.temperatureZone)}
                />
                <EntityDetailStat
                  icon={Package}
                  label={t("shop.colAvail", "Available")}
                  value={
                    availability.isLoading && available === undefined
                      ? "…"
                      : available === undefined
                        ? "—"
                        : String(available)
                  }
                  tone={out ? "danger" : "default"}
                />
              </>
            }
          />

          {out ? (
            <EntityStatusBadge tone="danger">{t("shop.outOfStock", "Out of stock")}</EntityStatusBadge>
          ) : null}

          <EntityDetailSection title={t("shop.order", "Order")} icon={ShoppingCart}>
            <div className="flex flex-wrap items-end gap-3">
              <div>
                <label htmlFor="shop-qty" className="mb-1 block text-[11px] font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)]">
                  {t("shop.quantity", "Qty")}
                </label>
                <div className="flex items-center gap-2">
                  <Button
                    type="button"
                    variant="outline"
                    size="icon-xs"
                    aria-label={t("shop.decreaseQty", "Decrease quantity")}
                    onClick={() => setQty((q) => Math.max(1, q - 1))}
                  >
                    <Minus className="size-3.5" />
                  </Button>
                  <Input
                    id="shop-qty"
                    type="number"
                    min={1}
                    max={clampedMax}
                    value={qty}
                    onChange={(e) => setQty(Math.max(1, Number.parseInt(e.target.value, 10) || 1))}
                    className="w-20 text-center tabular-nums"
                  />
                  <Button
                    type="button"
                    variant="outline"
                    size="icon-xs"
                    aria-label={t("shop.increaseQty", "Increase quantity")}
                    onClick={() =>
                      setQty((q) => (clampedMax !== undefined ? Math.min(clampedMax, q + 1) : q + 1))
                    }
                    disabled={clampedMax !== undefined && qty >= clampedMax}
                  >
                    <Plus className="size-3.5" />
                  </Button>
                </div>
              </div>
              <Button onClick={onAdd} disabled={!canAdd || addMutation.isPending}>
                <Plus className="size-4" />
                {addMutation.isPending
                  ? t("shop.adding", "Adding…")
                  : t("shop.addToCart", "Add to cart")}
              </Button>
              <Button asChild variant="outline">
                <Link to="/shop/cart">{t("shop.viewCart", "View cart")}</Link>
              </Button>
            </div>
          </EntityDetailSection>

          {product.description ? (
            <EntityDetailSection title={t("shop.details", "Details")} icon={Package}>
              <p className="whitespace-pre-wrap text-[13px] leading-relaxed text-[var(--color-foreground)]/90">
                {product.description}
              </p>
            </EntityDetailSection>
          ) : null}

          <div className="flex flex-wrap gap-4 text-[12px] text-[var(--color-muted-foreground)]">
            <EntityDetailMeta icon={(product.temperatureZone ?? "").toLowerCase() === "frozen" ? Snowflake : Thermometer}>
              {zoneLabel(product.temperatureZone)}
            </EntityDetailMeta>
            <EntityDetailMeta icon={Package}>{product.baseUom ?? "ea"}</EntityDetailMeta>
          </div>
        </>
      ) : null}
    </div>
  );
}
