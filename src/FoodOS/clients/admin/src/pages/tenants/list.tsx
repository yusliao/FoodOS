import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Building2, ChevronLeft, ChevronRight, Plus } from "lucide-react";
import { listTenants, type TenantDto } from "@/api/tenants";
import { Button } from "@/components/ui/button";
import { Monogram } from "@/components/monogram";
import { EntityPageHeader, ErrorBand } from "@/components/list";
import { ApiRequestError } from "@/lib/api-client";
import { cn } from "@/lib/cn";
import { CreateTenantDialog } from "@/components/tenants/create-tenant-dialog";
import { useT } from "@/i18n/locale-provider";
import { useAuth } from "@/auth/use-auth";
import { MultitenancyPermissions } from "@/lib/permissions";

const PAGE_SIZE = 12;

// Desktop grid template — shared by header + rows.
const DESKTOP_COLS = "grid-cols-[1fr_140px_24px] lg:grid-cols-[1.6fr_1.4fr_140px_24px]";

function formatDate(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString();
}

export function TenantsListPage() {
  const t = useT();
  const [pageNumber, setPageNumber] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);
  const navigate = useNavigate();
  const { user: currentUser } = useAuth();
  const canCreateTenant = (currentUser?.permissions ?? []).includes(
    MultitenancyPermissions.Tenants.Create,
  );

  const query = useQuery({
    queryKey: ["tenants", { pageNumber, pageSize: PAGE_SIZE }],
    queryFn: ({ signal }) => listTenants({ pageNumber, pageSize: PAGE_SIZE }, signal),
    enabled: !!currentUser?.permissions.includes(MultitenancyPermissions.Tenants.View),
  });

  const data = query.isError ? undefined : query.data;
  const items: TenantDto[] = data?.items ?? [];

  const pageBadge = useMemo(() => {
    if (!data) return "—";
    const p = String(data.pageNumber).padStart(2, "0");
    const tp = String(Math.max(data.totalPages, 1)).padStart(2, "0");
    return t("tenants.pageOf").replace("{p}", p).replace("{t}", tp);
  }, [data, t]);

  return (
    <div className="space-y-4 sm:space-y-6">
      <EntityPageHeader
        icon={Building2}
        title={t("tenants.registry")}
        tone="info"
        total={data?.totalCount ?? null}
        unit={t("tenants.unit")}
        description={
          data
            ? t(data.totalCount === 1 ? "tenants.countOne" : "tenants.countMany").replace(
                "{n}",
                String(data.totalCount),
              )
            : t("tenants.loadingRegistry")
        }
      >
        {canCreateTenant && (
          <Button
            onClick={() => setCreateOpen(true)}
            className="h-9 flex-1 gap-1.5 rounded-lg px-4 text-[13px] font-semibold sm:flex-none"
          >
            <Plus className="size-4" /> {t("tenants.newTenant")}
          </Button>
        )}
      </EntityPageHeader>

      {query.isError && (
        <div className="space-y-2">
        <ErrorBand
          message={
            query.error instanceof ApiRequestError
              ? query.error.problem?.detail ?? query.error.message
              : t("tenants.loadFailed")
          }
        />
        <Button variant="outline" disabled={query.isFetching} onClick={() => query.refetch()}>{t("workbench.retry")}</Button>
        </div>
      )}

      {query.isLoading && items.length === 0 && (
        <div
          role="status"
          className="py-12 text-center font-mono text-sm uppercase tracking-[0.18em] text-[var(--color-muted-foreground)]"
        >
          {t("tenants.loading")}
        </div>
      )}

      {!query.isLoading && items.length === 0 && !query.isError && (
        <div className="py-16 text-center">
          <p className="font-display text-2xl text-[var(--color-foreground)]">{t("tenants.emptyTitle")}</p>
          <p className="mt-1 text-sm text-[var(--color-muted-foreground)]">
            {t("tenants.emptyBody")}
          </p>
        </div>
      )}

      {items.length > 0 && (
        <div>
          <p className="mb-3 text-[12px] font-medium text-[var(--color-muted-foreground)]">
            {(data?.totalCount ?? 0) === 1
              ? t("tenants.registeredCountOne").replace("{n}", String(data?.totalCount ?? 0))
              : t("tenants.registeredCount").replace("{n}", String(data?.totalCount ?? 0))}
          </p>

          {/* Mobile card list */}
          <div className="space-y-2 md:hidden">
            {items.map((tenant, i) => (
              <TenantMobileCard
                key={tenant.id ?? i}
                tenant={tenant}
                onClick={() => navigate(`/tenants/${tenant.id}`)}
              />
            ))}
          </div>

          {/* Desktop table */}
          <div className="hidden overflow-hidden rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] shadow-xs md:block">
            <div
              className={`grid items-center gap-3 border-b border-[var(--color-border)] bg-[var(--color-muted)]/40 px-4 py-2.5 ${DESKTOP_COLS}`}
            >
              <span className="text-[11.5px] font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)]">
                {t("tenants.colTenant")}
              </span>
              <span className="hidden text-[11.5px] font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)] lg:block">
                {t("tenants.colAdminEmail")}
              </span>
              <span className="text-[11.5px] font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)]">
                {t("tenants.colStatus")}
              </span>
              <span />
            </div>

            <ol className="divide-y divide-[var(--color-border)]">
              {items.map((tenant, i) => (
                <TenantDesktopRow
                  key={tenant.id ?? i}
                  tenant={tenant}
                  onClick={() => navigate(`/tenants/${tenant.id}`)}
                />
              ))}
            </ol>
          </div>
        </div>
      )}

      {/* Pagination */}
      {data && data.totalPages > 1 && (
        <div className="flex items-center justify-between text-xs">
          <span className="font-mono uppercase tracking-[0.18em] text-[var(--color-muted-foreground)]">
            {pageBadge}
          </span>
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={!data.hasPrevious || query.isFetching}
              onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
              className="h-9 rounded-lg px-3 text-[13px]"
            >
              <ChevronLeft className="mr-1 h-3.5 w-3.5" /> {t("common.previous")}
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={!data.hasNext || query.isFetching}
              onClick={() => setPageNumber((p) => p + 1)}
              className="h-9 rounded-lg px-3 text-[13px]"
            >
              {t("common.next")} <ChevronRight className="ml-1 h-3.5 w-3.5" />
            </Button>
          </div>
        </div>
      )}

      {canCreateTenant && <CreateTenantDialog open={createOpen} onOpenChange={setCreateOpen} />}
    </div>
  );
}

// ─── Status pill ─────────────────────────────────────────────────────────

function StatusPill({ active }: { active: boolean }) {
  const t = useT();
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-full px-1.5 py-0.5 text-[10.5px] font-medium",
        active
          ? "bg-[oklch(from_var(--color-success)_l_c_h_/_0.12)] text-[var(--color-success)]"
          : "bg-[var(--color-muted)] text-[var(--color-muted-foreground)]",
      )}
    >
      {active ? t("chrome.active") : t("tenants.inactive")}
    </span>
  );
}

// ─── Mobile card ───────────────────────────────────────────────────────────

function TenantMobileCard({ tenant, onClick }: { tenant: TenantDto; onClick: () => void }) {
  const t = useT();
  return (
    <li className="list-none">
      <button
        type="button"
        onClick={onClick}
        aria-label={t("tenants.openTenant").replace("{name}", tenant.name)}
        className={cn(
          "group w-full overflow-hidden rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-4 text-left shadow-xs",
          "transition-colors hover:border-[var(--color-border-strong)] hover:bg-[var(--color-accent)]",
          "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-ring)]",
          !tenant.isActive && "opacity-75",
        )}
      >
        <div className="flex items-center justify-between">
          <div className="flex min-w-0 items-center gap-3">
            <Monogram seed={tenant.id} fallback={tenant.name} size="md" />
            <div className="min-w-0">
              <p className="truncate text-[14px] font-medium text-[var(--color-foreground)]">
                {tenant.name}
              </p>
              <p className="mt-0.5 truncate font-mono text-[11px] text-[var(--color-muted-foreground)]">
                {tenant.id}
              </p>
            </div>
          </div>
          <ChevronRight className="size-4 shrink-0 text-[var(--color-border)] transition-colors group-hover:text-[var(--color-muted-foreground)]" />
        </div>
        <div className="mt-2 ml-[52px] flex flex-wrap items-center gap-2">
          <StatusPill active={tenant.isActive} />
          <span className="truncate font-mono text-[11px] text-[var(--color-muted-foreground)]">
            {tenant.adminEmail}
          </span>
        </div>
      </button>
    </li>
  );
}

// ─── Desktop row ────────────────────────────────────────────────────────────

function TenantDesktopRow({ tenant, onClick }: { tenant: TenantDto; onClick: () => void }) {
  const t = useT();
  return (
    <li className="list-none">
      <button
        type="button"
        onClick={onClick}
        className={cn(
          `group grid w-full items-center gap-3 px-4 py-3.5 text-left transition-colors hover:bg-[var(--color-accent)] focus-visible:bg-[var(--color-accent)] focus-visible:outline-none ${DESKTOP_COLS}`,
          !tenant.isActive && "opacity-75",
        )}
      >
        {/* Name + id */}
        <div className="flex min-w-0 items-center gap-3">
          <Monogram seed={tenant.id} fallback={tenant.name} size="md" />
          <div className="min-w-0">
            <span className="block truncate text-[14px] font-medium text-[var(--color-foreground)] transition-colors group-hover:text-[var(--color-primary)]">
              {tenant.name}
            </span>
            <span className="block truncate font-mono text-[12px] text-[var(--color-muted-foreground)]">
              {tenant.id} · {t("tenants.validUntilShort").replace("{date}", formatDate(tenant.validUpto))}
            </span>
          </div>
        </div>

        {/* Admin email (lg+) */}
        <code className="hidden truncate font-mono text-[12px] text-[var(--color-muted-foreground)] lg:block">
          {tenant.adminEmail}
        </code>

        {/* Status */}
        <div className="flex items-center">
          <StatusPill active={tenant.isActive} />
        </div>

        <div className="flex items-center justify-end">
          <ChevronRight className="size-4 text-[var(--color-border)] transition-colors group-hover:text-[var(--color-muted-foreground)]" />
        </div>
      </button>
    </li>
  );
}
