import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Activity, ChevronRight, Heart, RefreshCw } from "lucide-react";
import { getLiveness, getReadiness, type HealthEntry, type HealthResult, type HealthStatus } from "@/api/health";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { EntityPageHeader, ErrorBand, SettingsSection, StatStrip, Stat } from "@/components/list";
import { cn } from "@/lib/cn";
import { useT } from "@/i18n/locale-provider";
import { useAuth } from "@/auth/use-auth";
import { MultitenancyPermissions } from "@/lib/permissions";

const REFRESH_INTERVAL_MS = 10_000;

type Translate = (key: string, fallback?: string) => string;

export function HealthPage() {
  const t = useT();
  const { user } = useAuth();
  const canView = !!user?.permissions.includes(MultitenancyPermissions.Tenants.View);
  const live = useQuery({
    queryKey: ["health", "live"],
    queryFn: ({ signal }) => getLiveness(signal),
    enabled: canView,
    refetchInterval: REFRESH_INTERVAL_MS,
    refetchOnWindowFocus: false,
  });

  const ready = useQuery({
    queryKey: ["health", "ready"],
    queryFn: ({ signal }) => getReadiness(signal),
    enabled: canView,
    refetchInterval: REFRESH_INTERVAL_MS,
    refetchOnWindowFocus: false,
  });

  const isLoading = live.isLoading || ready.isLoading;
  const isFetching = live.isFetching || ready.isFetching;

  const liveResult = live.isError ? undefined : live.data;
  const readyResult = ready.isError ? undefined : ready.data;
  const liveStatus = liveResult?.status ?? "Unknown";
  const readyStatus = readyResult?.status ?? "Unknown";

  const readyEntries = readyResult?.results ?? [];
  const checksHealthy = readyEntries.filter((e) => e.status === "Healthy").length;
  const checksDegraded = readyEntries.filter((e) => e.status === "Degraded").length;
  const checksFailing = readyEntries.filter((e) => e.status !== "Healthy" && e.status !== "Degraded").length;

  const refetchAll = () => {
    if (!canView || isFetching) return;
    void live.refetch();
    void ready.refetch();
  };

  return (
    <div className="space-y-8">
      <EntityPageHeader
        icon={Heart}
        tone="success"
        title={t("health.title")}
        description={
          <>
            {t("health.descriptionLead").replace("{n}", String(REFRESH_INTERVAL_MS / 1000))}{" "}
            {t("health.descriptionProbes")}{" "}
            (<code className="code-chip">/health/live</code>{" "}
            <code className="code-chip">/health/ready</code>)
            {t("health.descriptionTail")}
          </>
        }
      >
        <Button variant="outline" size="sm" disabled={isFetching} onClick={refetchAll} className="flex-1 sm:flex-none">
          <RefreshCw className={cn("mr-1.5 h-3.5 w-3.5", isFetching && "animate-spin")} />
          {t("health.refresh")}
        </Button>
      </EntityPageHeader>

      <StatStrip cols={4}>
        <Stat
          label={t("health.liveness")}
          value={<StatusGlyph status={liveStatus} t={t} />}
          hint={t("health.livenessHint")}
          tone={statusToTone(liveStatus)}
        />
        <Stat
          label={t("health.readiness")}
          value={<StatusGlyph status={readyStatus} t={t} />}
          hint={t("health.readinessHint")}
          tone={statusToTone(readyStatus)}
        />
        <Stat
          label={t("health.checksHealthy")}
          value={isLoading || !readyResult ? "—" : checksHealthy.toString()}
          hint={t("health.ofRegistered").replace("{n}", String(readyResult ? readyEntries.length : "—"))}
          tone={checksHealthy > 0 ? "success" : "default"}
        />
        <Stat
          label={t("health.checksFailing")}
          value={isLoading || !readyResult ? "—" : (checksFailing + checksDegraded).toString()}
          hint={
            !readyResult ? t("health.unavailable") : checksDegraded > 0
              ? t("health.failingHintDegraded")
                  .replace("{d}", String(checksDegraded))
                  .replace("{f}", String(checksFailing))
              : t("health.failingHint").replace("{f}", String(checksFailing))
          }
          tone={checksFailing > 0 ? "danger" : checksDegraded > 0 ? "warning" : "default"}
        />
      </StatStrip>

      {live.isError && (
        <ErrorBand
          kind={t("health.kindLiveness")}
          message={t("health.livenessFailed")}
        />
      )}
      {ready.isError && (
        <ErrorBand
          kind={t("health.kindReadiness")}
          message={t("health.readinessFailed")}
        />
      )}

      <ProbeSection
        title={t("health.liveness")}
        path="/health/live"
        result={liveResult}
        loading={live.isLoading}
        description={t("health.livenessDesc")}
        t={t}
      />

      <ProbeSection
        title={t("health.readiness")}
        path="/health/ready"
        result={readyResult}
        loading={ready.isLoading}
        description={t("health.readinessDesc")}
        t={t}
      />
    </div>
  );
}

// ─── subcomponents ──────────────────────────────────────────────────────

function ProbeSection({
  title,
  path,
  result,
  loading,
  description,
  t,
}: {
  title: string;
  path: string;
  result: HealthResult | undefined;
  loading: boolean;
  description: string;
  t: Translate;
}) {
  return (
    <SettingsSection
      title={title}
      description={description}
      footer={
        <div className="flex items-center justify-between">
          {result && <StatusBadge status={result.status} t={t} />}
          <code className="code-chip ml-auto">{path}</code>
        </div>
      }
    >
      {loading ? (
        <div className="py-6 text-sm text-[var(--color-muted-foreground)]">{t("health.probing")}</div>
      ) : !result ? <p className="py-5 text-sm">{t("health.unavailable")}</p> : result.results.length === 0 ? (
        <div className="flex items-center gap-3 py-5">
          <Activity className="h-4 w-4 text-[var(--color-muted-foreground)]" />
          <div>
            <div className="text-sm font-medium">{t("health.noChecks")}</div>
            <div className="text-xs text-[var(--color-muted-foreground)]">
              {t("health.probeStatus")} <code className="code-chip">{healthStatusLabel(result.status, t)}</code>.
            </div>
          </div>
        </div>
      ) : (
        <ul className="-mx-5 divide-y divide-[var(--color-border)] border-t border-[var(--color-border)]">
          {result.results.map((entry) => (
            <CheckRow key={entry.name} entry={entry} t={t} />
          ))}
        </ul>
      )}
    </SettingsSection>
  );
}

function CheckRow({ entry, t }: { entry: HealthEntry; t: Translate }) {
  const [open, setOpen] = useState(false);
  const hasDetails = !!entry.details && Object.keys(entry.details).length > 0;

  const rowInner = (
    <>
      <StatusDot status={entry.status} t={t} />
      <div className="min-w-0">
        <div className="truncate font-mono text-[13px] font-medium">{entry.name}</div>
        {entry.description && (
          <div className="mt-0.5 truncate text-xs text-[var(--color-muted-foreground)]">
            {entry.description}
          </div>
        )}
      </div>
      <span className="font-mono text-[11px] tabular-nums text-[var(--color-muted-foreground)]">
        {entry.durationMs.toFixed(1)}ms
      </span>
      {hasDetails ? (
        <ChevronRight
          className={cn(
            "h-4 w-4 text-[var(--color-muted-foreground)] transition-transform",
            open && "rotate-90",
          )}
        />
      ) : (
        <span className="h-4 w-4" aria-hidden />
      )}
    </>
  );

  return (
    <li>
      {hasDetails ? (
        <button
          type="button"
          onClick={() => setOpen((v) => !v)}
          aria-expanded={open}
          className="grid w-full grid-cols-[auto_1fr_auto_auto] items-center gap-4 px-5 py-3.5 text-left transition-colors hover:bg-[var(--color-muted)]/50 focus-visible:bg-[var(--color-muted)]/50 focus-visible:outline-none"
        >
          {rowInner}
        </button>
      ) : (
        <div className="grid grid-cols-[auto_1fr_auto_auto] items-center gap-4 px-5 py-3.5">
          {rowInner}
        </div>
      )}
      {hasDetails && open && (
        <div className="border-t border-[var(--color-border)] bg-[var(--color-surface-2)] px-5 py-3">
          <dl className="grid grid-cols-1 gap-x-6 gap-y-1.5 sm:grid-cols-2">
            {Object.entries(entry.details ?? {}).map(([k, v]) => (
              <div key={k} className="flex items-baseline justify-between gap-3 border-b border-dashed border-[var(--color-border)] py-1.5">
                <dt className="font-mono text-[10.5px] uppercase tracking-[0.16em] text-[var(--color-muted-foreground)]">
                  {k}
                </dt>
                <dd className="truncate font-mono text-[12px] text-[var(--color-foreground)]">
                  {String(v)}
                </dd>
              </div>
            ))}
          </dl>
        </div>
      )}
    </li>
  );
}

function StatusBadge({ status, t }: { status: HealthStatus; t: Translate }) {
  const variant =
    status === "Healthy" ? "success" : status === "Degraded" ? "warning" : "danger";
  return <Badge variant={variant}>{healthStatusLabel(status, t)}</Badge>;
}

function StatusDot({ status, t }: { status: HealthStatus; t: Translate }) {
  const tone = statusToColor(status);
  return (
    <span
      aria-hidden
      title={healthStatusLabel(status, t)}
      className={cn("h-2 w-2 rounded-full", tone)}
    />
  );
}

function StatusGlyph({ status, t }: { status: HealthStatus; t: Translate }) {
  if (status === "Healthy") {
    return (
      <span className="inline-flex items-center gap-2">
        <span className="pulse-dot" aria-hidden />
        <span>{t("health.statusHealthy")}</span>
      </span>
    );
  }
  return <span>{healthStatusLabel(status, t)}</span>;
}

function healthStatusLabel(status: HealthStatus, t: Translate): string {
  switch (status) {
    case "Healthy":
      return t("health.statusHealthy");
    case "Degraded":
      return t("health.statusDegraded");
    case "Unhealthy":
      return t("health.statusUnhealthy");
    case "Unknown":
      return t("health.statusUnknown");
    default:
      return status;
  }
}

function statusToTone(s: HealthStatus): "default" | "success" | "warning" | "danger" {
  if (s === "Healthy") return "success";
  if (s === "Degraded") return "warning";
  if (s === "Unknown") return "default";
  return "danger";
}

function statusToColor(s: HealthStatus): string {
  if (s === "Healthy") return "bg-[var(--color-success)]";
  if (s === "Degraded") return "bg-[var(--color-warning)]";
  return "bg-[var(--color-destructive)]";
}
