import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowRight,
  ClipboardList,
  PackageSearch,
  RefreshCw,
  ShoppingCart,
  Store,
} from "lucide-react";
import { getShopCart, searchShopOrders, SHOP_PERMISSIONS } from "@/api/shop";
import { useAuth } from "@/auth/use-auth";
import { Combobox, ErrorBand } from "@/components/list";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { useT } from "@/i18n/locale-provider";
import { describe, formatDate } from "@/lib/list-helpers";
import { ShopStoreProvider, useShopStore } from "@/pages/shop/store-context";

const RECENT_ORDER_COUNT = 5;

export function CustomerOverviewPage() {
  const t = useT();
  const { user, permissionsHydrated } = useAuth();
  const canViewShop = user?.permissions.includes(SHOP_PERMISSIONS.view) ?? false;

  if (!permissionsHydrated) {
    return <OverviewSkeleton />;
  }

  if (!canViewShop) {
    return (
      <section className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-card)] p-6 sm:p-8">
        <PackageSearch className="size-8 text-[var(--color-muted-foreground)]" aria-hidden />
        <h1 className="mt-4 font-display text-2xl font-bold tracking-tight">
          {t("customerHome.noAccessTitle")}
        </h1>
        <p className="mt-2 max-w-xl text-sm leading-6 text-[var(--color-muted-foreground)]">
          {t("customerHome.noAccessBody")}
        </p>
      </section>
    );
  }

  return (
    <ShopStoreProvider>
      <CustomerOverviewBody canOrder={user?.permissions.includes(SHOP_PERMISSIONS.order) ?? false} />
    </ShopStoreProvider>
  );
}

function CustomerOverviewBody({ canOrder }: { canOrder: boolean }) {
  const t = useT();
  const { user } = useAuth();
  const {
    stores,
    storesLoading,
    storesError,
    retryStores,
    store,
    storeId,
    setStoreId,
  } = useShopStore();

  const cartQuery = useQuery({
    queryKey: ["shop", "cart", store?.id],
    queryFn: () => getShopCart(store!.id),
    enabled: canOrder && !!store,
  });
  const ordersQuery = useQuery({
    queryKey: ["shop", "orders", store?.id, "overview"],
    queryFn: () =>
      searchShopOrders({ storeId: store!.id, pageNumber: 1, pageSize: RECENT_ORDER_COUNT }),
    enabled: !!store,
  });

  const displayName = user?.name ?? user?.email ?? t("customerHome.customer");
  const cartLines = cartQuery.data?.lines.length ?? 0;
  const recentOrders = ordersQuery.data?.items ?? [];

  return (
    <div className="space-y-5 sm:space-y-7">
      <section className="overflow-hidden rounded-2xl border border-[var(--color-border)] bg-[var(--color-card)] p-5 sm:p-7">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--color-primary)]">
          {t("customerHome.portal")}
        </p>
        <div className="mt-2 flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
          <div className="max-w-2xl">
            <h1 className="font-display text-2xl font-bold tracking-tight sm:text-3xl">
              {t("customerHome.welcome").replace("{name}", displayName)}
            </h1>
            <p className="mt-2 text-sm leading-6 text-[var(--color-muted-foreground)]">
              {t("customerHome.description")}
            </p>
          </div>
          <div className="w-full lg:w-72">
            <Combobox
              label={t("shop.selectStore", "Store")}
              variant="filter"
              searchable
              value={storeId}
              onChange={setStoreId}
              disabled={storesLoading || stores.length === 0}
              placeholder={
                storesLoading
                  ? t("shop.loadingStores", "Loading stores…")
                  : t("shop.chooseStore", "Choose a store")
              }
              options={stores.map((item) => ({
                value: item.id,
                label: item.name,
                hint: item.code,
              }))}
            />
          </div>
        </div>
      </section>

      {storesError ? (
        <div className="space-y-3">
          <div role="alert">
            <ErrorBand message={describe(storesError)} />
          </div>
          <Button variant="outline" onClick={retryStores}>
            <RefreshCw className="mr-2 size-4" aria-hidden />
            {t("customerHome.retryStores")}
          </Button>
        </div>
      ) : null}

      {!storesLoading && !storesError && stores.length === 0 ? (
        <section className="rounded-2xl border border-dashed border-[var(--color-border)] p-6 text-center">
          <Store className="mx-auto size-8 text-[var(--color-muted-foreground)]" aria-hidden />
          <h2 className="mt-3 font-display text-lg font-semibold">
            {t("customerHome.noStoresTitle")}
          </h2>
          <p className="mx-auto mt-1 max-w-lg text-sm text-[var(--color-muted-foreground)]">
            {t("shop.noStoresBody", "Ask an operator to create a customer and store before ordering.")}
          </p>
        </section>
      ) : null}

      {store ? (
        <>
          <section className="grid gap-3 sm:grid-cols-3">
            <SummaryCard
              icon={Store}
              label={t("customerHome.selectedStore")}
              value={store.name}
              detail={store.deliveryWindow ?? store.code}
            />
            <SummaryCard
              icon={ShoppingCart}
              label={t("customerHome.cartLines")}
              value={canOrder && !cartQuery.isLoading ? String(cartLines) : "—"}
              detail={canOrder ? t("customerHome.readyToReview") : t("customerHome.readOnly")}
              href={canOrder ? "/shop/cart" : undefined}
            />
            <SummaryCard
              icon={ClipboardList}
              label={t("customerHome.orders")}
              value={ordersQuery.isLoading ? "—" : String(ordersQuery.data?.totalCount ?? 0)}
              detail={t("customerHome.currentStore")}
              href="/shop/orders"
            />
          </section>

          <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <ActionLink to="/shop/catalog" icon={PackageSearch} label={t("customerHome.browseProducts")} />
            {canOrder ? (
              <ActionLink to="/shop/cart" icon={ShoppingCart} label={t("customerHome.reviewCart")} />
            ) : null}
            <ActionLink to="/shop/orders" icon={ClipboardList} label={t("customerHome.trackOrders")} />
            {canOrder ? (
              <ActionLink to="/shop/after-sales" icon={RefreshCw} label={t("customerHome.afterSales")} />
            ) : null}
          </section>

          <section className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-card)] p-5 sm:p-6">
            <div className="flex items-center justify-between gap-4">
              <div>
                <h2 className="font-display text-lg font-semibold">{t("customerHome.recentOrders")}</h2>
                <p className="mt-1 text-sm text-[var(--color-muted-foreground)]">
                  {t("customerHome.recentOrdersBody")}
                </p>
              </div>
              <Button asChild variant="ghost" size="sm">
                <Link to="/shop/orders">
                  {t("customerHome.viewAll")}
                  <ArrowRight className="ml-1 size-4" aria-hidden />
                </Link>
              </Button>
            </div>

            {ordersQuery.isLoading ? (
              <div className="mt-5 space-y-2">
                <Skeleton className="h-12 w-full rounded-lg" />
                <Skeleton className="h-12 w-full rounded-lg" />
              </div>
            ) : ordersQuery.isError ? (
              <div className="mt-5 space-y-3">
                <div role="alert">
                  <ErrorBand message={describe(ordersQuery.error)} />
                </div>
                <Button variant="outline" onClick={() => void ordersQuery.refetch()}>
                  <RefreshCw className="mr-2 size-4" aria-hidden />
                  {t("customerHome.retryOrders")}
                </Button>
              </div>
            ) : recentOrders.length === 0 ? (
              <div className="mt-5 rounded-xl bg-[var(--color-muted)] p-5 text-center">
                <p className="font-medium">{t("shop.noOrders", "No orders yet")}</p>
                <p className="mt-1 text-sm text-[var(--color-muted-foreground)]">
                  {t("shop.noOrdersBody", "Place an order from the cart to see it here.")}
                </p>
              </div>
            ) : (
              <ul className="mt-4 divide-y divide-[var(--color-border)]">
                {recentOrders.map((order) => (
                  <li key={order.id}>
                    <Link
                      to={`/shop/orders/${order.id}`}
                      className="flex items-center justify-between gap-4 py-3 text-sm hover:text-[var(--color-primary)]"
                    >
                      <span className="min-w-0">
                        <span className="block truncate font-mono font-semibold">{order.number}</span>
                        <span className="mt-0.5 block text-xs text-[var(--color-muted-foreground)]">
                          {formatDate(order.placedAt ?? order.cutoffAt)}
                        </span>
                      </span>
                      <span className="shrink-0 rounded-full bg-[var(--color-muted)] px-2.5 py-1 text-xs font-semibold">
                        {order.status}
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      ) : null}
    </div>
  );
}

function SummaryCard({
  icon: Icon,
  label,
  value,
  detail,
  href,
}: {
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  value: string;
  detail: string;
  href?: string;
}) {
  const content = (
    <div className="h-full rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-4">
      <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)]">
        <Icon className="size-4" aria-hidden />
        {label}
      </div>
      <div className="mt-3 truncate font-display text-xl font-bold">{value}</div>
      <div className="mt-1 truncate text-xs text-[var(--color-muted-foreground)]">{detail}</div>
    </div>
  );
  return href ? <Link to={href}>{content}</Link> : content;
}

function ActionLink({
  to,
  icon: Icon,
  label,
}: {
  to: string;
  icon: React.ComponentType<{ className?: string }>;
  label: string;
}) {
  return (
    <Link
      to={to}
      className="flex items-center justify-between gap-3 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-4 font-semibold transition-colors hover:border-[var(--color-primary)] hover:text-[var(--color-primary)]"
    >
      <span className="flex items-center gap-3">
        <Icon className="size-5" aria-hidden />
        {label}
      </span>
      <ArrowRight className="size-4" aria-hidden />
    </Link>
  );
}

function OverviewSkeleton() {
  return (
    <div className="space-y-5" role="status" aria-busy="true">
      <Skeleton className="h-40 w-full rounded-2xl" />
      <div className="grid gap-3 sm:grid-cols-3">
        <Skeleton className="h-28 rounded-xl" />
        <Skeleton className="h-28 rounded-xl" />
        <Skeleton className="h-28 rounded-xl" />
      </div>
    </div>
  );
}
