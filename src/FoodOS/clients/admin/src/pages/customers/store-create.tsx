import { useRef, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { createStore, searchCustomers, type CreateStoreInput } from "@/api/customers";
import { searchWarehouses, type WarehouseDto } from "@/api/inventory";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Field, LoadingRow, Pagination, Select } from "@/components/list";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogBody, DialogFooter } from "@/components/ui/dialog";
import { useT } from "@/i18n/locale-provider";
import { describe } from "./request-error";

export function StoreCreate({ customerOrgId, onClose }: { customerOrgId: string; onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [customer, setCustomer] = useState(customerOrgId);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [address, setAddress] = useState("");
  const [deliveryWindow, setDeliveryWindow] = useState("");
  const [warehouse, setWarehouse] = useState<WarehouseDto | null>(null);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const customers = useQuery({ queryKey: ["customers", ""], queryFn: ({ signal }) => searchCustomers("", signal) });
  const eligibleCustomers = customers.isSuccess ? customers.data.filter(c =>
    c.customerTenantId?.trim() && c.customerTenantId.trim().toLowerCase() !== "root") : [];
  const warehouses = useQuery({ queryKey: ["inventory", "warehouses", search, page], queryFn: ({ signal }) => searchWarehouses(search, page, signal) });
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const mutation = useMutation({
    mutationFn: ({ input, key }: { input: CreateStoreInput; key: string }) => createStore(input, key),
    onSuccess: async () => { await cache.invalidateQueries({ queryKey: ["stores"] }); toast.success(t("partners.storeCreated")); onClose(); },
  });
  const valid = customers.isSuccess && eligibleCustomers.some(c => c.id === customer)
    && warehouse !== null && Boolean(code.trim() && name.trim() && address.trim());
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!valid || !warehouse || mutation.isPending) return;
    const input: CreateStoreInput = { customerOrgId: customer, code: code.trim(), name: name.trim(),
      address: address.trim(), defaultWarehouseId: warehouse.id, defaultRouteId: null, deliveryWindow: deliveryWindow.trim() || null };
    const body = JSON.stringify(input);
    if (attempt.current?.body !== body) attempt.current = { body, key: crypto.randomUUID() };
    mutation.mutate({ input, key: attempt.current.key });
  }
  return <Dialog open onOpenChange={open => { if (!open && !mutation.isPending) onClose(); }}>
    <DialogContent size="lg"><DialogHeader><DialogTitle>{t("partners.newStore")}</DialogTitle>
      <DialogDescription>{t("partners.storeHint")}</DialogDescription></DialogHeader>
      <form onSubmit={submit}><DialogBody className="space-y-4">
        <fieldset disabled={mutation.isPending} className="space-y-4">
          {customers.isPending && <LoadingRow label={t("partners.loading")} />}
          {customers.isError && <div role="alert"><p>{describe(customers.error, t("partners.requestFailed"))}</p><Button type="button" variant="outline" onClick={() => void customers.refetch()}>{t("workbench.retry")}</Button></div>}
          <Field id="store-customer" label={t("partners.customer")} required>
            <Select id="store-customer" value={customer} onValueChange={setCustomer} emptyLabel={t("partners.chooseCustomer")}
              disabled={!customers.isSuccess || mutation.isPending} options={eligibleCustomers.map(c => ({ value: c.id, label: `${c.code} · ${c.name}` }))} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field id="store-code" label={t("partners.code")} required><Input id="store-code" required maxLength={32} value={code} onChange={e => setCode(e.target.value)} /></Field>
            <Field id="store-name" label={t("partners.name")} required><Input id="store-name" required maxLength={128} value={name} onChange={e => setName(e.target.value)} /></Field>
          </div>
          <Field id="store-address" label={t("partners.address")} required><Input id="store-address" required maxLength={256} value={address} onChange={e => setAddress(e.target.value)} /></Field>
          <Field id="store-window" label={t("partners.deliveryWindow")}><Input id="store-window" maxLength={64} value={deliveryWindow} onChange={e => setDeliveryWindow(e.target.value)} /></Field>
          <Field id="warehouse-search" label={t("partners.searchWarehouse")} hint={t("partners.warehouseHint")}>
            <Input id="warehouse-search" type="search" value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} />
          </Field>
          <p className="text-sm" role="status">{t("partners.selectedWarehouse")}: {warehouse ? `${warehouse.code} · ${warehouse.name}` : t("partners.noneSelected")}</p>
          {warehouses.isPending && <LoadingRow label={t("partners.loading")} />}
          {warehouses.isError && <div role="alert"><p>{describe(warehouses.error, t("partners.requestFailed"))}</p><Button type="button" variant="outline" onClick={() => void warehouses.refetch()}>{t("workbench.retry")}</Button></div>}
          {warehouses.isSuccess && <div className="space-y-2">
            {warehouses.data.items.length === 0 && <p>{t("partners.noWarehouses")}</p>}
            <div className="grid gap-2 sm:grid-cols-2">{warehouses.data.items.map(w => <Button key={w.id} type="button" variant="outline" className="h-auto justify-start whitespace-normal break-words text-left" aria-pressed={warehouse?.id === w.id} onClick={() => setWarehouse(w)}>{w.code} · {w.name}</Button>)}</div>
            <Pagination page={page} totalPages={warehouses.data.totalPages} totalCount={warehouses.data.totalCount} shown={warehouses.data.items.length} fetching={warehouses.isFetching}
              hasPrev={warehouses.data.hasPrevious} hasNext={warehouses.data.hasNext} onPrev={() => setPage(p => p - 1)} onNext={() => setPage(p => p + 1)} />
          </div>}
        </fieldset>
        {mutation.isError && <p role="alert" className="text-sm text-[var(--color-destructive)]">{describe(mutation.error, t("partners.requestFailed"))}</p>}
      </DialogBody><DialogFooter>
        <Button type="button" variant="outline" disabled={mutation.isPending} onClick={onClose}>{t("chrome.cancel")}</Button>
        <Button type="submit" disabled={!valid || mutation.isPending}>{t(mutation.isPending ? "partners.saving" : "partners.create")}</Button>
      </DialogFooter></form>
    </DialogContent>
  </Dialog>;
}
