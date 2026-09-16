import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { Store } from "lucide-react";
import { listStores, searchCustomers } from "@/api/customers";
import { useAuth } from "@/auth/use-auth";
import { OrderingPermissions, InventoryPermissions } from "@/lib/permissions";
import { EntityPageHeader, Field, Select, LoadingRow, ErrorBand } from "@/components/list";
import { Button } from "@/components/ui/button";
import { useT } from "@/i18n/locale-provider";
import { describe } from "./request-error";
import { StoreCreate } from "./store-create";

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function StoresPage() {
  const t = useT();
  const { user } = useAuth();
  const granted = user?.permissions ?? [];
  const canViewCustomers = granted.includes(OrderingPermissions.Customers.View);
  const canCreate = granted.includes(OrderingPermissions.Stores.Create);
  const canChoose = canViewCustomers && granted.includes(InventoryPermissions.Warehouses.View);
  const [params, setParams] = useSearchParams();
  const customer = params.get("customerOrgId") ?? "";
  const validFilter = customer === "" || (GUID.test(customer) && customer !== "00000000-0000-0000-0000-000000000000");
  const [creating, setCreating] = useState(false);
  const customers = useQuery({ queryKey: ["customers", ""], queryFn: ({ signal }) => searchCustomers("", signal), enabled: canViewCustomers });
  const stores = useQuery({ queryKey: ["stores", customer], queryFn: ({ signal }) => listStores(customer, signal), enabled: validFilter });
  return <div className="space-y-6">
    <EntityPageHeader icon={Store} title={t("partners.stores")} description={t("partners.storesDescription")}>
      {canCreate && <Button disabled={!canChoose || !validFilter} onClick={() => setCreating(true)}>{t("partners.newStore")}</Button>}
    </EntityPageHeader>
    {canCreate && !canChoose && <p role="status" className="text-sm">{t("partners.lookupPermission")}</p>}
    {canViewCustomers && customers.isSuccess && <Field id="store-filter" label={t("partners.customer")} className="max-w-lg">
      <Select id="store-filter" value={customer} onValueChange={value => setParams(value ? { customerOrgId: value } : {})} emptyLabel={t("partners.allCustomers")}
        options={[
          ...(customer && !customers.data.some(c => c.id === customer) ? [{ value: customer, label: customer }] : []),
          ...customers.data.map(c => ({ value: c.id, label: `${c.code} · ${c.name}` })),
        ]} />
    </Field>}
    {canViewCustomers && customers.isError && <div className="space-y-2"><ErrorBand message={describe(customers.error, t("partners.requestFailed"))} /><Button variant="outline" onClick={() => void customers.refetch()}>{t("workbench.retry")}</Button></div>}
    {customer && <div className="flex flex-wrap items-center gap-2 text-sm break-all"><span>{t("partners.filtered")}: {customer}</span><Button variant="outline" onClick={() => setParams({})}>{t("partners.clearFilter")}</Button></div>}
    {!validFilter && <p role="alert">{t("partners.invalidFilter")}</p>}
    {validFilter && stores.isPending && <LoadingRow label={t("partners.loading")} />}
    {validFilter && stores.isError && <div className="space-y-2"><ErrorBand message={describe(stores.error, t("partners.requestFailed"))} /><Button variant="outline" onClick={() => void stores.refetch()}>{t("workbench.retry")}</Button></div>}
    {validFilter && stores.isSuccess && stores.data.length === 0 && <p role="status">{t("partners.emptyStores")}</p>}
    {validFilter && stores.isSuccess && <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{stores.data.map(store => <article key={store.id} className="space-y-3 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-5">
      <h2 className="font-semibold break-words">{store.name}</h2>
      <dl className="space-y-2 text-sm break-words">
        {[
          ["code", store.code],
          ["customer", (canViewCustomers && customers.isSuccess ? customers.data.find(c => c.id === store.customerOrgId)?.name : null) ?? store.customerOrgId],
          ["tenant", store.customerTenantId ?? t("partners.unlinked")],
          ["address", store.address], ["warehouseId", store.defaultWarehouseId],
          ["routeId", store.defaultRouteId ?? t("partners.unassigned")],
          ["deliveryWindow", store.deliveryWindow ?? t("partners.unassigned")],
        ].map(([key, value]) => <div key={key}><dt className="text-[var(--color-muted-foreground)]">{t(`partners.${key}`)}</dt><dd>{value}</dd></div>)}
      </dl>
    </article>)}</div>}
    {creating && canCreate && canChoose && <StoreCreate customerOrgId={customer} onClose={() => setCreating(false)} />}
  </div>;
}
