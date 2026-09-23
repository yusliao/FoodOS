import { useState } from "react";
import { Link } from "react-router-dom";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { ClipboardList } from "lucide-react";
import { searchShopOrders } from "@/api/shop";
import { Button } from "@/components/ui/button";
import {
  EntityEmpty,
  EntityListCard,
  EntityListHeader,
  EntityListRow,
  EntityMobileCard,
  EntityPageHeader,
  EntityPager,
  EntityStatusBadge,
  ErrorBand,
} from "@/components/list";
import { describe, formatDate, formatMoney } from "@/lib/list-helpers";
import { useT } from "@/i18n/locale-provider";
import { useShopStore } from "./store-context";
import { formatCountdown, orderStatusTone } from "./shop-helpers";

const PAGE_SIZE = 20;
const DESKTOP_GRID = "grid-cols-[1fr_120px_110px_140px_120px]";

export function ShopOrdersPage() {
  const t = useT();
  const { store } = useShopStore();
  const [page, setPage] = useState(1);

  const query = useQuery({
    queryKey: ["shop", "orders", store?.id, page],
    queryFn: () => searchShopOrders({ storeId: store!.id, pageNumber: page, pageSize: PAGE_SIZE }),
    enabled: !!store,
    placeholderData: keepPreviousData,
  });

  const items = query.data?.items ?? [];

  return (
    <div className="space-y-4 sm:space-y-6">
      <EntityPageHeader
        icon={ClipboardList}
        title={t("shop.ordersTitle", "Orders")}
        total={query.data?.totalCount ?? null}
        unit={t("shop.orderUnit", "order")}
        description={t("shop.ordersDescription", "Reserved orders can be amended or cancelled before cutoff.")}
      />

      {query.isError ? <ErrorBand message={describe(query.error)} /> : null}

      {!store ? (
        <p className="text-[13px] text-[var(--color-muted-foreground)]">
          {t("shop.needStore", "Select a store to see contract prices and place orders.")}
        </p>
      ) : items.length === 0 && !query.isLoading ? (
        <EntityEmpty
          icon={ClipboardList}
          title={t("shop.noOrders", "No orders yet")}
          body={t("shop.noOrdersBody", "Place an order from the cart to see it here.")}
          action={
            <Button asChild>
              <Link to="/shop/catalog">{t("shop.browseCatalog", "Browse catalog")}</Link>
            </Button>
          }
        />
      ) : (
        <div>
          <div className="space-y-2 md:hidden">
            {items.map((order) => (
              <EntityMobileCard key={order.id} href={`/shop/orders/${order.id}`}>
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <code className="font-mono text-[13px] font-medium">{order.number}</code>
                    <p className="mt-0.5 text-[12px] text-[var(--color-muted-foreground)]">
                      {formatDate(order.placedAt ?? order.cutoffAt)}
                    </p>
                  </div>
                  <EntityStatusBadge tone={orderStatusTone(order.status)}>{order.status}</EntityStatusBadge>
                </div>
              </EntityMobileCard>
            ))}
          </div>

          <EntityListCard className="hidden md:block">
            <EntityListHeader className={DESKTOP_GRID}>
              <span>{t("shop.colOrder", "Order")}</span>
              <span>{t("shop.colStatus", "Status")}</span>
              <span>{t("shop.colTotal", "Total")}</span>
              <span>{t("shop.colCutoff", "Cutoff")}</span>
              <span>{t("shop.colDate", "Placed")}</span>
            </EntityListHeader>
            {items.map((order, i) => {
              const total = order.lines.reduce((s, l) => s + l.unitPrice * l.orderedQty, 0);
              const currency = order.lines[0]?.currency ?? "USD";
              const countdown = formatCountdown(order.cutoffAt);
              return (
                <EntityListRow
                  key={order.id}
                  className={DESKTOP_GRID}
                  isLast={i === items.length - 1}
                >
                  <Link to={`/shop/orders/${order.id}`} className="min-w-0 font-mono text-[13px] font-medium">
                    {order.number}
                  </Link>
                  <EntityStatusBadge tone={orderStatusTone(order.status)}>{order.status}</EntityStatusBadge>
                  <span className="font-display text-[14px] font-semibold tabular-nums">
                    {formatMoney(total, currency)}
                  </span>
                  <span className="text-[12px] text-[var(--color-muted-foreground)]">{countdown.label}</span>
                  <span className="text-[12px] text-[var(--color-muted-foreground)]">
                    {formatDate(order.placedAt)}
                  </span>
                </EntityListRow>
              );
            })}
          </EntityListCard>

          <EntityPager
            page={page}
            totalPages={query.data?.totalPages ?? 1}
            hasPrev={query.data?.hasPrevious ?? false}
            hasNext={query.data?.hasNext ?? false}
            onPrev={() => setPage((p) => Math.max(1, p - 1))}
            onNext={() => setPage((p) => p + 1)}
          />
        </div>
      )}
    </div>
  );
}
