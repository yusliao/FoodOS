import { useMemo, useState, useEffect } from "react";
import { useSearchParams } from "react-router-dom";
import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { ChevronRight, RefreshCw, ScrollText, X } from "lucide-react";
import {
  AUDIT_EVENT_TYPES,
  AUDIT_SEVERITIES,
  listAudits,
  getAuditSummary,
  type AuditEventType,
  type AuditSeverity,
  type AuditSummaryDto,
} from "@/api/audits";
import { useAuth } from "@/auth/use-auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import {
  EntityPageHeader,
  ErrorBand,
  Pagination,
  StatStrip,
  Stat,
  Select,
  FilterBar,
  LoadingRow,
} from "@/components/list";
import { EmptyState } from "@/components/empty-state";
import { ApiRequestError } from "@/lib/api-client";
import { AuditingPermissions } from "@/lib/permissions";
import { AuditDetailSheet } from "@/pages/audits/detail";
import { cn } from "@/lib/cn";
import { useT } from "@/i18n/locale-provider";

const PAGE_SIZE = 25;

// Track the search box in a local debounced state so we don't fire a query
// per keystroke — debounce to ~250ms before pushing to the URL.
const SEARCH_DEBOUNCE_MS = 250;

export function AuditsListPage() {
  const t = useT();
  const { user } = useAuth();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [params, setParams] = useSearchParams();

  const canCrossTenant = (user?.permissions ?? []).includes(
    AuditingPermissions.AuditTrails.ViewCrossTenant,
  );

  const pageNumber = Number(params.get("page") ?? "1") || 1;
  const eventType = (params.get("type") as AuditEventType | null) ?? "";
  const severity = (params.get("sev") as AuditSeverity | null) ?? "";
  const tenantId = params.get("tenant") ?? "";
  const correlationId = params.get("corr") ?? "";
  const search = params.get("q") ?? "";

  // Local search box state + debounced sync.
  const [searchInput, setSearchInput] = useState(search);
  useEffect(() => setSearchInput(search), [search]);
  useEffect(() => {
    const handle = setTimeout(() => {
      if (searchInput === search) return;
      const next = new URLSearchParams(params);
      if (searchInput.trim()) next.set("q", searchInput.trim());
      else next.delete("q");
      next.set("page", "1");
      setParams(next, { replace: true });
    }, SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(handle);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchInput]);

  const query = useQuery({
    queryKey: ["audits", { pageNumber, eventType, severity, tenantId, correlationId, search }],
    queryFn: () =>
      listAudits({
        pageNumber,
        pageSize: PAGE_SIZE,
        eventType: eventType || undefined,
        severity: severity || undefined,
        tenantId: tenantId || undefined,
        correlationId: correlationId || undefined,
        search: search || undefined,
        sort: "-OccurredAtUtc",
      }),
    placeholderData: keepPreviousData,
  });

  const summary = useQuery({
    queryKey: ["audits", "summary", { tenantId }],
    queryFn: () => getAuditSummary({ tenantId: tenantId || undefined }),
  });

  const data = query.data;
  const items: AuditSummaryDto[] = data?.items ?? [];

  const summaryStats = useMemo(() => {
    const s = summary.data;
    if (!s) return { total: 0, errors: 0, security: 0, exceptions: 0 };
    const total = Object.values(s.eventsByType).reduce((a, b) => a + (b ?? 0), 0);
    const errors = (s.eventsBySeverity.Error ?? 0) + (s.eventsBySeverity.Critical ?? 0);
    const security = s.eventsByType.Security ?? 0;
    const exceptions = s.eventsByType.Exception ?? 0;
    return { total, errors, security, exceptions };
  }, [summary.data]);

  const setParam = (key: string, value: string | null) => {
    const next = new URLSearchParams(params);
    if (value && value.length > 0) next.set(key, value);
    else next.delete(key);
    next.set("page", "1");
    setParams(next, { replace: true });
  };

  const setPage = (n: number) => {
    const next = new URLSearchParams(params);
    next.set("page", String(n));
    setParams(next, { replace: true });
  };

  const clearAll = () => {
    setParams(new URLSearchParams(), { replace: true });
    setSearchInput("");
  };

  const activeFilters = [eventType, severity, tenantId, correlationId, search].filter(Boolean).length;

  return (
    <div className="space-y-8">
      <EntityPageHeader
        icon={ScrollText}
        title={t("audits.title")}
        total={data?.totalCount ?? null}
        unit={t("audits.unit")}
        description={t("audits.description")}
      >
        <Button
          variant="outline"
          size="sm"
          disabled={query.isFetching}
          onClick={() => query.refetch()}
          className="flex-1 sm:flex-none"
        >
          <RefreshCw className={cn("mr-1.5 h-3.5 w-3.5", query.isFetching && "animate-spin")} />
          {t("audits.refresh")}
        </Button>
      </EntityPageHeader>

      <StatStrip cols={4}>
        <Stat label={t("audits.statTotal")} value={summary.isLoading ? "—" : summaryStats.total.toLocaleString()} hint={t("audits.statTotalHint")} />
        <Stat label={t("audits.statErrors")} value={summary.isLoading ? "—" : summaryStats.errors.toLocaleString()} hint={t("audits.statErrorsHint")} tone={summaryStats.errors > 0 ? "danger" : "default"} />
        <Stat label={t("audits.statSecurity")} value={summary.isLoading ? "—" : summaryStats.security.toLocaleString()} hint={t("audits.statSecurityHint")} tone={summaryStats.security > 0 ? "info" : "default"} />
        <Stat label={t("audits.statExceptions")} value={summary.isLoading ? "—" : summaryStats.exceptions.toLocaleString()} hint={t("audits.statExceptionsHint")} tone={summaryStats.exceptions > 0 ? "warning" : "default"} />
      </StatStrip>

      <FilterBar
        trailing={
          activeFilters > 0 ? (
            <Button variant="ghost" size="sm" onClick={clearAll} className="text-xs">
              <X className="mr-1 h-3.5 w-3.5" /> {t("audits.clearAll").replace("{n}", String(activeFilters))}
            </Button>
          ) : undefined
        }
      >
        <div className="min-w-[16rem] flex-1">
          <Input
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder={t("audits.searchPlaceholder")}
            aria-label={t("audits.searchAria")}
            className="h-8"
          />
        </div>
        <Select
          value={eventType}
          onValueChange={(v) => setParam("type", v || null)}
          options={AUDIT_EVENT_TYPES.map((type) => ({ value: type, label: eventTypeLabel(type, t) }))}
          emptyLabel={t("audits.allEventTypes")}
          className="min-w-[10rem]"
        />
        <Select
          value={severity}
          onValueChange={(v) => setParam("sev", v || null)}
          options={AUDIT_SEVERITIES.map((s) => ({ value: s, label: severityLabel(s, t) }))}
          emptyLabel={t("audits.allSeverities")}
          className="min-w-[10rem]"
        />
        {canCrossTenant && (
          <div className="min-w-[12rem]">
            <Input
              value={tenantId}
              onChange={(e) => setParam("tenant", e.target.value || null)}
              placeholder={t("audits.tenantPlaceholder")}
              aria-label={t("audits.tenantAria")}
              className="h-8 font-mono text-xs"
            />
          </div>
        )}
        <div className="min-w-[14rem]">
          <Input
            value={correlationId}
            onChange={(e) => setParam("corr", e.target.value || null)}
            placeholder={t("audits.corrPlaceholder")}
            aria-label={t("audits.corrAria")}
            className="h-8 font-mono text-xs"
          />
        </div>
      </FilterBar>

      {query.isError && (
        <ErrorBand
          message={
            query.error instanceof ApiRequestError
              ? query.error.problem?.detail ?? query.error.message
              : t("audits.loadFailed")
          }
        />
      )}

      {query.isLoading && <LoadingRow label={t("audits.loading")} />}

      {!query.isLoading && items.length === 0 && !query.isError && (
        <EmptyState
          icon={ScrollText}
          kicker={t("audits.emptyKicker")}
          title={t("audits.emptyTitle")}
          description={
            activeFilters > 0
              ? t("audits.emptyFiltered")
              : t("audits.emptyDefault")
          }
          action={
            activeFilters > 0 ? (
              <Button variant="outline" onClick={clearAll}>
                {t("audits.clearFilters")}
              </Button>
            ) : undefined
          }
        />
      )}

      {items.length > 0 && (
        <ol className="divide-y divide-[var(--color-border)] border-y border-[var(--color-border)]">
          {items.map((event) => (
            <AuditRow key={event.id} event={event} onClick={() => setSelectedId(event.id)} />
          ))}
        </ol>
      )}

      {data && data.totalPages > 1 && (
        <Pagination
          page={data.pageNumber}
          totalPages={data.totalPages}
          totalCount={data.totalCount}
          shown={items.length}
          fetching={query.isFetching}
          hasPrev={data.hasPrevious}
          hasNext={data.hasNext}
          onPrev={() => setPage(Math.max(1, pageNumber - 1))}
          onNext={() => setPage(pageNumber + 1)}
          noun={t("common.events")}
        />
      )}

      {/* Audit detail side sheet */}
      <AuditDetailSheet auditId={selectedId} onClose={() => setSelectedId(null)} />
    </div>
  );
}

function AuditRow({ event, onClick }: { event: AuditSummaryDto; onClick: () => void }) {
  const t = useT();
  return (
    <li>
      <button
        type="button"
        onClick={onClick}
        className="group grid w-full grid-cols-[auto_8rem_auto_1fr_auto] items-center gap-4 px-1 py-3 text-left transition-colors hover:bg-[var(--color-muted)]/50 focus:outline-none focus-visible:bg-[var(--color-muted)]/50"
      >
        <SeverityDot severity={event.severity} />
        <span className="font-mono text-[11px] tabular-nums text-[var(--color-muted-foreground)]">
          {formatTimestamp(event.occurredAtUtc)}
        </span>
        <Badge
          variant={eventTypeVariant(event.eventType)}
          className="justify-self-start font-mono uppercase tracking-[0.14em]"
        >
          {eventTypeLabel(event.eventType, t)}
        </Badge>
        <div className="min-w-0">
          <div className="flex flex-wrap items-baseline gap-2">
            <span className="truncate text-sm font-medium">
              {event.source ?? "—"}
            </span>
            {event.userName && (
              <span className="truncate font-mono text-[11px] text-[var(--color-muted-foreground)]">
                · {event.userName}
              </span>
            )}
            {event.tenantId && (
              <code className="code-chip">{event.tenantId}</code>
            )}
          </div>
          {event.correlationId && (
            <div className="mt-0.5 truncate font-mono text-[10.5px] text-[var(--color-muted-foreground)]">
              {t("audits.corrLine").replace("{id}", event.correlationId)}
            </div>
          )}
        </div>
        <ChevronRight className="h-4 w-4 text-[var(--color-muted-foreground)] transition-transform group-hover:translate-x-0.5" />
      </button>
    </li>
  );
}

// ─── helpers ────────────────────────────────────────────────────────────

function SeverityDot({ severity }: { severity: AuditSeverity }) {
  const t = useT();
  const tone =
    severity === "Critical" || severity === "Error"
      ? "bg-[var(--color-destructive)]"
      : severity === "Warning"
        ? "bg-[var(--color-warning)]"
        : severity === "Information"
          ? "bg-[var(--color-info)]"
          : "bg-[var(--color-muted-foreground)]/50";
  return <span aria-hidden title={severityLabel(severity, t)} className={cn("h-2 w-2 rounded-full", tone)} />;
}

function eventTypeVariant(type: AuditEventType) {
  switch (type) {
    case "Security":
      return "info" as const;
    case "Exception":
      return "danger" as const;
    case "EntityChange":
      return "brand" as const;
    case "Activity":
      return "muted" as const;
    default:
      return "outline" as const;
  }
}

function eventTypeLabel(
  type: AuditEventType,
  t: (key: string, fallback?: string) => string,
): string {
  switch (type) {
    case "EntityChange":
      return t("audits.typeEntityChange");
    case "Security":
      return t("audits.typeSecurity");
    case "Activity":
      return t("audits.typeActivity");
    case "Exception":
      return t("audits.typeException");
    default:
      return t("audits.typeNone");
  }
}

function severityLabel(
  severity: AuditSeverity,
  t: (key: string, fallback?: string) => string,
): string {
  switch (severity) {
    case "Trace":
      return t("audits.sevTrace");
    case "Debug":
      return t("audits.sevDebug");
    case "Information":
      return t("audits.sevInformation");
    case "Warning":
      return t("audits.sevWarning");
    case "Error":
      return t("audits.sevError");
    case "Critical":
      return t("audits.sevCritical");
    default:
      return t("audits.sevNone");
  }
}

function formatTimestamp(value: string): string {
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  // Tight ISO-like local format: 04-30 14:22:01
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
}
