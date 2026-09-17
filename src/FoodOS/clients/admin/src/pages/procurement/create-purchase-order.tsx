import { useRef, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { searchProducts } from "@/api/catalog";
import { searchWarehouses } from "@/api/inventory";
import { createPurchaseOrder, searchSuppliers } from "@/api/procurement";
import { ErrorBand, Field, LoadingRow, Pagination, Select } from "@/components/list";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { useT } from "@/i18n/locale-provider";
import { describe } from "@/pages/customers/request-error";

type Choice = { id: string; label: string };
type Choices = { items: Choice[]; totalCount: number; totalPages: number; hasNext: boolean; hasPrevious: boolean };
type Loader = (search: string, page: number, signal: AbortSignal) => Promise<Choices>;
const suppliers: Loader = async (search, _page, signal) => {
  const data = await searchSuppliers(search, signal);
  return { items: data.map(item => ({ id: item.id, label: item.code + " · " + item.name })), totalCount: data.length, totalPages: 1, hasNext: false, hasPrevious: false };
};
const warehouses: Loader = async (search, page, signal) => {
  const data = await searchWarehouses(search, page, signal);
  return { ...data, items: data.items.map(item => ({ id: item.id, label: item.code + " · " + item.name })) };
};
const products: Loader = async (search, page, signal) => {
  const data = await searchProducts({ search, pageNumber: page, pageSize: 20 }, signal);
  return { ...data, items: data.items.map(item => ({ id: item.id, label: item.sku + " · " + item.name })) };
};

function Lookup({ kind, load, selected, onChoose }: { kind: string; load: Loader; selected?: Choice | null; onChoose: (choice: Choice) => void }) {
  const t = useT();
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const query = useQuery({ queryKey: ["procurement", "create-lookup", kind, search, page], queryFn: ({ signal }) => load(search, page, signal) });
  return <section aria-label={t("purchase." + kind)} className="min-w-0 space-y-3 rounded-lg border p-3">
    <Field id={"po-search-" + kind} label={t("purchase." + kind)}><Input id={"po-search-" + kind} value={search} placeholder={t("purchase.lookupSearch")} onChange={event => { setSearch(event.target.value); setPage(1); }} /></Field>
    {selected && <p className="break-words text-sm">{t("purchase.selected")}: {selected.label}</p>}
    {query.isPending && <LoadingRow label={t("purchase.loading")} />}
    {query.isError && <><ErrorBand message={describe(query.error, t("purchase.failed"))} /><Button type="button" onClick={() => void query.refetch()}>{t("workbench.retry")}</Button></>}
    {query.isSuccess && <>
      {query.data.items.length === 0 && <p role="status">{t("purchase.noChoices")}</p>}
      <div className="max-h-40 space-y-2 overflow-y-auto">{query.data.items.map(item => <Button className="h-auto w-full justify-start whitespace-normal text-left break-words" type="button" variant="outline" key={item.id} aria-pressed={selected?.id === item.id} onClick={() => onChoose(item)}>{item.label}</Button>)}</div>
      <Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} shown={query.data.items.length} hasPrev={query.data.hasPrevious} hasNext={query.data.hasNext} fetching={query.isFetching} onPrev={() => setPage(value => value - 1)} onNext={() => setPage(value => value + 1)} />
    </>}
  </section>;
}

export function CreatePurchaseOrderDialog({ onClose }: { onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [supplier, setSupplier] = useState<Choice | null>(null);
  const [warehouse, setWarehouse] = useState<Choice | null>(null);
  const [expectedAt, setExpectedAt] = useState("");
  const [lines, setLines] = useState<{ key: string; product: Choice; zone: string; quantity: string }[]>([]);
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const mutation = useMutation({ mutationFn: createPurchaseOrder, onSuccess: async () => { await cache.invalidateQueries({ queryKey: ["procurement", "purchase-orders"] }); onClose(); } });
  const valid = supplier && warehouse && Number.isFinite(Date.parse(expectedAt)) && lines.length > 0 && lines.every(line => Number.isFinite(Number(line.quantity)) && Number(line.quantity) > 0);
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!valid || !supplier || !warehouse || mutation.isPending) return;
    const body = { supplierId: supplier.id, warehouseId: warehouse.id, expectedAt: new Date(expectedAt).toISOString(), lines: lines.map(line => ({ productId: line.product.id, zone: line.zone, quantity: Number(line.quantity) })) };
    const serialized = JSON.stringify(body);
    if (attempt.current?.body !== serialized) attempt.current = { body: serialized, key: crypto.randomUUID() };
    mutation.mutate({ body, key: attempt.current.key });
  }
  return <Dialog open onOpenChange={open => !open && !mutation.isPending && onClose()}><DialogContent className="sm:max-w-2xl">
    <DialogHeader><DialogTitle>{t("purchase.create")}</DialogTitle><DialogDescription>{t("purchase.createHint")}</DialogDescription></DialogHeader>
    <form onSubmit={submit}><DialogBody className="space-y-4"><fieldset disabled={mutation.isPending} className="min-w-0 space-y-4">
      <Lookup kind="supplier" load={suppliers} selected={supplier} onChoose={setSupplier} />
      <Lookup kind="warehouse" load={warehouses} selected={warehouse} onChoose={setWarehouse} />
      <Field id="po-expected" label={t("purchase.expected")} required><Input id="po-expected" type="datetime-local" required value={expectedAt} onChange={event => setExpectedAt(event.target.value)} /></Field>
      <Lookup kind="product" load={products} onChoose={product => setLines(current => [...current, { key: crypto.randomUUID(), product, zone: "Ambient", quantity: "1" }])} />
      {lines.length === 0 && <p role="status">{t("purchase.noLines")}</p>}
      {lines.map((line, index) => <section key={line.key} aria-label={t("purchase.line") + " " + (index + 1)} className="space-y-3 rounded-lg border p-3">
        <p className="break-words text-sm font-medium">{index + 1}. {line.product.label}</p>
        <Field id={"po-zone-" + line.key} label={t("purchase.zone")}><Select id={"po-zone-" + line.key} disabled={mutation.isPending} value={line.zone} options={["Ambient", "Chilled", "Frozen"].map(value => ({ value, label: t("purchase." + value) }))} onValueChange={zone => setLines(current => current.map(item => item.key === line.key ? { ...item, zone } : item))} /></Field>
        <Field id={"po-qty-" + line.key} label={t("purchase.quantity")} required><Input id={"po-qty-" + line.key} type="number" min="0.001" step="0.001" required value={line.quantity} onChange={event => setLines(current => current.map(item => item.key === line.key ? { ...item, quantity: event.target.value } : item))} /></Field>
        <Button type="button" variant="outline" onClick={() => setLines(current => current.filter(item => item.key !== line.key))}>{t("purchase.removeLine")}</Button>
      </section>)}
    </fieldset>{mutation.isError && <ErrorBand message={describe(mutation.error, t("purchase.failed"))} />}</DialogBody>
    <DialogFooter><Button type="button" variant="outline" onClick={onClose} disabled={mutation.isPending}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!valid || mutation.isPending}>{t("purchase.create")}</Button></DialogFooter></form>
  </DialogContent></Dialog>;
}
