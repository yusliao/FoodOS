import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ClipboardCheck } from "lucide-react";
import { toast } from "sonner";
import {
  failQualityCheck,
  passQualityCheck,
  PROCUREMENT_PERMISSIONS,
  searchPurchaseOrders,
  type PurchaseOrderDto,
  type PurchaseOrderLineDto,
} from "@/api/procurement";
import { useAuth } from "@/auth/use-auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { EntityEmpty, EntityPageHeader, EntityStatusBadge, ErrorBand, Field } from "@/components/list";
import { describe } from "@/lib/list-helpers";
import { useT } from "@/i18n/locale-provider";
import { JobCard, newIdempotencyKey } from "./ops-helpers";

function remaining(line: PurchaseOrderLineDto): number {
  return Math.max(0, line.quantity - line.receivedQty - line.rejectedQty);
}

function QcLineForm({
  order,
  line,
}: {
  order: PurchaseOrderDto;
  line: PurchaseOrderLineDto;
}) {
  const t = useT();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const perms = user?.permissions ?? [];
  const canPass = perms.includes(PROCUREMENT_PERMISSIONS.qualityPass);
  const canFail = perms.includes(PROCUREMENT_PERMISSIONS.qualityFail);
  const leftover = remaining(line);
  const [lotNo, setLotNo] = useState("");
  const [expiryDate, setExpiryDate] = useState(() => {
    const d = new Date();
    d.setDate(d.getDate() + 7);
    return d.toISOString().slice(0, 10);
  });
  const [quantity, setQuantity] = useState(String(leftover));
  const [sampleQty, setSampleQty] = useState("1");
  const [note, setNote] = useState("");

  const mutation = useMutation({
    mutationFn: async (result: "pass" | "fail") => {
      const body = {
        quantity: Number(quantity),
        sampleQty: Number(sampleQty),
        lotNo: lotNo.trim(),
        expiryDate,
        note: note.trim() || null,
      };
      const key = newIdempotencyKey();
      return result === "pass"
        ? passQualityCheck(order.id, line.id, body, key)
        : failQualityCheck(order.id, line.id, body, key);
    },
    onSuccess: async (_id, result) => {
      toast.success(result === "pass" ? t("ops.qcPassed", "QC passed") : t("ops.qcFailed", "QC failed"));
      await queryClient.invalidateQueries({ queryKey: ["procurement", "purchase-orders"] });
    },
    onError: (error) => toast.error(describe(error)),
  });

  const checks = order.qualityChecks.filter((c) => c.lineId === line.id);

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <p className="font-mono text-[13px]">{line.productId.slice(0, 8)}…</p>
          <p className="text-[12px] text-[var(--color-muted-foreground)]">
            {line.zone} · {leftover}/{line.quantity} {t("ops.remaining", "remaining")}
          </p>
        </div>
        <EntityStatusBadge tone={leftover > 0 ? "warning" : "success"}>
          {leftover > 0 ? t("ops.pendingQc", "Pending QC") : t("ops.lineClosed", "Closed")}
        </EntityStatusBadge>
      </div>

      {checks.map((c) => (
        <p key={c.id} className="text-[12px] text-[var(--color-muted-foreground)]">
          {c.result} · {c.lotNo} · {c.quantity}
        </p>
      ))}

      {leftover > 0 && (canPass || canFail) ? (
        <div className="grid gap-3 sm:grid-cols-2">
          <Field id={`lot-${line.id}`} label={t("ops.lotNo", "Lot no")} required>
            <Input id={`lot-${line.id}`} value={lotNo} onChange={(e) => setLotNo(e.target.value)} />
          </Field>
          <Field id={`exp-${line.id}`} label={t("ops.expiry", "Expiry")} required>
            <Input id={`exp-${line.id}`} type="date" value={expiryDate} onChange={(e) => setExpiryDate(e.target.value)} />
          </Field>
          <Field id={`qty-${line.id}`} label={t("ops.qty", "Quantity")} required>
            <Input id={`qty-${line.id}`} type="number" min="0.0001" step="any" value={quantity} onChange={(e) => setQuantity(e.target.value)} />
          </Field>
          <Field id={`sample-${line.id}`} label={t("ops.sampleQty", "Sample qty")} required>
            <Input id={`sample-${line.id}`} type="number" min="0" step="any" value={sampleQty} onChange={(e) => setSampleQty(e.target.value)} />
          </Field>
          <Field id={`note-${line.id}`} label={t("ops.note", "Note")}>
            <Input id={`note-${line.id}`} value={note} onChange={(e) => setNote(e.target.value)} />
          </Field>
          <div className="flex items-end gap-2">
            {canPass ? (
              <Button
                data-testid={`qc-pass-${line.id}`}
                disabled={mutation.isPending || !lotNo.trim()}
                onClick={() => mutation.mutate("pass")}
              >
                {t("ops.pass", "Pass")}
              </Button>
            ) : null}
            {canFail ? (
              <Button
                variant="destructive"
                data-testid={`qc-fail-${line.id}`}
                disabled={mutation.isPending || !lotNo.trim()}
                onClick={() => mutation.mutate("fail")}
              >
                {t("ops.fail", "Fail")}
              </Button>
            ) : null}
          </div>
        </div>
      ) : null}
    </div>
  );
}

export function QualityDeskPage() {
  const t = useT();
  const [search, setSearch] = useState("");
  const query = useQuery({
    queryKey: ["procurement", "purchase-orders", search],
    queryFn: () => searchPurchaseOrders(search || undefined),
  });

  const orders = useMemo(
    () => (query.data ?? []).filter((o) => o.status !== "Draft"),
    [query.data],
  );

  return (
    <div className="space-y-4 sm:space-y-6">
      <EntityPageHeader
        icon={ClipboardCheck}
        title={t("ops.qcTitle", "Quality desk")}
        description={t("ops.qcDescription", "Pass or fail inbound lines. Purchasing and QC are separate permissions.")}
      />
      {query.isError ? <ErrorBand message={describe(query.error)} /> : null}
      <Input
        placeholder={t("ops.searchPo", "Search PO number")}
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        aria-label={t("ops.searchPo", "Search PO number")}
      />
      {orders.length === 0 && !query.isLoading ? (
        <EntityEmpty
          icon={ClipboardCheck}
          title={t("ops.noPos", "No purchase orders")}
          body={t("ops.noPosBody", "Inbound POs that are sent or receiving show up here for QC.")}
        />
      ) : (
        <div className="space-y-3">
          {orders.map((order) => (
            <JobCard key={order.id}>
              <div className="mb-3 flex items-start justify-between gap-3">
                <div>
                  <code className="font-mono text-[13px] font-medium">{order.number}</code>
                  <p className="text-[12px] text-[var(--color-muted-foreground)]">{order.status}</p>
                </div>
              </div>
              <div className="space-y-4">
                {order.lines.map((line) => (
                  <QcLineForm key={line.id} order={order} line={line} />
                ))}
              </div>
            </JobCard>
          ))}
        </div>
      )}
    </div>
  );
}
