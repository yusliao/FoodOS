import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { LifeBuoy } from "lucide-react";
import { toast } from "sonner";
import {
  createShopAfterSalesTicket,
  searchShopAfterSalesTickets,
  searchShopOrders,
  type ShopAfterSalesTicketType,
  type ShopOrderDto,
} from "@/api/shop";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Combobox,
  EntityEmpty,
  EntityPageHeader,
  EntityStatusBadge,
  ErrorBand,
  Field,
} from "@/components/list";
import { describe } from "@/lib/list-helpers";
import { useT } from "@/i18n/locale-provider";
import { useShopStore } from "./store-context";
import { ShopOrderAccess } from "./order-access";
import { useShopProducts } from "./use-shop-data";
import { orderStatusTone } from "./shop-helpers";

const TYPES: ShopAfterSalesTicketType[] = ["Shortage", "Damage", "Return"];

function claimable(status: string): boolean {
  return status === "Received" || status === "Reconciled";
}

export function ShopAfterSalesPage() {
  return (
    <ShopOrderAccess>
      <ShopAfterSalesBody />
    </ShopOrderAccess>
  );
}

function ShopAfterSalesBody() {
  const t = useT();
  const { store } = useShopStore();
  const queryClient = useQueryClient();
  const [orderId, setOrderId] = useState<string | null>(null);
  const [lineId, setLineId] = useState<string | null>(null);
  const [type, setType] = useState<ShopAfterSalesTicketType>("Shortage");
  const [quantity, setQuantity] = useState("1");
  const [reason, setReason] = useState("");

  const ordersQuery = useQuery({
    queryKey: ["shop", "orders", store?.id, "after-sales"],
    queryFn: () => searchShopOrders({ storeId: store!.id, pageNumber: 1, pageSize: 50 }),
    enabled: !!store,
  });

  const ticketsQuery = useQuery({
    queryKey: ["shop", "after-sales", store?.id],
    queryFn: () => searchShopAfterSalesTickets(store!.id),
    enabled: !!store,
  });

  const orders = useMemo(
    () => (ordersQuery.data?.items ?? []).filter((o) => claimable(o.status)),
    [ordersQuery.data],
  );
  const selected: ShopOrderDto | undefined = orders.find((o) => o.id === orderId);
  const products = useShopProducts(
    store?.id,
    selected?.lines.map((line) => ({ productId: line.productId, quantity: line.orderedQty })) ?? [],
  );
  const selectedLine = selected?.lines.find((l) => l.id === lineId);

  const mutation = useMutation({
    mutationFn: () =>
      createShopAfterSalesTicket(
        {
          orderId: orderId!,
          orderLineId: lineId!,
          type,
          quantity: Number(quantity),
          reason: reason.trim(),
        },
        crypto.randomUUID(),
      ),
    onSuccess: async () => {
      toast.success(t("shop.afterSalesFiled", "Claim filed"));
      setReason("");
      await queryClient.invalidateQueries({ queryKey: ["shop", "after-sales", store?.id] });
      await queryClient.invalidateQueries({ queryKey: ["shop", "orders"] });
    },
    onError: (err) => toast.error(t("shop.afterSalesFailed", "Could not file claim"), { description: describe(err) }),
  });

  const canSubmit = !!store && !!orderId && !!lineId && Number(quantity) > 0 && reason.trim().length > 0;

  return (
    <div className="space-y-5">
      <EntityPageHeader
        icon={LifeBuoy}
        title={t("shop.afterSalesTitle", "After-sales")}
        total={ticketsQuery.data?.length ?? null}
        unit={t("shop.claimUnit", "claim")}
        description={t(
          "shop.afterSalesDescription",
          "File shortage, damage, or return against a received order. Qty writes back onto the same order line.",
        )}
      />

      {ordersQuery.isError ? <ErrorBand message={describe(ordersQuery.error)} /> : null}
      {ticketsQuery.isError ? <ErrorBand message={describe(ticketsQuery.error)} /> : null}

      {!store ? (
        <p className="text-[13px] text-[var(--color-muted-foreground)]">
          {t("shop.needStore", "Select a store to see contract prices and place orders.")}
        </p>
      ) : (
        <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
          <div className="space-y-4 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-4">
            <h2 className="text-[14px] font-semibold">{t("shop.fileClaim", "File a claim")}</h2>
            <Field id="as-order" label={t("shop.order", "Order")} required>
              <Combobox
                label={t("shop.order", "Order")}
                value={orderId}
                onChange={(id) => {
                  setOrderId(id);
                  setLineId(null);
                }}
                searchable
                placeholder={t("shop.chooseReceivedOrder", "Choose a received order")}
                options={orders.map((o) => ({
                  value: o.id,
                  label: o.number,
                  hint: o.status,
                }))}
              />
            </Field>
            <Field id="as-line" label={t("shop.line", "Line")} required>
              <Combobox
                label={t("shop.line", "Line")}
                value={lineId}
                onChange={setLineId}
                disabled={!selected}
                placeholder={t("shop.chooseLine", "Choose a line")}
                options={(selected?.lines ?? []).map((line) => ({
                  value: line.id,
                  label: products.byId.get(line.productId)?.name ?? line.productId.slice(0, 8),
                  hint: `${line.deliveredQty}/${line.orderedQty}`,
                }))}
              />
            </Field>
            {selectedLine ? (
              <p className="text-[12px] text-[var(--color-muted-foreground)]">
                {t("shop.colDelivered", "Delivered")} {selectedLine.deliveredQty} ·{" "}
                {t("shop.colReturned", "Returned")} {selectedLine.returnedQty} ·{" "}
                {t("shop.colShortage", "Shortage")} {selectedLine.shortageQty}
                {selectedLine.shortageReason ? ` (${selectedLine.shortageReason})` : ""}
              </p>
            ) : null}
            <Field id="as-type" label={t("shop.claimType", "Type")} required>
              <Combobox
                label={t("shop.claimType", "Type")}
                value={type}
                onChange={(v) => setType((v as ShopAfterSalesTicketType) ?? "Shortage")}
                options={TYPES.map((value) => ({ value, label: value }))}
              />
            </Field>
            <Field id="as-qty" label={t("shop.quantity", "Qty")} required>
              <Input
                id="as-qty"
                type="number"
                min={0.01}
                step="any"
                value={quantity}
                onChange={(e) => setQuantity(e.target.value)}
                className="h-8 w-28 tabular-nums"
              />
            </Field>
            <Field id="as-reason" label={t("shop.reason", "Reason")} required>
              <Input
                id="as-reason"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                placeholder={t("shop.reasonPlaceholder", "Short note for the claim")}
              />
            </Field>
            <Button disabled={!canSubmit || mutation.isPending} onClick={() => mutation.mutate()}>
              {mutation.isPending ? t("shop.filing", "Filing…") : t("shop.fileClaim", "File a claim")}
            </Button>
          </div>

          <div className="space-y-3">
            <h2 className="text-[14px] font-semibold">{t("shop.claims", "Claims")}</h2>
            {(ticketsQuery.data ?? []).length === 0 ? (
              <EntityEmpty
                icon={LifeBuoy}
                title={t("shop.noClaims", "No claims yet")}
                body={t("shop.noClaimsBody", "Received orders can be claimed here after delivery.")}
              />
            ) : (
              (ticketsQuery.data ?? []).map((ticket) => (
                <div
                  key={ticket.id}
                  className="rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-4"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="text-[13px] font-medium">
                        {ticket.type} · {ticket.quantity}
                      </p>
                      <p className="mt-0.5 text-[12px] text-[var(--color-muted-foreground)]">{ticket.reason}</p>
                      <Link
                        to={`/shop/orders/${ticket.orderId}`}
                        className="mt-1 inline-block font-mono text-[11px] text-[var(--color-primary)]"
                      >
                        {ticket.orderId.slice(0, 8)}…
                      </Link>
                    </div>
                    <EntityStatusBadge tone={orderStatusTone("Received")}>{ticket.status}</EntityStatusBadge>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
}
