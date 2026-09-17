import { useRef, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createAfterSales, getAfterSales, type SalesOrder } from "@/api/orders";
import { ErrorBand, Field, LoadingRow } from "@/components/list";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { useLocale, useT } from "@/i18n/locale-provider";
import { describe } from "@/pages/customers/request-error";

export function AfterSales({ order, canManage }: { order: SalesOrder; canManage: boolean }) {
  const { t, culture } = useLocale();
  const [creating, setCreating] = useState(false);
  const query = useQuery({ queryKey: ["ordering", "after-sales", order.id, order.storeId], queryFn: ({ signal }) => getAfterSales(order.storeId, order.id, signal) });
  const allowed = canManage && ["Received", "Reconciled"].includes(order.status);
  return <section className="space-y-4">
    <div className="flex flex-wrap items-center justify-between gap-3"><h2 className="font-semibold">{t("afterSales.title")}</h2>{allowed && <Button onClick={() => setCreating(true)}>{t("afterSales.create")}</Button>}</div>
    <p className="text-sm text-[var(--color-muted-foreground)]">{t("afterSales.scope")}</p>
    {query.isPending && <LoadingRow label={t("orders.loading")} />}
    {query.isError && <div className="space-y-2"><ErrorBand message={describe(query.error, t("orders.failed"))} /><Button onClick={() => void query.refetch()}>{t("workbench.retry")}</Button></div>}
    {query.isSuccess && query.data.length === 0 && <p role="status">{t("afterSales.empty")}</p>}
    {query.data?.map(ticket => <article key={ticket.id} className="space-y-2 rounded-xl border p-4 text-sm">
      <h3 className="font-semibold">{t(`afterSales.${ticket.type}`, ticket.type)} · {ticket.quantity}</h3>
      <p className="break-all">{t("afterSales.line")}: {ticket.orderLineId}</p><p className="break-words">{ticket.reason}</p>
      <p>{t(`afterSales.${ticket.status}`, ticket.status)} · {new Intl.DateTimeFormat(culture, { dateStyle: "medium", timeStyle: "short" }).format(new Date(ticket.createdAt))}</p>
    </article>)}
    {creating && allowed && <AfterSalesDialog order={order} onClose={() => setCreating(false)} />}
  </section>;
}

function AfterSalesDialog({ order, onClose }: { order: SalesOrder; onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [lineId, setLineId] = useState("");
  const [type, setType] = useState("Return");
  const [quantity, setQuantity] = useState("");
  const [reason, setReason] = useState("");
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const line = order.lines.find(item => item.id === lineId);
  const remaining = line ? Math.max(0, type === "Shortage" ? line.orderedQty - line.deliveredQty - line.shortageQty : line.deliveredQty - line.returnedQty) : 0;
  const mutation = useMutation({ mutationFn: createAfterSales, onSuccess: async () => {
    await cache.invalidateQueries({ queryKey: ["ordering"] }); onClose();
  } });
  const valid = !!line && Number(quantity) > 0 && Number(quantity) <= remaining && !!reason.trim();
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!valid || mutation.isPending) return;
    const body = { orderId: order.id, orderLineId: lineId, type, quantity: Number(quantity), reason: reason.trim() };
    const serialized = JSON.stringify(body);
    if (attempt.current?.body !== serialized) attempt.current = { body: serialized, key: crypto.randomUUID() };
    mutation.mutate({ body, key: attempt.current.key });
  }
  return <Dialog open onOpenChange={open => !open && !mutation.isPending && onClose()}><DialogContent>
    <DialogHeader><DialogTitle>{t("afterSales.create")}</DialogTitle><DialogDescription>{t("afterSales.warning")}</DialogDescription></DialogHeader>
    <form onSubmit={submit}><DialogBody className="space-y-4"><fieldset className="space-y-4" disabled={mutation.isPending}>
      <Field id="claim-line" label={t("afterSales.line")} required><select id="claim-line" className="h-10 w-full rounded-lg border bg-[var(--color-card)] px-3" value={lineId} onChange={event => setLineId(event.target.value)} required><option value="">{t("afterSales.chooseLine")}</option>{order.lines.map(item => <option key={item.id} value={item.id}>{item.productId}</option>)}</select></Field>
      <Field id="claim-type" label={t("afterSales.type")}><select id="claim-type" className="h-10 w-full rounded-lg border bg-[var(--color-card)] px-3" value={type} onChange={event => setType(event.target.value)}>{["Shortage", "Damage", "Return"].map(value => <option key={value} value={value}>{t(`afterSales.${value}`)}</option>)}</select></Field>
      <Field id="claim-quantity" label={t("afterSales.quantity")} required><Input id="claim-quantity" type="number" min="0.001" step="0.001" max={remaining} required value={quantity} onChange={event => setQuantity(event.target.value)} /></Field>
      <p className="text-sm">{t("afterSales.remaining")}: {remaining}</p>
      <Field id="claim-reason" label={t("afterSales.reason")} required><Input id="claim-reason" maxLength={256} required value={reason} onChange={event => setReason(event.target.value)} /></Field>
    </fieldset>{mutation.isError && <ErrorBand message={describe(mutation.error, t("orders.failed"))} />}</DialogBody>
    <DialogFooter><Button variant="outline" type="button" disabled={mutation.isPending} onClick={onClose}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!valid || mutation.isPending}>{t("afterSales.submit")}</Button></DialogFooter></form>
  </DialogContent></Dialog>;
}
