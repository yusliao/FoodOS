import { useEffect, useRef, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { amendOrder, cancelOrder, type SalesOrder } from "@/api/orders";
import { searchProducts } from "@/api/catalog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { ErrorBand, LoadingRow, Pagination } from "@/components/list";
import { useFulfillmentCapabilities } from "@/api/fulfillment";
import { WmsStatusNotice } from "@/components/wms-status";
import { useT } from "@/i18n/locale-provider";
import { describe } from "@/pages/customers/request-error";

export function ManageOrder({ order, canReadProducts }: { order: SalesOrder; canReadProducts: boolean }) {
  const t = useT();
  const cache = useQueryClient();
  const capabilities = useFulfillmentCapabilities();
  const [editing, setEditing] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [now, setNow] = useState(() => Date.now());
  const attempt = useRef<string | null>(null);
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);
  const allowed = order.status === "Reserved" && now < Date.parse(order.cutoffAt);
  const mutation = useMutation({ mutationFn: cancelOrder, onSuccess: async () => {
    await cache.invalidateQueries({ queryKey: ["ordering"] }); setCancelling(false);
  } });
  if (!capabilities.isSuccess || capabilities.data.readiness !== "ready" || capabilities.data.acceptsOrderChanges !== true) return <WmsStatusNotice />;
  if (!allowed) return <p className="text-sm text-[var(--color-muted-foreground)]">{t("orderManage.locked")}</p>;
  return <div className="flex flex-wrap gap-3">
    <Button variant="outline" onClick={() => setEditing(true)}>{t("orderManage.amend")}</Button>
    <Button variant="destructive" onClick={() => { mutation.reset(); setCancelling(true); }}>{t("orderManage.cancel")}</Button>
    <ConfirmDialog open={cancelling} onOpenChange={setCancelling} title={t("orderManage.cancel")} confirmLabel={t("orderManage.cancel")} destructive pending={mutation.isPending} description={<>{t("orderManage.cancelHint")}{mutation.isError && <span role="alert" className="block">{describe(mutation.error, t("orders.failed"))}</span>}</>} onConfirm={() => {
      if (mutation.isPending || Date.now() >= Date.parse(order.cutoffAt)) return;
      attempt.current ??= crypto.randomUUID();
      mutation.mutate({ id: order.id, key: attempt.current });
    }} />
    {editing && <AmendDialog order={order} canReadProducts={canReadProducts} onClose={() => setEditing(false)} />}
  </div>;
}

function AmendDialog({ order, canReadProducts, onClose }: { order: SalesOrder; canReadProducts: boolean; onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [lines, setLines] = useState(() => order.lines.map(line => ({ productId: line.productId, quantity: String(line.orderedQty) })));
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const products = useQuery({ queryKey: ["catalog", "amend-products", search, page], queryFn: ({ signal }) => searchProducts({ search, pageNumber: page, pageSize: 20, isActive: true }, signal), enabled: canReadProducts });
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const mutation = useMutation({ mutationFn: amendOrder, onSuccess: async () => { await cache.invalidateQueries({ queryKey: ["ordering"] }); onClose(); } });
  const valid = lines.length > 0 && lines.every(line => Number.isFinite(Number(line.quantity)) && Number(line.quantity) > 0);
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!valid || mutation.isPending || Date.now() >= Date.parse(order.cutoffAt)) return;
    const payload = lines.map(line => ({ productId: line.productId, quantity: Number(line.quantity) }));
    const body = JSON.stringify(payload);
    if (attempt.current?.body !== body) attempt.current = { body, key: crypto.randomUUID() };
    mutation.mutate({ id: order.id, key: attempt.current.key, lines: payload });
  }
  return <Dialog open onOpenChange={open => !open && !mutation.isPending && onClose()}><DialogContent>
    <DialogHeader><DialogTitle>{t("orderManage.amend")}</DialogTitle><DialogDescription>{t("orderManage.amendHint")}</DialogDescription></DialogHeader>
    <form onSubmit={submit}><DialogBody className="space-y-4"><fieldset disabled={mutation.isPending} className="space-y-4">
      {lines.map((line, index) => <div key={line.productId} className="space-y-2 rounded-lg border p-3">
        <label className="block space-y-2 text-sm"><span className="block break-all">{line.productId}</span><Input aria-label={`${t("orderManage.quantity")} ${index + 1}`} type="number" min="0.001" step="0.001" required value={line.quantity} onChange={event => setLines(current => current.map((item, i) => i === index ? { ...item, quantity: event.target.value } : item))} /></label>
        <Button type="button" variant="outline" size="sm" onClick={() => setLines(current => current.filter(item => item.productId !== line.productId))}>{t("orderManage.remove")}</Button>
      </div>)}
      {canReadProducts ? <section className="space-y-3">
        <label className="block space-y-2 text-sm"><span>{t("orderManage.search")}</span><Input value={search} onChange={event => { setSearch(event.target.value); setPage(1); }} /></label>
        {products.isPending && <LoadingRow label={t("orders.loading")} />}
        {products.isError && <><ErrorBand message={describe(products.error, t("orders.failed"))} /><Button type="button" onClick={() => void products.refetch()}>{t("workbench.retry")}</Button></>}
        {products.isSuccess && <>
          {products.data.items.length === 0 && <p>{t("orderManage.empty")}</p>}
          {products.data.items.map(product => <div key={product.id} className="flex items-center justify-between gap-3 text-sm"><span className="min-w-0 break-words">{product.sku} · {product.name}</span><Button type="button" size="sm" variant="outline" disabled={lines.some(line => line.productId === product.id)} onClick={() => setLines(current => [...current, { productId: product.id, quantity: "1" }])}>{t("orderManage.add")}</Button></div>)}
          <Pagination page={page} totalPages={products.data.totalPages} totalCount={products.data.totalCount} shown={products.data.items.length} hasPrev={products.data.hasPrevious} hasNext={products.data.hasNext} fetching={products.isFetching} onPrev={() => setPage(value => value - 1)} onNext={() => setPage(value => value + 1)} />
        </>}
      </section> : <p className="text-sm">{t("orderManage.productPermission")}</p>}
    </fieldset>{mutation.isError && <ErrorBand message={describe(mutation.error, t("orders.failed"))} />}</DialogBody>
    <DialogFooter><Button type="button" variant="outline" disabled={mutation.isPending} onClick={onClose}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!valid || mutation.isPending}>{t("orderManage.save")}</Button></DialogFooter>
    </form></DialogContent></Dialog>;
}
