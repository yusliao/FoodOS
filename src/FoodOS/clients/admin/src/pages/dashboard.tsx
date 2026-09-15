import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowRight,
  Building2,
  FileText,
  LayoutDashboard,
  Receipt,
  UsersRound,
} from "lucide-react";
import { listTenants } from "@/api/tenants";
import { listInvoices, getPlans } from "@/api/billing";
import { getOpsKpis } from "@/api/ops";
import { Skeleton } from "@/components/ui/skeleton";
import { EntityPageHeader, Stat, StatStrip, ToneIconTile, type ToneIconTileTone } from "@/components/list";
import { useAuth } from "@/auth/use-auth";
import { useT } from "@/i18n/locale-provider";
import { OpsPermissions } from "@/lib/permissions";
import { cn } from "@/lib/cn";

/**
 * DashboardPage — the operator overview. EntityPageHeader greeting,
 * four KPI stat tiles drawing from real data, then pivot cards into
 * the rest of the app. No fake "Coming soon" filler.
 */
export function DashboardPage() {
  const t = useT();
  const { user } = useAuth();

  const tenantsQuery = useQuery({
    queryKey: ["tenants", { pageNumber: 1, pageSize: 1 }],
    queryFn: () => listTenants({ pageNumber: 1, pageSize: 1 }),
  });
  const plansQuery = useQuery({
    queryKey: ["billing", "plans", { includeInactive: true }],
    queryFn: () => getPlans(true),
  });
  const invoicesQuery = useQuery({
    queryKey: ["billing", "invoices", { pageNumber: 1, pageSize: 50 }],
    queryFn: () => listInvoices({ pageNumber: 1, pageSize: 50 }),
  });
  const canViewOps = user?.permissions.includes(OpsPermissions.Kpis.View) ?? false;
  const kpisQuery = useQuery({
    queryKey: ["ops", "kpis"],
    queryFn: () => getOpsKpis(),
    enabled: canViewOps,
  });

  const tenantsTotal = tenantsQuery.data?.totalCount;
  const plans = plansQuery.data ?? [];
  const activePlans = plans.filter((p) => p.isActive).length;
  const invoicesPage = invoicesQuery.data;
  const outstandingCount =
    invoicesPage?.items.filter((i) => i.status === "Issued").length ?? 0;

  const firstName = user?.name?.split(" ")[0];

  return (
    <div className="space-y-6">
      {/* ── Page header ──────────────────────────────────────────────── */}
      <div className="fsh-enter">
        <EntityPageHeader
          icon={LayoutDashboard}
          title={
            <>
              {t("dashboard.title")}
              {firstName ? (
                <span className="text-[var(--color-muted-foreground)]">, {firstName}</span>
              ) : null}
            </>
          }
          tone="primary"
          description={t("dashboard.description")}
        />
      </div>

      {canViewOps ? (
        <section className="space-y-2">
          <p className="text-[11px] font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)]">
            {t("dashboard.operations")}
          </p>
          <StatStrip cols={4} className="fsh-enter fsh-enter-2">
            <Stat
              label={t("dashboard.fulfillment")}
              value={
                kpisQuery.isLoading ? (
                  <Skeleton className="h-7 w-16" />
                ) : (
                  formatRate(kpisQuery.data?.fulfillmentRate, t)
                )
              }
              hint={
                kpisQuery.data
                  ? t("dashboard.fulfillmentHint")
                      .replace("{fulfilled}", String(kpisQuery.data.fulfilledOrderCount))
                      .replace("{committed}", String(kpisQuery.data.committedOrderCount))
                  : t("dashboard.fulfillmentHintEmpty")
              }
              tone="success"
            />
            <Stat
              label={t("dashboard.stockout")}
              value={
                kpisQuery.isLoading ? (
                  <Skeleton className="h-7 w-16" />
                ) : (
                  formatRate(kpisQuery.data?.stockoutRate, t)
                )
              }
              hint={t("dashboard.stockoutHint")}
              tone={(kpisQuery.data?.stockoutRate ?? 0) > 0 ? "warning" : "default"}
            />
            <Stat
              label={t("dashboard.shrinkage")}
              value={
                kpisQuery.isLoading ? (
                  <Skeleton className="h-7 w-16" />
                ) : (
                  formatRate(kpisQuery.data?.shrinkageRate, t)
                )
              }
              hint={t("dashboard.shrinkageHint")}
              tone={(kpisQuery.data?.shrinkageRate ?? 0) > 0 ? "warning" : "default"}
            />
            <Stat
              label={t("dashboard.temperature")}
              value={
                kpisQuery.isLoading ? (
                  <Skeleton className="h-7 w-16" />
                ) : (
                  formatRate(kpisQuery.data?.temperatureComplianceRate, t)
                )
              }
              hint={t("dashboard.temperatureHint")}
            />
          </StatStrip>
        </section>
      ) : null}

      {/* ── KPI stat strip ───────────────────────────────────────────── */}
      <StatStrip cols={4} className="fsh-enter fsh-enter-2">
        <Stat
          label={t("nav.items.tenants")}
          value={
            tenantsQuery.isLoading ? (
              <Skeleton className="h-7 w-16" />
            ) : (
              tenantsTotal?.toLocaleString() ?? "—"
            )
          }
          hint={t("dashboard.tenantsHint")}
        />
        <Stat
          label={t("dashboard.plans")}
          value={
            plansQuery.isLoading ? (
              <Skeleton className="h-7 w-16" />
            ) : (
              plans.length.toLocaleString()
            )
          }
          hint={t("dashboard.plansActive").replace("{n}", String(activePlans))}
        />
        <Stat
          label={t("dashboard.invoices")}
          value={
            invoicesQuery.isLoading ? (
              <Skeleton className="h-7 w-16" />
            ) : (
              invoicesPage?.items.length.toLocaleString() ?? "—"
            )
          }
          hint={
            invoicesPage
              ? t("dashboard.invoicesHint").replace("{n}", invoicesPage.totalCount.toLocaleString())
              : t("dashboard.loading")
          }
        />
        <Stat
          label={t("dashboard.outstanding")}
          value={
            invoicesQuery.isLoading ? (
              <Skeleton className="h-7 w-16" />
            ) : (
              outstandingCount.toLocaleString()
            )
          }
          hint={t("dashboard.outstandingHint")}
          tone={outstandingCount > 0 ? "warning" : "default"}
        />
      </StatStrip>

      {/* ── Quick pivots ─────────────────────────────────────────────── */}
      <section className="fsh-enter fsh-enter-3 space-y-3">
        <p className="text-[11px] font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)]">
          {t("dashboard.entryPoints")}
        </p>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <PivotCard
            to="/tenants"
            icon={Building2}
            tone="info"
            title={t("nav.items.tenants")}
            description={t("dashboard.tenantsDesc")}
          />
          <PivotCard
            to="/users"
            icon={UsersRound}
            tone="primary"
            title={t("nav.items.users")}
            description={t("dashboard.usersDesc")}
          />
          <PivotCard
            to="/billing/plans"
            icon={Receipt}
            tone="success"
            title={t("nav.items.billing")}
            description={t("dashboard.billingDesc")}
          />
          <PivotCard
            to="/billing/invoices"
            icon={FileText}
            tone="warning"
            title={t("dashboard.invoices")}
            description={t("dashboard.invoicesDesc")}
          />
        </div>
      </section>
    </div>
  );
}

// ─── subcomponents ───────────────────────────────────────────────────

function PivotCard({
  to,
  icon: Icon,
  tone,
  title,
  description,
}: {
  to: string;
  icon: typeof Building2;
  tone: ToneIconTileTone;
  title: string;
  description: string;
}) {
  return (
    <Link to={to} className="group block focus:outline-none">
      <div
        className={cn(
          "flex h-full flex-col gap-3 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-4 shadow-xs",
          "transition-colors duration-200 hover:border-[var(--color-border-strong)] hover:bg-[var(--color-accent)]",
        )}
      >
        <div className="flex items-start justify-between">
          <ToneIconTile icon={Icon} tone={tone} size="md" />
          <ArrowRight
            aria-hidden
            className="size-3.5 text-[var(--color-muted-foreground)] opacity-0 transition-all duration-200 group-hover:translate-x-0.5 group-hover:opacity-100"
          />
        </div>
        <div>
          <div className="font-display text-[14px] font-semibold tracking-tight text-[var(--color-foreground)]">
            {title}
          </div>
          <p className="mt-0.5 text-[12px] leading-snug text-[var(--color-muted-foreground)]">
            {description}
          </p>
        </div>
      </div>
    </Link>
  );
}

function formatRate(
  value: number | null | undefined,
  t: (key: string, fallback?: string) => string,
) {
  if (value === null || value === undefined) {
    return t("dashboard.na");
  }
  return `${(value * 100).toFixed(1)}%`;
}
