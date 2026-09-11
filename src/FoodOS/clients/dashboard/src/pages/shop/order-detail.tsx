import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ClipboardList, Pencil, Trash2 } from "lucide-react";
import { toast } from "sonner";
import {
  amendOrder,
  cancelOrder,
  getOrderById,
  type AmendOrderLineInput,
} from "@/api/ordering";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Dialog,
  DialogBody,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  EntityDetailBack,
  EntityDetailHero,
  EntityDetailSection,
  EntityDetailStat,
  EntityListCard,
  EntityListHeader,
  EntityListRow,
  EntityStatusBadge,
  ErrorBand,
} from "@/components/list";
import { describe, formatDate, formatDateTimeMono, formatMoney } from "@/lib/list-helpers";
import { useT } from "@/i18n/locale-provider";
import { formatCountdown, isAmendable, orderStatusTone, zoneLabel } from "./shop-helpers";
import { useProductsById } from "./use-shop-data";

const DESKTOP_GRID = "grid-cols-[1fr_90px_90px_110px_110px]";

export function ShopOrderDetailPage() {
  const t = useT();
  const { orderId = "" } = useParams<{ orderId: string }>();
  const queryClient = useQueryClient();
  const [qtyDraft, setQtyDraft] = useState<Record<string, number>>({});
  const [confirmCancel, setConfirmCancel] = useState(false);

  const query = useQuery({
    queryKey: ["ordering", "orders", orderId],
    queryFn: () => getOrderById(orderId),
    enabled: !!orderId,
  });

  const order = query.data;
  const products = useProductsById(order?.lines.map((l) => l.productId) ?? []);

  useEffect(() => {
    if (!order) return;
    const next: Record<string, number> = {};
    for (const line of order.lines) {
      next[line.productId] = line.orderedQty;
    }
    setQtyDraft(next);
  }, [order]);

  const amendMutation = useMutation({
    mutationFn: (input: { orderId: string; lines: AmendOrderLineInput[]; idempotencyKey: string }) =>
      amendOrder(input.orderId, input.lines, input.idempotencyKey),
    onSuccess: (_id, input) => {
      toast.success(t("shop.amended", "Order updated"));
      queryClient.invalidateQueries({ queryKey: ["ordering", "orders", input.orderId] });
      queryClient.invalidateQueries({ queryKey: ["ordering", "orders"] });
    },
    onError: (err) =>
      toast.error(t("shop.amendFailed", "Could not amend order"), { description: describe(err) }),
  });

  const cancelMutation = useMutation({
    mutationFn: (input: { orderId: string; idempotencyKey: string }) =>
      cancelOrder(input.orderId, input.idempotencyKey),
    onSuccess: (_id, input) => {
      toast.success(t("shop.cancelled", "Order cancelled"));
      queryClient.invalidateQueries({ queryKey: ["ordering", "orders", input.orderId] });
      queryClient.invalidateQueries({ queryKey: ["ordering", "orders"] });
      setConfirmCancel(false);
    },
    onError: (err) =>
      toast.error(t("shop.cancelFailed", "Could not cancel order"), { description: describe(err) }),
  });

  const cutoff = order ? formatCountdown(order.cutoffAt) : null;
  const canEdit = order ? isAmendable(order.status, order.cutoffAt) : false;
  const total = order?.lines.reduce((s, l) => s + l.unitPrice * l.orderedQty, 0) ?? 0;
  const currency = order?.lines[0]?.currency ?? "USD";

  const onSaveAmend = () => {
    if (!order) return;
    const lines = Object.entries(qtyDraft)
      .filter(([, qty]) => qty > 0)
      .map(([productId, quantity]) => ({ productId, quantity }));
    if (lines.length === 0) {
      toast.error(t("shop.amendNeedsLines", "Keep at least one line, or cancel the order."));
      return;
    }
    amendMutation.mutate({
      orderId: order.id,
      lines,
      idempotencyKey: crypto.randomUUID(),
    });
  };

  return (
    <div className="space-y-5">
      <EntityDetailBack to="/shop/orders" label={t("shop.backToOrders", "Back to orders")} />

      {query.isError ? <ErrorBand message={describe(query.error)} /> : null}

      {query.isLoading ? (
        <Skeleton className="h-40 w-full rounded-xl" />
      ) : order ? (
        <>
          <EntityDetailHero
            title={order.number}
            badges={
              <EntityStatusBadge tone={orderStatusTone(order.status)} withDot>
                {order.status}
              </EntityStatusBadge>
            }
            subtitle={formatDateTimeMono(order.cutoffAt)}
            actions={
              canEdit ? (
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    onClick={onSaveAmend}
                    disabled={amendMutation.isPending}
                  >
                    <Pencil className="size-4" />
                    {amendMutation.isPending
                      ? t("shop.saving", "Saving…")
                      : t("shop.amend", "Save changes")}
                  </Button>
                  <Button variant="destructive" onClick={() => setConfirmCancel(true)}>
                    <Trash2 className="size-4" />
                    {t("shop.cancelOrder", "Cancel order")}
                  </Button>
                </div>
              ) : undefined
            }
            stats={
              <>
                <EntityDetailStat
                  icon={ClipboardList}
                  label={t("shop.colTotal", "Total")}
                  value={formatMoney(total, currency)}
                  tone="primary"
                />
                <EntityDetailStat
                  label={t("shop.colCutoff", "Cutoff")}
                  value={cutoff?.label ?? "—"}
                  tone={cutoff?.open ? "warning" : "danger"}
                />
                <EntityDetailStat
                  label={t("shop.colDate", "Placed")}
                  value={formatDate(order.placedAt)}
                />
              </>
            }
          />

          <p className="text-[13px] text-[var(--color-muted-foreground)]">
            {canEdit
              ? t("shop.beforeCutoff", "You can amend or cancel until cutoff.")
              : t("shop.afterCutoff", "This order is locked. Changes are no longer allowed.")}
          </p>

          <EntityDetailSection title={t("shop.lines", "Lines")} icon={ClipboardList} padded={false}>
            <EntityListCard>
              <EntityListHeader className={DESKTOP_GRID}>
                <span>{t("shop.colProduct", "Product")}</span>
                <span>{t("shop.quantity", "Qty")}</span>
                <span>{t("shop.zone", "Zone")}</span>
                <span>{t("shop.colPrice", "Your price")}</span>
                <span>{t("shop.lineTotal", "Line total")}</span>
              </EntityListHeader>
              {order.lines.map((line, i) => {
                const product = products.byId.get(line.productId);
                return (
                  <EntityListRow
                    key={line.id}
                    className={DESKTOP_GRID}
                    isLast={i === order.lines.length - 1}
                  >
                    <div className="min-w-0">
                      <div className="truncate text-[14px] font-medium">
                        {product?.name ?? line.productId}
                      </div>
                      <code className="font-mono text-[11px] text-[var(--color-muted-foreground)]">
                        {product?.sku ?? ""}
                      </code>
                    </div>
                    {canEdit ? (
                      <Input
                        type="number"
                        min={0}
                        value={qtyDraft[line.productId] ?? line.orderedQty}
                        onChange={(e) =>
                          setQtyDraft((prev) => ({
                            ...prev,
                            [line.productId]: Math.max(0, Number.parseInt(e.target.value, 10) || 0),
                          }))
                        }
                        className="h-8 w-20 tabular-nums"
                        aria-label={t("shop.quantity", "Qty")}
                      />
                    ) : (
                      <span className="tabular-nums">{line.orderedQty}</span>
                    )}
                    <span className="text-[12px]">{zoneLabel(line.zone)}</span>
                    <span data-testid="quoted-price" className="font-display text-[14px] font-semibold tabular-nums">
                      {formatMoney(line.unitPrice, line.currency)}
                    </span>
                    <span className="font-display text-[14px] font-semibold tabular-nums">
                      {formatMoney(line.unitPrice * line.orderedQty, line.currency)}
                    </span>
                  </EntityListRow>
                );
              })}
            </EntityListCard>
          </EntityDetailSection>
        </>
      ) : null}

      <Dialog open={confirmCancel} onOpenChange={setConfirmCancel}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("shop.cancelOrder", "Cancel order")}</DialogTitle>
            <DialogDescription>
              {t("shop.confirmCancel", "Cancel this order? Reserved stock will be released.")}
            </DialogDescription>
          </DialogHeader>
          <DialogBody />
          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline">
                {t("shop.keepOrder", "Keep order")}
              </Button>
            </DialogClose>
            <Button
              variant="destructive"
              disabled={cancelMutation.isPending || !order}
              onClick={() =>
                order &&
                cancelMutation.mutate({
                  orderId: order.id,
                  idempotencyKey: crypto.randomUUID(),
                })
              }
            >
              {cancelMutation.isPending
                ? t("shop.cancelling", "Cancelling…")
                : t("shop.cancelOrder", "Cancel order")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
