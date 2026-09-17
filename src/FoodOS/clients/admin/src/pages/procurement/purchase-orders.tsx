import { useRef, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ClipboardList } from "lucide-react";
import { appointPurchaseOrder, searchPurchaseOrders, sendPurchaseOrder, type PurchaseOrder } from "@/api/procurement";
import { useAuth } from "@/auth/use-auth";
import { EntityPageHeader, ErrorBand, Field, LoadingRow } from "@/components/list";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { useLocale, useT } from "@/i18n/locale-provider";
import { CatalogPermissions, InventoryPermissions, ProcurementPermissions } from "@/lib/permissions";
import { describe } from "@/pages/customers/request-error";
import { WmsStatusNotice } from "@/components/wms-status";
import { CreatePurchaseOrderDialog } from "./create-purchase-order";

const key = ["procurement", "purchase-orders"] as const;
export function PurchaseOrdersPage() {
  const { t, culture } = useLocale();
  const { user } = useAuth();
  const [search, setSearch] = useState("");
  const [creating, setCreating] = useState(false);
  const [appointing, setAppointing] = useState<PurchaseOrder | null>(null);
  const canManage = !!user?.permissions.includes(ProcurementPermissions.Purchase.Create);
  const canChoose = [ProcurementPermissions.Suppliers.View, InventoryPermissions.Warehouses.View, CatalogPermissions.Products.View].every(permission => user?.permissions.includes(permission));
  const query = useQuery({ queryKey: [...key, search], queryFn: ({ signal }) => searchPurchaseOrders(search, signal) });
  return <div className="space-y-6">
    <WmsStatusNotice />
    <EntityPageHeader icon={ClipboardList} title={t("purchase.title")} description={t("purchase.description")}>
      {canManage && <Button disabled={!canChoose} onClick={() => setCreating(true)}>{t("purchase.create")}</Button>}
    </EntityPageHeader>
    {canManage && !canChoose && <p role="status" className="text-sm">{t("purchase.lookupPermission")}</p>}
    <label className="block max-w-md space-y-2 text-sm"><span>{t("purchase.search")}</span><Input value={search} onChange={event => setSearch(event.target.value)} /></label>
    {query.isPending && <LoadingRow label={t("purchase.loading")} />}
    {query.isError && <div className="space-y-2"><ErrorBand message={describe(query.error, t("purchase.failed"))} /><Button onClick={() => void query.refetch()}>{t("workbench.retry")}</Button></div>}
    {query.isSuccess && query.data.length === 0 && <p role="status">{t("purchase.empty")}</p>}
    <div className="grid gap-4 xl:grid-cols-2">{query.data?.map(order => <article key={order.id} className="min-w-0 space-y-4 rounded-xl border p-5">
      <div className="flex flex-wrap justify-between gap-2"><h2 className="font-semibold">{order.number}</h2><span>{t(`purchase.${order.status}`, order.status)}</span></div>
      <dl className="grid gap-3 text-sm sm:grid-cols-2">
        {[["supplier", order.supplierId], ["warehouse", order.warehouseId], ["expected", new Intl.DateTimeFormat(culture, { dateStyle: "medium", timeStyle: "short" }).format(new Date(order.expectedAt))]].map(([label, value]) => <div className="min-w-0" key={label}><dt>{t(`purchase.${label}`)}</dt><dd className="break-all">{value}</dd></div>)}
      </dl>
      {order.appointment && <p className="break-words text-sm">{t("purchase.dock")}: {order.appointment.dockSlot} · {t("purchase.vehicle")}: {order.appointment.vehicleNo || "—"}</p>}
      <div className="flex flex-wrap gap-2">
        {canManage && order.status === "Draft" && <SendButton order={order} />}
        {canManage && !order.appointment && ["Draft", "Sent", "Receiving"].includes(order.status) && <Button variant="outline" onClick={() => setAppointing(order)}>{t("purchase.appoint")}</Button>}
      </div>
      {order.lines.map(line => <div key={line.id} className="space-y-2 rounded-lg border p-3 text-sm"><p className="break-all font-medium">{line.productId}</p><p>{t(`purchase.${line.zone}`, line.zone)} · {t("purchase.quantity")}: {line.quantity}</p><p>{t("purchase.received")}: {line.receivedQty} · {t("purchase.rejected")}: {line.rejectedQty}</p>
      </div>)}
      <details className="text-sm"><summary>{t("purchase.history")} ({order.qualityChecks.length})</summary>{order.qualityChecks.map(check => <div key={check.id} className="mt-2 space-y-1 border-t pt-2"><p>{t(`purchase.${check.result}`, check.result)} · {check.quantity} · {check.lotNo}</p><p className="break-all">{check.lineId}</p><p>{check.note}</p></div>)}</details>
    </article>)}</div>
    {creating && canManage && canChoose && <CreatePurchaseOrderDialog onClose={() => setCreating(false)} />}
    {appointing && canManage && <AppointmentDialog order={appointing} onClose={() => setAppointing(null)} />}
  </div>;
}

function SendButton({ order }: { order: PurchaseOrder }) {
  const t = useT();
  const cache = useQueryClient();
  const [open, setOpen] = useState(false);
  const attempt = useRef<string | null>(null);
  const mutation = useMutation({ mutationFn: sendPurchaseOrder, onSuccess: async () => { await cache.invalidateQueries({ queryKey: key }); setOpen(false); } });
  return <><Button onClick={() => setOpen(true)}>{t("purchase.send")}</Button><ConfirmDialog open={open} onOpenChange={setOpen} title={t("purchase.send")} description={<>{t("purchase.sendHint")}{mutation.isError && <span role="alert" className="block">{describe(mutation.error, t("purchase.failed"))}</span>}</>} confirmLabel={t("purchase.send")} pending={mutation.isPending} onConfirm={() => { attempt.current ??= crypto.randomUUID(); mutation.mutate({ id: order.id, key: attempt.current }); }} /></>;
}

function AppointmentDialog({ order, onClose }: { order: PurchaseOrder; onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [dockSlot, setDockSlot] = useState("");
  const [vehicleNo, setVehicleNo] = useState("");
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const mutation = useMutation({ mutationFn: appointPurchaseOrder, onSuccess: async () => { await cache.invalidateQueries({ queryKey: key }); onClose(); } });
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!dockSlot.trim() || mutation.isPending) return;
    const input = { id: order.id, dockSlot: dockSlot.trim(), vehicleNo: vehicleNo.trim() || null };
    const body = JSON.stringify(input);
    if (attempt.current?.body !== body) attempt.current = { body, key: crypto.randomUUID() };
    mutation.mutate({ ...input, key: attempt.current.key });
  }
  return <Dialog open onOpenChange={open => !open && !mutation.isPending && onClose()}><DialogContent><DialogHeader><DialogTitle>{t("purchase.appoint")}</DialogTitle><DialogDescription>{t("purchase.appointHint")}</DialogDescription></DialogHeader><form onSubmit={submit}><DialogBody className="space-y-4"><fieldset disabled={mutation.isPending} className="space-y-4"><Field id="po-dock" label={t("purchase.dock")} required><Input id="po-dock" required value={dockSlot} onChange={event => setDockSlot(event.target.value)} /></Field><Field id="po-vehicle" label={t("purchase.vehicle")}><Input id="po-vehicle" value={vehicleNo} onChange={event => setVehicleNo(event.target.value)} /></Field></fieldset>{mutation.isError && <ErrorBand message={describe(mutation.error, t("purchase.failed"))} />}</DialogBody><DialogFooter><Button variant="outline" type="button" onClick={onClose} disabled={mutation.isPending}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!dockSlot.trim() || mutation.isPending}>{t("purchase.appoint")}</Button></DialogFooter></form></DialogContent></Dialog>;
}
