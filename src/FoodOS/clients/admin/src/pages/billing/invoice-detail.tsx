import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Ban, CheckCircle2, Download, FileText, Send } from "lucide-react";
import { toast } from "sonner";
import {
  downloadInvoicePdf,
  getInvoice,
  issueInvoice,
  markInvoicePaid,
  voidInvoice,
  type InvoiceStatus,
  type InvoiceLineItemDto,
} from "@/api/billing";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Input } from "@/components/ui/input";
import { EntityPageHeader, SettingsSection, Field } from "@/components/list";
import { ApiRequestError } from "@/lib/api-client";
import { cn } from "@/lib/cn";
import { useAuth } from "@/auth/use-auth";
import { BillingPermissions } from "@/lib/permissions";
import { useT } from "@/i18n/locale-provider";

function formatMoney(amount: number, currency: string) {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(amount);
  } catch {
    return `${amount.toFixed(2)} ${currency}`;
  }
}

const dateLong = new Intl.DateTimeFormat(undefined, {
  month: "long",
  day: "numeric",
  year: "numeric",
});
function formatDate(iso?: string | null) {
  if (!iso) return "—";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : dateLong.format(d);
}

function formatPeriod(year: number, month: number) {
  return `${year}-${String(month).padStart(2, "0")}`;
}

function statusVariant(status: InvoiceStatus): React.ComponentProps<typeof Badge>["variant"] {
  switch (status) {
    case "Paid":
      return "success";
    case "Issued":
      return "info";
    case "Draft":
      return "warning";
    case "Void":
      return "danger";
    default:
      return "default";
  }
}

function statusLabel(
  status: InvoiceStatus,
  t: (key: string, fallback?: string) => string,
): string {
  switch (status) {
    case "Draft":
      return t("billing.statusDraft");
    case "Issued":
      return t("billing.statusIssued");
    case "Paid":
      return t("billing.statusPaid");
    case "Void":
      return t("billing.statusVoid");
    default:
      return status;
  }
}

function describe(err: unknown, fallback: string): string {
  if (err instanceof ApiRequestError) return err.problem?.detail ?? err.problem?.title ?? err.message;
  if (err instanceof Error) return err.message;
  return fallback;
}

export function InvoiceDetailPage() {
  const t = useT();
  const { invoiceId = "" } = useParams<{ invoiceId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { user: currentUser } = useAuth();
  const canView = !!currentUser?.permissions.includes(BillingPermissions.View);
  const canManageBilling = canView && !!currentUser?.permissions.includes(BillingPermissions.Manage);

  const query = useQuery({
    queryKey: ["billing", "invoice", invoiceId],
    queryFn: ({ signal }) => getInvoice(invoiceId, signal),
    enabled: canView && !!invoiceId,
  });
  const invoice = query.isError ? undefined : query.data;

  const invalidate = () => Promise.all([
    queryClient.invalidateQueries({ queryKey: ["billing", "invoice", invoiceId] }),
    queryClient.invalidateQueries({ queryKey: ["billing", "invoices"] }),
  ]);

  const [dueAt, setDueAt] = useState("");
  const [voidReason, setVoidReason] = useState("");

  const downloadMutation = useMutation({
    mutationFn: ({ id, number }: { id: string; number: string }) => downloadInvoicePdf(id, number),
    onError: (err) =>
      toast.error(t("billing.downloadFailed"), {
        description: describe(err, t("billing.downloadFailedBody")),
      }),
  });

  const issueMutation = useMutation({
    mutationFn: ({ id, due }: { id: string; due: string }) => issueInvoice(id, due ? new Date(due).toISOString() : null),
    onSuccess: () => {
      toast.success(t("billing.issuedToast"), { description: t("billing.issuedToastBody") });
      setDueAt("");
      return invalidate();
    },
    onError: (err) =>
      toast.error(t("billing.issueFailed"), { description: describe(err, t("billing.issueFailedBody")) }),
  });

  const payMutation = useMutation({
    mutationFn: (id: string) => markInvoicePaid(id),
    onSuccess: () => {
      toast.success(t("billing.markedPaid"));
      return invalidate();
    },
    onError: (err) =>
      toast.error(t("billing.markPaidFailed"), {
        description: describe(err, t("billing.markPaidFailedBody")),
      }),
  });

  const voidMutation = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => voidInvoice(id, reason.trim() || null),
    onSuccess: () => {
      toast.success(t("billing.voidedToast"));
      setVoidReason("");
      return invalidate();
    },
    onError: (err) =>
      toast.error(t("billing.voidFailed"), { description: describe(err, t("billing.voidFailedBody")) }),
  });

  const busy = issueMutation.isPending || payMutation.isPending || voidMutation.isPending || query.isFetching;

  return (
    <div className="space-y-6">
      <div>
        <Button variant="ghost" size="sm" onClick={() => navigate("/billing/invoices")} className="-ml-2 mb-4">
          <ArrowLeft className="mr-1 h-4 w-4" /> {t("billing.allInvoices")}
        </Button>

        {query.isLoading ? (
          <div className="space-y-2">
            <Skeleton className="h-7 w-72" />
            <Skeleton className="h-4 w-full max-w-96" />
          </div>
        ) : query.isError ? (
          <div className="text-sm text-[var(--color-destructive)]">
            {describe(query.error, t("billing.loadInvoiceFailed"))}
            <Button className="ml-3" variant="outline" disabled={!canView || query.isFetching} onClick={() => query.refetch()}>{t("workbench.retry")}</Button>
          </div>
        ) : invoice ? (
          <EntityPageHeader
            icon={FileText}
            tone="saffron"
            title={formatMoney(invoice.subtotalAmount, invoice.currency)}
            description={
              <span className="flex flex-wrap items-center gap-2">
                <code className="rounded bg-[var(--color-surface-2)] px-1.5 py-0.5 font-mono text-[11px] font-medium tracking-tight">
                  {invoice.invoiceNumber}
                </code>
                <Badge variant={statusVariant(invoice.status)}>{statusLabel(invoice.status, t)}</Badge>
                {invoice.purpose && (
                  <Badge variant="outline">
                    {invoice.purpose === "Subscription"
                      ? t("billing.purposeSubscription")
                      : t("billing.purposeUsage")}
                  </Badge>
                )}
                <span className="font-mono text-[11px] text-[var(--color-muted-foreground)]">
                  {t("billing.metaLine")
                    .replace("{tenant}", invoice.tenantId)
                    .replace("{period}", formatPeriod(invoice.periodYear, invoice.periodMonth))
                    .replace("{created}", formatDate(invoice.createdAtUtc))}
                  {invoice.periodStartUtc && invoice.periodEndUtc && (
                    ` · ${t("billing.termRange")
                      .replace("{start}", formatDate(invoice.periodStartUtc))
                      .replace("{end}", formatDate(invoice.periodEndUtc))}`
                  )}
                  {invoice.issuedAtUtc &&
                    ` · ${t("billing.issuedOn").replace("{date}", formatDate(invoice.issuedAtUtc))}`}
                  {invoice.dueAtUtc && invoice.status === "Issued" && (
                    <span className="text-[var(--color-warning)]">
                      {" "}
                      · {t("billing.dueOn").replace("{date}", formatDate(invoice.dueAtUtc))}
                    </span>
                  )}
                  {invoice.paidAtUtc && (
                    <span className="text-[var(--color-success)]">
                      {" "}
                      · {t("billing.paidOn").replace("{date}", formatDate(invoice.paidAtUtc))}
                    </span>
                  )}
                  {invoice.voidedAtUtc && (
                    <span className="text-[var(--color-destructive)]">
                      {" "}
                      · {t("billing.voidedOn").replace("{date}", formatDate(invoice.voidedAtUtc))}
                    </span>
                  )}
                </span>
              </span>
            }
          >
            {canView && (
              <Button
                variant="outline"
                size="sm"
                onClick={() =>
                  downloadMutation.mutate({ id: invoice.id, number: invoice.invoiceNumber })
                }
                disabled={downloadMutation.isPending}
                title={t("billing.downloadTitle")}
              >
                <Download className="mr-1.5 h-3.5 w-3.5" />
                {downloadMutation.isPending ? t("billing.preparing") : t("billing.downloadPdf")}
              </Button>
            )}
          </EntityPageHeader>
        ) : null}
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <SettingsSection
          title={t("billing.lineItems")}
          description={
            invoice
              ? t(invoice.lineItems.length === 1 ? "billing.lineOne" : "billing.lineMany").replace(
                  "{n}",
                  String(invoice.lineItems.length),
                )
              : query.isError
                ? t("billing.unavailable")
                : t("billing.loading")
          }
        >
          {query.isError ? (
            <div className="py-8 text-center text-sm text-[var(--color-destructive)]">
              {describe(query.error, t("billing.loadLinesFailed"))}
            </div>
          ) : query.isLoading ? (
            <ul className="-mx-5 divide-y divide-[var(--color-border)] border-t border-[var(--color-border)]">
              {Array.from({ length: 2 }).map((_, i) => (
                <li key={i} className="px-5 py-4">
                  <Skeleton className="h-4 w-1/2" />
                  <Skeleton className="mt-2 h-3 w-1/4" />
                </li>
              ))}
            </ul>
          ) : invoice && invoice.lineItems.length === 0 ? (
            <div className="py-8 text-center text-sm text-[var(--color-muted-foreground)]">
              {t("billing.noLineItems")}
            </div>
          ) : invoice ? (
            <ul className="-mx-5 border-t border-[var(--color-border)]">
              {invoice.lineItems.map((li, i) => (
                <LineItemRow key={li.id} item={li} currency={invoice.currency} delayIndex={i} />
              ))}
              <li className="grid grid-cols-[1fr_auto] items-baseline gap-x-6 border-t-2 border-[var(--color-border-strong)] px-5 py-4">
                <div className="font-mono text-[11px] uppercase tracking-[0.18em] text-[var(--color-muted-foreground)]">
                  {t("billing.subtotal")}
                </div>
                <div className="text-display text-xl font-semibold tabular-nums">
                  {formatMoney(invoice.subtotalAmount, invoice.currency)}
                </div>
              </li>
            </ul>
          ) : null}
        </SettingsSection>

        <div className="space-y-4">
          {invoice && (
            <>
              {canManageBilling && (
                <>
              <SettingsSection
                icon={Send}
                title={t("billing.issue")}
                description={t("billing.issueDesc")}
              >
                <div className={cn("space-y-3", invoice.status !== "Draft" && "opacity-60")}>
                  <Field id="dueAt" label={t("billing.dueDate")} hint={t("billing.dueDateHint")}>
                    <Input
                      id="dueAt"
                      type="date"
                      value={dueAt}
                      onChange={(e) => setDueAt(e.target.value)}
                      disabled={invoice.status !== "Draft" || busy}
                    />
                  </Field>
                  <Button
                    size="sm"
                    disabled={invoice.status !== "Draft" || busy}
                    onClick={() => { if (canManageBilling) issueMutation.mutate({ id: invoiceId, due: dueAt }); }}
                    className="w-full"
                  >
                    {issueMutation.isPending ? t("billing.issuing") : t("billing.issueInvoice")}
                  </Button>
                </div>
              </SettingsSection>

              <SettingsSection
                icon={CheckCircle2}
                title={t("billing.markPaid")}
                description={t("billing.markPaidDesc")}
              >
                <div className={cn(invoice.status !== "Issued" && "opacity-60")}>
                  <Button
                    size="sm"
                    disabled={invoice.status !== "Issued" || busy}
                    onClick={() => { if (canManageBilling) payMutation.mutate(invoiceId); }}
                    className="w-full"
                  >
                    {payMutation.isPending ? t("billing.saving") : t("billing.markAsPaid")}
                  </Button>
                </div>
              </SettingsSection>

              <SettingsSection
                icon={Ban}
                title={t("billing.void")}
                description={t("billing.voidDesc")}
              >
                <div
                  className={cn(
                    "space-y-3",
                    (invoice.status === "Paid" || invoice.status === "Void") && "opacity-60",
                  )}
                >
                  <Field id="voidReason" label={t("billing.reason")} hint={t("billing.reasonHint")}>
                    <Input
                      id="voidReason"
                      placeholder={t("billing.voidPlaceholder")}
                      value={voidReason}
                      onChange={(e) => setVoidReason(e.target.value)}
                      disabled={
                        invoice.status === "Paid" ||
                        invoice.status === "Void" ||
                        busy
                      }
                    />
                  </Field>
                  <Button
                    variant="destructive"
                    size="sm"
                    disabled={
                      invoice.status === "Paid" ||
                      invoice.status === "Void" ||
                      busy
                    }
                    onClick={() => { if (canManageBilling) voidMutation.mutate({ id: invoiceId, reason: voidReason }); }}
                    className="w-full"
                  >
                    {voidMutation.isPending ? t("billing.voiding") : t("billing.voidInvoice")}
                  </Button>
                </div>
              </SettingsSection>
                </>
              )}

              {invoice.notes && (
                <SettingsSection title={t("billing.notes")}>
                  <p className="whitespace-pre-line text-xs text-[var(--color-foreground)]">
                    {invoice.notes}
                  </p>
                </SettingsSection>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}

function LineItemRow({
  item,
  currency,
  delayIndex,
}: {
  item: InvoiceLineItemDto;
  currency: string;
  delayIndex: number;
}) {
  return (
    <li
      className="fsh-enter grid grid-cols-[1fr_auto] items-baseline gap-x-6 border-b border-[var(--color-border)] last:border-b-0 px-5 py-3"
      style={{ animationDelay: `${Math.min(delayIndex, 6) * 30}ms` }}
    >
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-sm font-medium">{item.description}</span>
          <Badge variant={item.kind === "BaseFee" ? "default" : item.kind === "Overage" ? "warning" : "muted"}>
            {item.kind}
          </Badge>
          {item.resource && (
            <span className="font-mono text-[10.5px] uppercase tracking-[0.18em] text-[var(--color-muted-foreground)]">
              {item.resource}
            </span>
          )}
        </div>
        <div className="mt-1 font-mono text-[11px] text-[var(--color-muted-foreground)] tabular-nums">
          {item.quantity.toLocaleString()} × {formatMoney(item.unitPrice, currency)}
        </div>
      </div>
      <div className="text-right text-sm font-semibold tabular-nums">
        {formatMoney(item.amount, currency)}
      </div>
    </li>
  );
}
