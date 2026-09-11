import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Package, Plus, ShoppingCart, Snowflake, Thermometer } from "lucide-react";
import { toast } from "sonner";
import { searchCategories, searchProducts, type ProductDto } from "@/api/catalog";
import { getCart, updateCart, type CartLineInput } from "@/api/ordering";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Combobox,
  EntityEmpty,
  EntityListCard,
  EntityListHeader,
  EntityListRow,
  EntityMobileCard,
  EntityPageHeader,
  EntityPager,
  EntitySearch,
  EntityStatusBadge,
  ErrorBand,
} from "@/components/list";
import { cn } from "@/lib/cn";
import { describe, formatMoney } from "@/lib/list-helpers";
import { useT } from "@/i18n/locale-provider";
import { useShopStore } from "./store-context";
import { quoteSourceLabel, zoneLabel } from "./shop-helpers";
import { useAvailableQtys, usePriceQuotes } from "./use-shop-data";

const PAGE_SIZE = 20;
const DESKTOP_GRID =
  "grid-cols-[1fr_110px_120px_100px_120px] lg:grid-cols-[1fr_130px_140px_110px_140px]";

export function ShopCatalogPage() {
  const t = useT();
  const { store } = useShopStore();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [page, setPage] = useState(1);
  const [categoryId, setCategoryId] = useState<string | null>(null);

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(search.trim());
      setPage(1);
    }, 250);
    return () => clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    setPage(1);
  }, [categoryId]);

  const productsQuery = useQuery({
    queryKey: [
      "catalog",
      "products",
      "shop",
      { search: debouncedSearch, categoryId, pageNumber: page, pageSize: PAGE_SIZE },
    ],
    queryFn: () =>
      searchProducts({
        search: debouncedSearch || undefined,
        categoryId,
        isActive: true,
        pageNumber: page,
        pageSize: PAGE_SIZE,
        sortBy: "name",
        sortDir: "asc",
      }),
    placeholderData: keepPreviousData,
  });

  const categoriesQuery = useQuery({
    queryKey: ["catalog", "categories", "shop-filter"],
    queryFn: () => searchCategories({ pageSize: 200 }),
    staleTime: 60_000,
  });

  const items = useMemo(() => productsQuery.data?.items ?? [], [productsQuery.data]);
  const quoteRequests = useMemo(
    () => items.map((p) => ({ productId: p.id, quantity: 1 })),
    [items],
  );
  const quotes = usePriceQuotes(store?.customerOrgId, quoteRequests);
  const availability = useAvailableQtys(
    store?.defaultWarehouseId,
    items.map((p) => ({ productId: p.id, zone: p.temperatureZone })),
  );

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

  const onAdd = (product: ProductDto) => {
    if (!store) return;
    const current = cartQuery.data?.lines ?? [];
    const existing = current.find((l) => l.productId === product.id);
    const lines: CartLineInput[] = existing
      ? current.map((l) =>
          l.productId === product.id
            ? { productId: l.productId, quantity: l.quantity + 1 }
            : { productId: l.productId, quantity: l.quantity },
        )
      : [
          ...current.map((l) => ({ productId: l.productId, quantity: l.quantity })),
          { productId: product.id, quantity: 1 },
        ];
    addMutation.mutate({ storeId: store.id, lines });
  };

  const searchActive = debouncedSearch.length > 0 || categoryId !== null;

  return (
    <div className="space-y-4 sm:space-y-6">
      <EntityPageHeader
        icon={ShoppingCart}
        title={t("shop.catalogTitle", "Order catalog")}
        total={productsQuery.data?.totalCount ?? null}
        unit={t("shop.productUnit", "product")}
        description={t(
          "shop.catalogDescription",
          "Prices are quoted for the selected store. Catalog list prices are never shown here.",
        )}
      >
        <Button asChild variant="outline" className="h-9 flex-1 rounded-lg px-4 text-[13px] sm:flex-none">
          <Link to="/shop/cart">
            <ShoppingCart className="size-4" />
            {t("shop.cartTitle", "Cart")}
            {cartQuery.data && cartQuery.data.lines.length > 0 ? (
              <span className="font-mono text-[11px]">({cartQuery.data.lines.length})</span>
            ) : null}
          </Link>
        </Button>
      </EntityPageHeader>

      <EntitySearch
        value={search}
        onChange={setSearch}
        placeholder={t("shop.searchPlaceholder", "Search products…")}
      />

      <Combobox
        label={t("shop.category", "Category")}
        variant="filter"
        searchable
        clearable
        value={categoryId}
        onChange={setCategoryId}
        options={(categoriesQuery.data?.items ?? []).map((c) => ({ value: c.id, label: c.name }))}
      />

      {productsQuery.isError ? <ErrorBand message={describe(productsQuery.error)} /> : null}

      {productsQuery.isLoading && items.length === 0 ? (
        <EntityListLoadingGrid />
      ) : items.length === 0 ? (
        <EntityEmpty
          icon={Package}
          title={
            searchActive
              ? t("shop.noProductsFound", "No products found")
              : t("shop.noProducts", "No products yet")
          }
          body={
            searchActive
              ? t("shop.noProductsMatch", "Nothing matches the current search.")
              : t("shop.noProductsBody", "The catalog is empty for this tenant.")
          }
          action={
            searchActive ? (
              <Button
                variant="outline"
                onClick={() => {
                  setSearch("");
                  setCategoryId(null);
                }}
              >
                {t("shop.clearFilters", "Clear filters")}
              </Button>
            ) : undefined
          }
        />
      ) : (
        <div>
          <p className="mb-3 text-[12px] font-medium text-[var(--color-muted-foreground)]">
            {productsQuery.data?.totalCount ?? 0} {t("shop.productsFound", "products")}
          </p>

          <div className="space-y-2 md:hidden">
            {items.map((product) => {
              const quote = quotes.byProductId.get(product.id);
              const available = availability.byProductId.get(product.id)?.available;
              const out = available !== undefined && available <= 0;
              return (
                <EntityMobileCard
                  key={product.id}
                  href={`/shop/products/${product.id}`}
                  dim={out}
                  aria-label={product.name}
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="truncate text-[14px] font-medium">{product.name}</p>
                      <code className="font-mono text-[11px] text-[var(--color-muted-foreground)]">
                        {product.sku}
                      </code>
                    </div>
                    <QuotedPrice quoteLoading={!quote && quotes.isLoading} quote={quote} />
                  </div>
                  <div className="mt-2 flex flex-wrap items-center gap-2">
                    <ZoneChip zone={product.temperatureZone} />
                    <AvailabilityChip available={available} loading={availability.isLoading} out={out} />
                    <Button
                      size="sm"
                      className="ml-auto h-8"
                      disabled={!store || out || addMutation.isPending}
                      onClick={(e) => {
                        e.preventDefault();
                        e.stopPropagation();
                        onAdd(product);
                      }}
                    >
                      <Plus className="size-3.5" />
                      {t("shop.addToCart", "Add")}
                    </Button>
                  </div>
                </EntityMobileCard>
              );
            })}
          </div>

          <EntityListCard className="hidden md:block">
            <EntityListHeader className={DESKTOP_GRID}>
              <span>{t("shop.colProduct", "Product")}</span>
              <span>{t("shop.colSku", "SKU")}</span>
              <span>{t("shop.colPrice", "Your price")}</span>
              <span>{t("shop.colAvail", "Available")}</span>
              <span />
            </EntityListHeader>
            {items.map((product, i) => {
              const quote = quotes.byProductId.get(product.id);
              const available = availability.byProductId.get(product.id)?.available;
              const out = available !== undefined && available <= 0;
              return (
                <EntityListRow
                  key={product.id}
                  className={DESKTOP_GRID}
                  isLast={i === items.length - 1}
                  dim={out}
                >
                  <Link to={`/shop/products/${product.id}`} className="min-w-0 outline-none">
                    <p className="truncate text-[14px] font-medium text-[var(--color-foreground)] group-hover:text-[var(--color-primary)]">
                      {product.name}
                    </p>
                    <div className="mt-0.5">
                      <ZoneChip zone={product.temperatureZone} />
                    </div>
                  </Link>
                  <code className="truncate font-mono text-[12px] text-[var(--color-muted-foreground)]">
                    {product.sku}
                  </code>
                  <QuotedPrice quoteLoading={!quote && quotes.isLoading} quote={quote} />
                  <AvailabilityChip available={available} loading={availability.isLoading} out={out} />
                  <div className="flex justify-end">
                    <Button
                      size="sm"
                      className="h-8"
                      disabled={!store || out || addMutation.isPending}
                      onClick={() => onAdd(product)}
                    >
                      <Plus className="size-3.5" />
                      {t("shop.addToCart", "Add")}
                    </Button>
                  </div>
                </EntityListRow>
              );
            })}
          </EntityListCard>

          <EntityPager
            page={page}
            totalPages={productsQuery.data?.totalPages ?? 1}
            hasPrev={productsQuery.data?.hasPrevious ?? false}
            hasNext={productsQuery.data?.hasNext ?? false}
            onPrev={() => setPage((p) => Math.max(1, p - 1))}
            onNext={() => setPage((p) => p + 1)}
          />
        </div>
      )}
    </div>
  );
}

function QuotedPrice({
  quote,
  quoteLoading,
}: {
  quote: { unitPrice: number; currency: string; source: string } | undefined;
  quoteLoading: boolean;
}) {
  const t = useT();
  if (quoteLoading) {
    return <Skeleton className="h-5 w-16" />;
  }
  if (!quote) {
    return (
      <span className="text-[12px] text-[var(--color-muted-foreground)]">
        {t("shop.priceUnavailable", "Price unavailable")}
      </span>
    );
  }
  return (
    <div>
      <div
        data-testid="quoted-price"
        className="font-display text-[14px] font-semibold tabular-nums text-[var(--color-foreground)]"
      >
        {formatMoney(quote.unitPrice, quote.currency)}
      </div>
      <span className="text-[10px] font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)]">
        {quoteSourceLabel(quote.source)}
      </span>
    </div>
  );
}

function ZoneChip({ zone }: { zone?: string }) {
  const Icon = (zone ?? "").toLowerCase() === "frozen" ? Snowflake : Thermometer;
  return (
    <span className="inline-flex items-center gap-1 rounded-full bg-[var(--color-secondary)] px-2 py-0.5 text-[11px] font-medium text-[var(--color-secondary-foreground)]">
      <Icon className="size-3" aria-hidden />
      {zoneLabel(zone)}
    </span>
  );
}

function AvailabilityChip({
  available,
  loading,
  out,
}: {
  available: number | undefined;
  loading: boolean;
  out: boolean;
}) {
  const t = useT();
  if (loading && available === undefined) return <Skeleton className="h-5 w-12" />;
  if (available === undefined) {
    return <span className="text-[12px] text-[var(--color-muted-foreground)]">—</span>;
  }
  if (out) {
    return <EntityStatusBadge tone="danger">{t("shop.outOfStock", "Out of stock")}</EntityStatusBadge>;
  }
  return (
    <span className="inline-flex items-center gap-1 font-mono text-[12px] tabular-nums text-[var(--color-muted-foreground)]">
      <Package className="size-3" />
      {available}
    </span>
  );
}

function EntityListLoadingGrid() {
  return (
    <div>
      <div className="space-y-2 md:hidden">
        {Array.from({ length: 4 }).map((_, i) => (
          <div
            key={i}
            className="rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-4"
          >
            <Skeleton className="h-4 w-40" />
            <Skeleton className="mt-2 h-3 w-24" />
          </div>
        ))}
      </div>
      <div className={cn("hidden md:block")}>
        <EntityListCard>
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className={cn("grid items-center gap-3 px-5 py-3", DESKTOP_GRID)}>
              <Skeleton className="h-4 w-48" />
              <Skeleton className="h-3 w-20" />
              <Skeleton className="h-4 w-16" />
              <Skeleton className="h-4 w-12" />
              <Skeleton className="ml-auto h-8 w-16" />
            </div>
          ))}
        </EntityListCard>
      </div>
    </div>
  );
}
