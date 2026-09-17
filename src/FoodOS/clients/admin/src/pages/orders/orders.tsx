import { useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { Receipt } from "lucide-react";
import { toast } from "sonner";
import { getOrder, orderStatuses, reconcileOrder, searchOrders } from "@/api/orders";
import { useAuth } from "@/auth/use-auth";
import { EntityPageHeader, ErrorBand, LoadingRow, Pagination } from "@/components/list";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { useLocale, useT } from "@/i18n/locale-provider";
import { CatalogPermissions, OrderingPermissions } from "@/lib/permissions";
import { describe } from "@/pages/customers/request-error";
import { AfterSales } from "./after-sales";
import { listStores } from "@/api/customers";
import { ManageOrder } from "./manage-order";

const key = ["ordering", "orders"] as const;

export function OrdersPage() {
  const t = useT();
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const [storeId, setStoreId] = useState("");
  const { user } = useAuth();
  const canReadStores = !!user?.permissions.includes(OrderingPermissions.Stores.View);
  const stores = useQuery({ queryKey: ["ordering", "order-store-filter"], queryFn: ({ signal }) => listStores("", signal), enabled: canReadStores });
  const query = useQuery({ queryKey: [...key, page, status, storeId], queryFn: ({ signal }) => searchOrders(page, status, signal, storeId) });
  return <div className="space-y-6">
    <EntityPageHeader icon={Receipt} title={t("orders.title")} description={t("orders.description")} />
    <label className="block space-y-2 text-sm"><span>{t("orders.status")}</span><select className="block h-10 w-full max-w-sm rounded-lg border bg-[var(--color-card)] px-3" value={status} onChange={event => { setStatus(event.target.value); setPage(1); }}>
      <option value="">{t("orders.all")}</option>{orderStatuses.map(value => <option key={value} value={value}>{t(`orders.states.${value}`)}</option>)}
    </select></label>
    {canReadStores && <label className="block space-y-2 text-sm"><span>{t("orders.store")}</span><select className="block h-10 w-full max-w-sm rounded-lg border bg-[var(--color-card)] px-3" value={storeId} onChange={event => { setStoreId(event.target.value); setPage(1); }} disabled={stores.isPending || stores.isError}>
      <option value="">{t("orders.allStores")}</option>{stores.data?.map(store => <option key={store.id} value={store.id}>{store.code} · {store.name}</option>)}
    </select></label>}
    {canReadStores && stores.isPending && <LoadingRow label={t("orders.loading")} />}
    {stores.isError && <div className="space-y-2"><ErrorBand message={describe(stores.error, t("orders.failed"))} /><Button onClick={() => void stores.refetch()}>{t("workbench.retry")}</Button></div>}
    {query.isPending && <LoadingRow label={t("orders.loading")} />}
    {query.isError && <div className="space-y-2"><ErrorBand message={describe(query.error, t("orders.failed"))} /><Button onClick={() => void query.refetch()}>{t("workbench.retry")}</Button></div>}
    {query.isSuccess && <>
      {query.data.items.length === 0 && <p role="status">{t("orders.empty")}</p>}
      <div className="grid gap-4 lg:grid-cols-2">{query.data.items.map(order => <article className="min-w-0 space-y-3 rounded-xl border p-5" key={order.id}>
        <div className="flex flex-wrap justify-between gap-2"><Link className="font-semibold underline" to={`/orders/${order.id}`}>{order.number}</Link><span>{t(`orders.states.${order.status}`, order.status)}</span></div>
        <dl className="space-y-2 text-sm"><div><dt>{t("orders.store")}</dt><dd className="break-all">{order.storeId}</dd></div><div><dt>{t("orders.businessDate")}</dt><dd>{order.businessDate}</dd></div></dl>
      </article>)}</div>
      <Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} shown={query.data.items.length} fetching={query.isFetching} hasPrev={query.data.hasPrevious} hasNext={query.data.hasNext} onPrev={() => setPage(value => value - 1)} onNext={() => setPage(value => value + 1)} />
    </>}
  </div>;
}

export function OrderDetailPage() {
  const { id = "" } = useParams();
  const { t, culture } = useLocale();
  const { user } = useAuth();
  const cache = useQueryClient();
  const [confirming, setConfirming] = useState(false);
  const attempt = useRef<{ id: string; key: string } | null>(null);
  const validId = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id) && id !== "00000000-0000-0000-0000-000000000000";
  const query = useQuery({ queryKey: [...key, id], queryFn: ({ signal }) => getOrder(id, signal), enabled: validId });
  const mutation = useMutation({ mutationFn: reconcileOrder, onSuccess: async () => {
    await cache.invalidateQueries({ queryKey: key });
    setConfirming(false); toast.success(t("orders.reconciled"));
  } });
  const order = query.data;
  const canReconcile = user?.permissions.includes(OrderingPermissions.Orders.Reconcile) && order?.status === "Received";
  const money = (amount: number, currency: string) => new Intl.NumberFormat(culture, { style: "currency", currency }).format(amount);
  return <div className="space-y-6">
    <Link className="underline" to="/orders">{t("orders.back")}</Link>
    <EntityPageHeader icon={Receipt} title={order?.number ?? t("orders.detail")} description={t("orders.reconcileHint")} />
    {!validId && <ErrorBand message={t("orders.invalidId")} />}
    {validId && query.isPending && <LoadingRow label={t("orders.loading")} />}
    {query.isError && <div className="space-y-2"><ErrorBand message={describe(query.error, t("orders.failed"))} /><Button onClick={() => void query.refetch()}>{t("workbench.retry")}</Button></div>}
    {order && <>
      <dl className="grid gap-4 rounded-xl border p-5 text-sm sm:grid-cols-2">
        {[["status", t(`orders.states.${order.status}`, order.status)], ["customer", order.customerOrgId], ["store", order.storeId], ["warehouse", order.warehouseId], ["businessDate", order.businessDate], ["cutoff", new Intl.DateTimeFormat(culture, { dateStyle: "medium", timeStyle: "short" }).format(new Date(order.cutoffAt))]].map(([label, value]) => <div className="min-w-0" key={label}><dt className="text-[var(--color-muted-foreground)]">{t(`orders.${label}`)}</dt><dd className="break-all">{value}</dd></div>)}
      </dl>
      {canReconcile && <Button onClick={() => { mutation.reset(); setConfirming(true); }}>{t("orders.reconcile")}</Button>}
      {user?.permissions.includes(OrderingPermissions.Orders.Manage) && <ManageOrder key={order.id} order={order} canReadProducts={user.permissions.includes(CatalogPermissions.Products.View)} />}
      <h2 className="font-semibold">{t("orders.lines")}</h2>
      <div className="grid gap-4 lg:grid-cols-2">{order.lines.map(line => <article key={line.id} className="min-w-0 space-y-3 rounded-xl border p-5 text-sm">
        <h3 className="break-all font-semibold">{line.productId}</h3>
        <dl className="grid grid-cols-2 gap-3">{[["ordered", line.orderedQty], ["reserved", line.reservedQty], ["delivered", line.deliveredQty], ["returned", line.returnedQty], ["shortage", line.shortageQty], ["unitPrice", money(line.unitPrice, line.currency)]].map(([label, value]) => <div key={label}><dt>{t(`orders.${label}`)}</dt><dd>{value}</dd></div>)}</dl>
        {line.shortageReason && <p>{line.shortageReason}</p>}{line.varianceReason && <p>{line.varianceReason}</p>}
        {line.lots.map(lot => <p className="break-all" key={lot.lotId}>{t("orders.lot")}: {lot.lotNo} · {t("orders.delivered")}: {lot.deliveredQty} · {t("orders.returned")}: {lot.returnedQty}</p>)}
      </article>)}</div>
      <AfterSales order={order} canManage={!!user?.permissions.includes(OrderingPermissions.Orders.Manage)} />
    </>}
    <ConfirmDialog open={confirming && !!canReconcile} onOpenChange={setConfirming} title={t("orders.reconcile")} description={<>{t("orders.reconcileHint")}{mutation.isError && <span role="alert" className="block text-[var(--color-destructive)]">{describe(mutation.error, t("orders.failed"))}</span>}</>} confirmLabel={t("orders.reconcile")} pending={mutation.isPending} onConfirm={() => {
      if (!canReconcile || mutation.isPending) return;
      if (attempt.current?.id !== id) attempt.current = { id, key: crypto.randomUUID() };
      mutation.mutate(attempt.current);
    }} />
  </div>;
}
