import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { ArrowRight, LayoutDashboard } from "lucide-react";
import { getOpsKpis } from "@/api/ops";
import { Skeleton } from "@/components/ui/skeleton";
import { Button } from "@/components/ui/button";
import { EntityPageHeader, Stat, StatStrip } from "@/components/list";
import { sections, filterNavSpec, topNavBottom } from "@/components/layout/nav-items";
import { useAuth } from "@/auth/use-auth";
import { useT } from "@/i18n/locale-provider";
import { messageKeyForNavItem } from "@/i18n/locale-store";
import { OpsPermissions } from "@/lib/permissions";

export function DashboardPage() {
  const t = useT();
  const { user, permissionsHydrated, permissionsError, refreshPermissions } = useAuth();
  const granted = permissionsHydrated ? user?.permissions ?? [] : [];
  const canViewOps = granted.includes(OpsPermissions.Kpis.View);
  const kpis = useQuery({
    queryKey: ["ops", "kpis"],
    queryFn: () => getOpsKpis(),
    enabled: canViewOps,
  });
  const available = sections
    .map(section => ({ ...section, items: filterNavSpec(section.items, granted) }))
    .filter(section => section.items.length > 0);

  return (
    <div className="space-y-6">
      <EntityPageHeader icon={LayoutDashboard} title={t("workbench.title")}
        description={t("workbench.description")} tone="primary" />
      <p className="text-sm text-[var(--color-muted-foreground)]">
        {user?.name} · {t("workbench.identity")}
      </p>
      {permissionsError && (
        <div role="alert" className="space-y-2">
          <p>{t("workbench.permissionsFailed")}</p>
          <Button variant="outline" onClick={() => void refreshPermissions()}>{t("workbench.retry")}</Button>
        </div>
      )}
      {canViewOps && (
        <section className="space-y-3" aria-label={t("dashboard.operations")}>
          <h2 className="font-semibold">{t("dashboard.operations")}</h2>
          {kpis.isError ? (
            <div role="alert" className="space-y-2">
              <p>{t("workbench.loadFailed")}</p>
              <Button variant="outline" onClick={() => void kpis.refetch()}>{t("workbench.retry")}</Button>
            </div>
          ) : (
            <StatStrip cols={4}>
              {([
                ["fulfillment", kpis.data?.fulfillmentRate],
                ["stockout", kpis.data?.stockoutRate],
                ["shrinkage", kpis.data?.shrinkageRate],
                ["temperature", kpis.data?.temperatureComplianceRate],
              ] as const).map(([key, value]) => (
                <Stat key={key} label={t("dashboard." + key)}
                  value={kpis.isPending ? <Skeleton className="h-7 w-16" />
                    : value == null ? t("dashboard.na") : (value * 100).toFixed(1) + "%"} />
              ))}
            </StatStrip>
          )}
        </section>
      )}
      {!canViewOps && <p className="text-sm text-[var(--color-muted-foreground)]">{t("workbench.noReports")}</p>}
      {!permissionsError && available.length === 0 && <p role="status">{t("workbench.noModules")}</p>}
      {available.map(section => (
        <section key={section.id} className="space-y-3">
          <h2 className="font-semibold">{t("nav.sections." + section.id, section.caption)}</h2>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {section.items.map(item => (
              <Link key={item.to} to={item.to}
                className="flex items-center gap-3 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-4 hover:bg-[var(--color-accent)]">
                <item.icon className="size-5" aria-hidden />
                <span className="flex-1">{t(messageKeyForNavItem(item.to), item.label)}</span>
                <ArrowRight className="size-4" aria-hidden />
              </Link>
            ))}
          </div>
        </section>
      ))}
      <section className="space-y-2">
        <h2 className="font-semibold">{t("workbench.personal")}</h2>
        {topNavBottom.map(item => <Link key={item.to} to={item.to} className="underline">{t(messageKeyForNavItem(item.to), item.label)}</Link>)}
      </section>
    </div>
  );
}
