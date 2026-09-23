import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Package, Plus, ShoppingCart } from "lucide-react";
import { toast } from "sonner";
import {
  getShopCart,
  searchShopProducts,
  updateShopCart,
  SHOP_PERMISSIONS,
  type ShopCartLineInput,
  type ShopProductDto,
} from "@/api/shop";
import { useAuth } from "@/auth/use-auth";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import {
  EntityEmpty,
  EntityListCard,
  EntityListHeader,
  EntityListRow,
  EntityMobileCard,
  EntityPageHeader,
  EntityPager,
  EntitySearch,
  ErrorBand,
} from "@/components/list";
import { cn } from "@/lib/cn";
import { describe, formatMoney } from "@/lib/list-helpers";
import { useT } from "@/i18n/locale-provider";
import { useShopStore } from "./store-context";
import { quoteSourceLabel } from "./shop-helpers";

const PAGE_SIZE = 20;
const DESKTOP_GRID =
  "grid-cols-[1fr_110px_130px_130px_110px] lg:grid-cols-[1fr_140px_160px_150px_130px]";

export function ShopCatalogPage() {
  const t = useT();
  const { user } = useAuth();
  const canOrder = user?.permissions.includes(SHOP_PERMISSIONS.order) ?? false;
  const { store } = useShopStore();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [page, setPage] = useState(1);

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(search.trim());
      setPage(1);
    }, 250);
    return () => clearTimeout(timer);
  }, [search]);

  const productsQuery = useQuery({
    queryKey: [
      "shop",
      "products",
      "shop",
      store?.id,
      { search: debouncedSearch, pageNumber: page, pageSize: PAGE_SIZE },
    ],
    queryFn: () =>
      searchShopProducts({
        storeId: store!.id,
        search: debouncedSearch || undefined,
        pageNumber: page,
        pageSize: PAGE_SIZE,
      }),
    enabled: !!store,
    placeholderData: keepPreviousData,
  });

  const items = useMemo(() => productsQuery.data?.items ?? [], [productsQuery.data]);

  const cartQuery = useQuery({
    queryKey: ["shop", "cart", store?.id],
    queryFn: () => getShopCart(store!.id),
    enabled: canOrder && !!store,
  });

  const queryClient = useQueryClient();
  const addMutation = useMutation({
    mutationFn: (input: { storeId: string; lines: ShopCartLineInput[] }) =>
      updateShopCart(input.storeId, input.lines),
    onSuccess: (_id, input) => {
      toast.success(t("shop.addedToCart", "Added to cart"));
      queryClient.invalidateQueries({ queryKey: ["shop", "cart", input.storeId] });
    },
    onError: (err) =>
      toast.error(t("shop.addFailed", "Could not update cart"), { description: describe(err) }),
  });

  const onAdd = (product: ShopProductDto) => {
    if (!store) return;
    const current = cartQuery.data?.lines ?? [];
    const existing = current.find((l) => l.productId === product.id);
    const lines: ShopCartLineInput[] = existing
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

  const searchActive = debouncedSearch.length > 0;

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
        {canOrder ? (
          <Button asChild variant="outline" className="h-9 flex-1 rounded-lg px-4 text-[13px] sm:flex-none">
            <Link to="/shop/cart">
              <ShoppingCart className="size-4" />
              {t("shop.cartTitle", "Cart")}
              {cartQuery.data && cartQuery.data.lines.length > 0 ? (
                <span className="font-mono text-[11px]">({cartQuery.data.lines.length})</span>
              ) : null}
            </Link>
          </Button>
        ) : null}
      </EntityPageHeader>

      <EntitySearch
        value={search}
        onChange={setSearch}
        placeholder={t("shop.searchPlaceholder", "Search products…")}
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
              const out = !product.isAvailable;
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
                    <QuotedPrice product={product} />
                  </div>
                  <div className="mt-2 flex flex-wrap items-center gap-2">
                    <AvailabilityChip available={product.isAvailable} />
                    <Button
                      size="sm"
                      className="ml-auto h-8"
                      disabled={!canOrder || !store || out || addMutation.isPending}
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
              const out = !product.isAvailable;
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
                    <div className="mt-0.5 text-[11px] text-[var(--color-muted-foreground)]">
                      {product.baseUom}
                    </div>
                  </Link>
                  <code className="truncate font-mono text-[12px] text-[var(--color-muted-foreground)]">
                    {product.sku}
                  </code>
                  <QuotedPrice product={product} />
                  <AvailabilityChip available={product.isAvailable} />
                  <div className="flex justify-end">
                    <Button
                      size="sm"
                      className="h-8"
                      disabled={!canOrder || !store || out || addMutation.isPending}
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

function QuotedPrice({ product }: { product: ShopProductDto }) {
  return (
    <div>
      <div
        data-testid="quoted-price"
        className="font-display text-[14px] font-semibold tabular-nums text-[var(--color-foreground)]"
      >
        {formatMoney(product.unitPrice, product.currency)}
      </div>
      <span className="text-[10px] font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)]">
        {quoteSourceLabel(product.priceSource)}
      </span>
    </div>
  );
}

function AvailabilityChip({ available }: { available: boolean }) {
  const t = useT();
  if (!available) {
    return (
      <span className="text-[12px] text-[var(--color-destructive)]">
        {t("shop.outOfStock", "Out of stock")}
      </span>
    );
  }
  return (
    <span className="text-[12px] text-[var(--color-muted-foreground)]">
      {t("shop.availableToOrder", "Available to order")}
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
              <Skeleton className="h-4 w-20" />
              <Skeleton className="ml-auto h-8 w-16" />
            </div>
          ))}
        </EntityListCard>
      </div>
    </div>
  );
}
