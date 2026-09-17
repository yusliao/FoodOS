import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Route } from "lucide-react";
import { searchRoutes } from "@/api/logistics";
import { searchWarehouses, type WarehouseDto } from "@/api/inventory";
import { useAuth } from "@/auth/use-auth";
import { EntityPageHeader, ErrorBand, LoadingRow, Pagination } from "@/components/list";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n/locale-provider";
import { InventoryPermissions, LogisticsPermissions, OrderingPermissions } from "@/lib/permissions";
import { describe } from "@/pages/customers/request-error";
import { CreateRouteDialog } from "./create-route";

export function DeliveryRoutesPage() {
  const t = useT();
  const { user } = useAuth();
  const grants = user?.permissions ?? [];
  const canView = grants.includes(LogisticsPermissions.Routes.View);
  const canChoose = canView && grants.includes(InventoryPermissions.Warehouses.View);
  const canCreate = grants.includes(LogisticsPermissions.Routes.Create);
  const canStores = grants.includes(OrderingPermissions.Stores.View);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [warehouse, setWarehouse] = useState<WarehouseDto | null>(null);
  const [creating, setCreating] = useState(false);
  const warehouses = useQuery({ queryKey: ["logistics", "route-warehouses", search, page], queryFn: ({ signal }) => searchWarehouses(search, page, signal), enabled: canChoose });
  const routes = useQuery({ queryKey: ["logistics", "routes", warehouse?.id], queryFn: ({ signal }) => searchRoutes(warehouse!.id, signal), enabled: canChoose && !!warehouse });
  return <div className="space-y-6">
    <EntityPageHeader icon={Route} title={t("deliveryRoutes.title")} description={t("deliveryRoutes.description")}>
      {canCreate && <Button disabled={!canChoose || !canStores || !warehouse} onClick={() => setCreating(true)}>{t("deliveryRoutes.create")}</Button>}
    </EntityPageHeader>
    {!canChoose && <p role="status">{t("deliveryRoutes.warehousePermission")}</p>}
    {canCreate && !canStores && <p role="status">{t("deliveryRoutes.storePermission")}</p>}
    {canChoose && <section className="space-y-3 rounded-xl border p-4" aria-label={t("deliveryRoutes.warehouse")}>
      <label className="block space-y-2"><span>{t("deliveryRoutes.warehouse")}</span><Input value={search} onChange={event => { setSearch(event.target.value); setPage(1); }} /></label>
      {warehouse && <p>{t("deliveryRoutes.selected")}: {warehouse.code} · {warehouse.name}</p>}
      {warehouses.isPending && <LoadingRow label={t("deliveryRoutes.loading")} />}
      {warehouses.isError && <><ErrorBand message={describe(warehouses.error, t("deliveryRoutes.failed"))} /><Button onClick={() => void warehouses.refetch()}>{t("workbench.retry")}</Button></>}
      {warehouses.isSuccess && <>
        {warehouses.data.items.length === 0 && <p role="status">{t("deliveryRoutes.noChoices")}</p>}
        <div className="flex flex-wrap gap-2">{warehouses.data.items.map(item => <Button className="h-auto whitespace-normal text-left" variant="outline" key={item.id} aria-pressed={warehouse?.id === item.id} onClick={() => { setWarehouse(item); setCreating(false); }}>{item.code} · {item.name}</Button>)}</div>
        <Pagination page={page} totalPages={warehouses.data.totalPages} totalCount={warehouses.data.totalCount} shown={warehouses.data.items.length} hasPrev={warehouses.data.hasPrevious} hasNext={warehouses.data.hasNext} fetching={warehouses.isFetching} onPrev={() => setPage(value => value - 1)} onNext={() => setPage(value => value + 1)} />
      </>}
    </section>}
    {canChoose && !warehouse && <p role="status">{t("deliveryRoutes.selectWarehouse")}</p>}
    {canChoose && warehouse && <>
      {routes.isPending && <LoadingRow label={t("deliveryRoutes.loading")} />}
      {routes.isError && <><ErrorBand message={describe(routes.error, t("deliveryRoutes.failed"))} /><Button onClick={() => void routes.refetch()}>{t("workbench.retry")}</Button></>}
      {routes.isSuccess && routes.data.length === 0 && <p role="status">{t("deliveryRoutes.empty")}</p>}
      {routes.isSuccess && <div className="grid gap-4 md:grid-cols-2">{routes.data.map(route => <article key={route.id} className="min-w-0 space-y-3 rounded-xl border p-4"><h2 className="break-all font-semibold">{route.code}</h2><p className="break-all text-sm">{t("deliveryRoutes.vehicle")}: {route.defaultVehicleId || t("deliveryRoutes.none")}</p><h3>{t("deliveryRoutes.stops")}</h3><ol className="list-inside list-decimal text-sm">{route.storeIds.map((id, index) => <li className="break-all" key={index}>{id}</li>)}</ol></article>)}</div>}
    </>}
    {creating && canChoose && canCreate && canStores && warehouse && <CreateRouteDialog warehouse={warehouse} canVehicles={grants.includes(LogisticsPermissions.Vehicles.View)} onClose={() => setCreating(false)} />}
  </div>;
}
