import { useRef, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { listStores, type StoreDto } from "@/api/customers";
import { createRoute, searchVehicles } from "@/api/logistics";
import type { WarehouseDto } from "@/api/inventory";
import { ErrorBand, Field, LoadingRow } from "@/components/list";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { useT } from "@/i18n/locale-provider";
import { describe } from "@/pages/customers/request-error";

export function CreateRouteDialog({ warehouse, canVehicles, onClose }: { warehouse: WarehouseDto; canVehicles: boolean; onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [code, setCode] = useState("");
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<StoreDto[]>([]);
  const [vehicle, setVehicle] = useState<string | null>(null);
  const stores = useQuery({ queryKey: ["logistics", "route-stores"], queryFn: ({ signal }) => listStores("", signal) });
  const vehicles = useQuery({ queryKey: ["logistics", "vehicles"], queryFn: ({ signal }) => searchVehicles(signal), enabled: canVehicles });
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const mutation = useMutation({ mutationFn: createRoute, onSuccess: async () => { await cache.invalidateQueries({ queryKey: ["logistics", "routes"] }); onClose(); } });
  const valid = !!code.trim() && selected.length > 0;
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!valid || mutation.isPending) return;
    const body = { warehouseId: warehouse.id, code: code.trim(), storeIds: selected.map(store => store.id), defaultVehicleId: canVehicles ? vehicle : null };
    const serialized = JSON.stringify(body);
    if (attempt.current?.body !== serialized) attempt.current = { body: serialized, key: crypto.randomUUID() };
    mutation.mutate({ body, key: attempt.current.key });
  }
  function move(index: number, offset: number) { setSelected(current => { const next = [...current]; [next[index], next[index + offset]] = [next[index + offset], next[index]]; return next; }); }
  const matches = stores.data?.filter(store => `${store.code} ${store.name}`.toLocaleLowerCase().includes(search.trim().toLocaleLowerCase())) ?? [];
  return <Dialog open onOpenChange={open => !open && !mutation.isPending && onClose()}><DialogContent className="sm:max-w-2xl">
    <DialogHeader><DialogTitle>{t("deliveryRoutes.create")}</DialogTitle><DialogDescription>{t("deliveryRoutes.createHint")}</DialogDescription></DialogHeader>
    <form onSubmit={submit}><DialogBody className="space-y-4"><fieldset disabled={mutation.isPending} className="min-w-0 space-y-4">
      <p>{t("deliveryRoutes.warehouse")}: {warehouse.code} · {warehouse.name}</p>
      <Field id="route-code" label={t("deliveryRoutes.code")} required><Input id="route-code" required maxLength={16} value={code} onChange={event => setCode(event.target.value)} /></Field>
      <Field id="route-store-search" label={t("deliveryRoutes.searchStores")}><Input id="route-store-search" value={search} onChange={event => setSearch(event.target.value)} /></Field>
      {stores.isPending && <LoadingRow label={t("deliveryRoutes.loading")} />}
      {stores.isError && <><ErrorBand message={describe(stores.error, t("deliveryRoutes.failed"))} /><Button type="button" onClick={() => void stores.refetch()}>{t("workbench.retry")}</Button></>}
      {stores.isSuccess && <div className="max-h-40 space-y-2 overflow-y-auto">{matches.length === 0 && <p role="status">{t("deliveryRoutes.noChoices")}</p>}{matches.map(store => <Button type="button" variant="outline" className="h-auto w-full justify-start whitespace-normal text-left" key={store.id} disabled={selected.some(item => item.id === store.id)} onClick={() => setSelected(current => [...current, store])}>{store.code} · {store.name}</Button>)}</div>}
      <h3>{t("deliveryRoutes.stops")}</h3>
      <ol className="space-y-3">{selected.map((store, index) => <li key={store.id} className="space-y-2 rounded-lg border p-3"><p className="break-words">{index + 1}. {store.code} · {store.name}</p><div className="flex flex-wrap gap-2"><Button type="button" variant="outline" disabled={index === 0} onClick={() => move(index, -1)}>{t("deliveryRoutes.up")}</Button><Button type="button" variant="outline" disabled={index === selected.length - 1} onClick={() => move(index, 1)}>{t("deliveryRoutes.down")}</Button><Button type="button" variant="outline" onClick={() => setSelected(current => current.filter(item => item.id !== store.id))}>{t("deliveryRoutes.remove")}</Button></div></li>)}</ol>
      {!canVehicles && <p>{t("deliveryRoutes.vehiclePermission")}</p>}
      {canVehicles && <section aria-label={t("deliveryRoutes.vehicle")} className="space-y-2"><h3>{t("deliveryRoutes.vehicle")}</h3><Button type="button" variant="outline" aria-pressed={vehicle === null} onClick={() => setVehicle(null)}>{t("deliveryRoutes.none")}</Button>
        {vehicles.isPending && <LoadingRow label={t("deliveryRoutes.loading")} />}
        {vehicles.isError && <><ErrorBand message={describe(vehicles.error, t("deliveryRoutes.failed"))} /><Button type="button" onClick={() => void vehicles.refetch()}>{t("workbench.retry")}</Button></>}
        {vehicles.isSuccess && vehicles.data.length === 0 && <p>{t("deliveryRoutes.noChoices")}</p>}
        <div className="flex flex-wrap gap-2">{vehicles.data?.map(item => <Button type="button" variant="outline" key={item.id} aria-pressed={vehicle === item.id} onClick={() => setVehicle(item.id)}>{item.plate}</Button>)}</div>
      </section>}
    </fieldset>{mutation.isError && <ErrorBand message={describe(mutation.error, t("deliveryRoutes.failed"))} />}</DialogBody><DialogFooter><Button type="button" variant="outline" disabled={mutation.isPending} onClick={onClose}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!valid || mutation.isPending}>{t("deliveryRoutes.save")}</Button></DialogFooter></form>
  </DialogContent></Dialog>;
}
