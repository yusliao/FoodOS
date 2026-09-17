import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  ChevronLeft,
  ChevronRight,
  FileText,
  Filter,
  X,
} from "lucide-react";
import { listInvoices, type InvoiceDto, type InvoiceStatus } from "@/api/billing";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/list";
import { KpiTile } from "@/components/kpi-tile";
import { ApiRequestError } from "@/lib/api-client";
import { cn } from "@/lib/cn";
import { useT } from "@/i18n/locale-provider";
import { useAuth } from "@/auth/use-auth";
import { BillingPermissions } from "@/lib/permissions";
import { CurrencySummary } from "@/components/billing/currency-summary";

const PAGE_SIZE = 20;

const STATUSES: InvoiceStatus[] = ["Draft", "Issued", "Paid", "Void"];

function formatMoney(amount: number, currency: string) {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(amount);
  } catch {
    return `${amount.toFixed(2)} ${currency}`;
  }
}

const dateShort = new Intl.DateTimeFormat(undefined, {
  month: "short",
  day: "2-digit",
  year: "numeric",
});

function formatDate(iso?: string | null) {
  if (!iso) return "—";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : dateShort.format(d);
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

function describe(
  err: unknown,
  t: (key: string, fallback?: string) => string,
): string {
  if (err instanceof ApiRequestError) return err.problem?.detail ?? err.problem?.title ?? err.message;
  if (err instanceof Error) return err.message;
  return t("billing.loadInvoicesFailed");
}

export function InvoicesListPage() {
  const t = useT();
  const { user } = useAuth();
  const canView = !!user?.permissions.includes(BillingPermissions.View);
  const navigate = useNavigate();
  const [pageNumber, setPageNumber] = useState(1);

  const [tenantFilter, setTenantFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState<InvoiceStatus | "">("");
  const [periodYear, setPeriodYear] = useState("");
  const [periodMonth, setPeriodMonth] = useState("");
  const validFilters = (!periodYear || (Number(periodYear) >= 2000 && Number(periodYear) <= 2100))
    && (!periodMonth || (Number(periodMonth) >= 1 && Number(periodMonth) <= 12));

  const filters = useMemo(
    () => ({
      tenantId: tenantFilter.trim() || undefined,
      status: statusFilter || undefined,
      periodYear: periodYear ? Number(periodYear) : undefined,
      periodMonth: periodMonth ? Number(periodMonth) : undefined,
    }),
    [tenantFilter, statusFilter, periodYear, periodMonth],
  );

  const query = useQuery({
    queryKey: ["billing", "invoices", { pageNumber, ...filters }],
    queryFn: ({ signal }) => listInvoices({ pageNumber, pageSize: PAGE_SIZE, ...filters }, signal),
    enabled: canView && validFilters,
  });

  const unavailable = query.isError || !validFilters;
  const data = unavailable ? undefined : query.data;
  const items = useMemo<InvoiceDto[]>(() => data?.items ?? [], [data]);

  const totals = useMemo(() => {
    const amounts = (rows: InvoiceDto[]) => rows.map(inv => ({ currency: inv.currency, amount: inv.subtotalAmount }));
    return {
      all: amounts(items),
      outstanding: amounts(items.filter(inv => inv.status === "Issued")),
      paid: amounts(items.filter(inv => inv.status === "Paid")),
    };
  }, [items]);

  const filtersDirty =
    !!tenantFilter || !!statusFilter || !!periodYear || !!periodMonth;

  const clearFilters = () => {
    setTenantFilter("");
    setStatusFilter("");
    setPeriodYear("");
    setPeriodMonth("");
    setPageNumber(1);
  };

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <KpiTile
          label={t("billing.pageInvoices")}
          value={query.isLoading ? <Skeleton className="h-7 w-16" /> : unavailable ? "—" : data?.items.length ?? 0}
          subtitle={
            data
              ? t("billing.totalCount").replace("{n}", data.totalCount.toLocaleString())
              : t(unavailable ? "billing.unavailable" : "billing.loadingEllipsis")
          }
        />
        <KpiTile
          label={t("billing.billed")}
          value={
            query.isLoading ? (
              <Skeleton className="h-7 w-24" />
            ) : unavailable ? "—" : (
              <CurrencySummary values={totals.all} />
            )
          }
          subtitle={t("billing.pageAmounts")}
        />
        <KpiTile
          label={t("billing.outstanding")}
          value={
            query.isLoading ? (
              <Skeleton className="h-7 w-24" />
            ) : unavailable ? "—" : (
              <CurrencySummary values={totals.outstanding} />
            )
          }
          subtitle={t("billing.pageOutstanding")}
        />
        <KpiTile
          label={t("billing.paid")}
          value={
            query.isLoading ? (
              <Skeleton className="h-7 w-24" />
            ) : unavailable ? "—" : (
              <CurrencySummary values={totals.paid} />
            )
          }
          subtitle={t("billing.pagePaid")}
        />
      </div>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-3">
          <div>
            <CardTitle className="flex items-center gap-2">
              <Filter className="h-4 w-4 text-[var(--color-muted-foreground)]" />
              <span>{t("billing.filters")}</span>
            </CardTitle>
            <CardDescription>{t("billing.filtersDesc")}</CardDescription>
          </div>
          {filtersDirty && (
            <Button variant="ghost" size="sm" onClick={clearFilters}>
              <X className="mr-1 h-3.5 w-3.5" /> {t("billing.clear")}
            </Button>
          )}
        </CardHeader>
        <CardContent className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <div className="space-y-1.5">
            <Label htmlFor="filter-tenant">{t("billing.tenant")}</Label>
            <Input
              id="filter-tenant"
              placeholder={t("billing.tenantPlaceholder")}
              value={tenantFilter}
              onChange={(e) => {
                setTenantFilter(e.target.value);
                setPageNumber(1);
              }}
              autoComplete="off"
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="filter-status">{t("billing.status")}</Label>
            <Select
              id="filter-status"
              value={statusFilter}
              onValueChange={(v) => {
                setStatusFilter(v as InvoiceStatus | "");
                setPageNumber(1);
              }}
              options={STATUSES.map((s) => ({ value: s, label: statusLabel(s, t) }))}
              emptyLabel={t("billing.all")}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="filter-year">{t("billing.year")}</Label>
            <Input
              id="filter-year"
              aria-invalid={!!periodYear && (Number(periodYear) < 2000 || Number(periodYear) > 2100)}
              inputMode="numeric"
              placeholder="2026"
              value={periodYear}
              onChange={(e) => {
                setPeriodYear(e.target.value.replace(/[^0-9]/g, "").slice(0, 4));
                setPageNumber(1);
              }}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="filter-month">{t("billing.month")}</Label>
            <Input
              id="filter-month"
              aria-invalid={!!periodMonth && (Number(periodMonth) < 1 || Number(periodMonth) > 12)}
              inputMode="numeric"
              placeholder={t("billing.monthPlaceholder")}
              value={periodMonth}
              onChange={(e) => {
                setPeriodMonth(e.target.value.replace(/[^0-9]/g, "").slice(0, 2));
                setPageNumber(1);
              }}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t("billing.invoices")}</CardTitle>
          <CardDescription>
            {data ? (
              t("billing.pageOfTotal")
                .replace("{page}", String(data.pageNumber))
                .replace("{pages}", String(Math.max(data.totalPages, 1)))
                .replace("{total}", data.totalCount.toLocaleString())
            ) : (
              t(unavailable ? "billing.unavailable" : "billing.loading")
            )}
          </CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          {!validFilters && <p role="alert" className="px-6 py-4 text-sm text-[var(--color-destructive)]">{t("billing.invalidPeriod")}</p>}
          {query.isError && (
            <div className="border-t border-[var(--color-border)] px-6 py-4 text-sm text-[var(--color-destructive)]">
              <p>{describe(query.error, t)}</p>
              <Button className="mt-3" variant="outline" disabled={!canView || query.isFetching} onClick={() => query.refetch()}>{t("workbench.retry")}</Button>
            </div>
          )}

          {unavailable ? null : query.isLoading && items.length === 0 ? (
            <ul className="divide-y divide-[var(--color-border)]">
              {Array.from({ length: 5 }).map((_, i) => (
                <li key={i} className="px-6 py-4">
                  <div className="flex items-center justify-between">
                    <div className="space-y-2">
                      <Skeleton className="h-4 w-40" />
                      <Skeleton className="h-3 w-64" />
                    </div>
                    <Skeleton className="h-5 w-20" />
                  </div>
                </li>
              ))}
            </ul>
          ) : items.length === 0 ? (
            <div className="px-6 py-10 text-center text-sm text-[var(--color-muted-foreground)]">
              {t("billing.emptyInvoices")}
            </div>
          ) : (
            <ul>
              {items.map((inv, i) => (
                <li key={inv.id} className="border-t border-[var(--color-border)] first:border-t-0">
                  <button
                    type="button"
                    onClick={() => navigate(`/billing/invoices/${inv.id}`)}
                    className={cn(
                      "fsh-enter grid w-full grid-cols-1 sm:grid-cols-[1fr_auto] items-center gap-x-6 gap-y-2 px-6 py-4 text-left transition-colors hover:bg-[var(--color-muted)] cursor-pointer",
                    )}
                    style={{ animationDelay: `${Math.min(i, 8) * 25}ms` }}
                  >
                  <div className="flex min-w-0 items-center gap-3">
                    <span
                      aria-hidden
                      className="grid h-9 w-9 shrink-0 place-items-center rounded-md bg-[var(--color-surface-2)] text-[var(--color-muted-foreground)] ring-1 ring-inset ring-[var(--color-border)]"
                    >
                      <FileText className="h-4 w-4" />
                    </span>
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <code className="max-w-full break-all rounded bg-[var(--color-surface-2)] px-1.5 py-0.5 font-mono text-[11px] font-medium tracking-tight">
                          {inv.invoiceNumber}
                        </code>
                        <Badge variant={statusVariant(inv.status)}>{statusLabel(inv.status, t)}</Badge>
                        {inv.purpose && (
                          <Badge variant="outline">
                            {inv.purpose === "Subscription"
                              ? t("billing.purposeSubscription")
                              : t("billing.purposeUsage")}
                          </Badge>
                        )}
                      </div>
                      <div className="mt-1 truncate font-mono text-[11px] tracking-tight text-[var(--color-muted-foreground)]">
                        {t("billing.metaLine")
                          .replace("{tenant}", inv.tenantId)
                          .replace("{period}", formatPeriod(inv.periodYear, inv.periodMonth))
                          .replace("{created}", formatDate(inv.createdAtUtc))}
                        {inv.paidAtUtc && (
                          <>
                            {" · "}
                            <span className="text-[var(--color-success)]">
                              {t("billing.paidOn").replace("{date}", formatDate(inv.paidAtUtc))}
                            </span>
                          </>
                        )}
                        {inv.voidedAtUtc && (
                          <>
                            {" · "}
                            <span className="text-[var(--color-destructive)]">
                              {t("billing.voidedOn").replace("{date}", formatDate(inv.voidedAtUtc))}
                            </span>
                          </>
                        )}
                      </div>
                    </div>
                  </div>

                  <div className="text-right">
                    <div className="text-display text-base font-semibold tabular-nums">
                      {formatMoney(inv.subtotalAmount, inv.currency)}
                    </div>
                    {inv.dueAtUtc && inv.status === "Issued" && (
                      <div className="font-mono text-[11px] text-[var(--color-warning)]">
                        {t("billing.dueOn").replace("{date}", formatDate(inv.dueAtUtc))}
                      </div>
                    )}
                  </div>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      <div className="flex items-center justify-between text-sm">
        <div className="font-mono text-[11px] uppercase tracking-[0.18em] text-[var(--color-muted-foreground)]">
          {data
            ? t("billing.pageSlash")
                .replace("{page}", String(data.pageNumber))
                .replace("{pages}", String(Math.max(data.totalPages, 1)))
            : ""}
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={!data?.hasPrevious || query.isFetching}
            onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
          >
            <ChevronLeft className="mr-1 h-4 w-4" /> {t("common.previous")}
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={!data?.hasNext || query.isFetching}
            onClick={() => setPageNumber((p) => p + 1)}
          >
            {t("common.next")} <ChevronRight className="ml-1 h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}
