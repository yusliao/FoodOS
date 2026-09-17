import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { ChevronRight, Plus, Shield, ShieldCheck } from "lucide-react";
import { searchRoles, type RoleDto } from "@/api/roles";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { EntityPageHeader, ErrorBand, LoadingRow, Pagination } from "@/components/list";
import { EmptyState } from "@/components/empty-state";
import { ApiRequestError } from "@/lib/api-client";
import { CreateRoleDialog } from "@/components/roles/create-role-dialog";
import { useT } from "@/i18n/locale-provider";
import { useAuth } from "@/auth/use-auth";
import { IdentityPermissions } from "@/lib/permissions";

const ROOT_ROLE_NAMES = new Set(["Admin", "Basic"]);

// Desktop grid template — shared by header + rows.
const DESKTOP_COLS =
  "grid-cols-[1fr_120px_24px] lg:grid-cols-[1.4fr_2fr_120px_24px]";

export function RolesListPage() {
  const t = useT();
  const { user } = useAuth();
  const canView = !!user?.permissions.includes(IdentityPermissions.Roles.View);
  const canCreate = canView && !!user?.permissions.includes(IdentityPermissions.Roles.Create);
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const [debounced, setDebounced] = useState("");
  const [createOpen, setCreateOpen] = useState(false);
  const [page, setPage] = useState(1);

  useEffect(() => {
    if (search.trim() === debounced) return;
    const timer = setTimeout(() => { setDebounced(search.trim()); setPage(1); }, 200);
    return () => clearTimeout(timer);
  }, [search, debounced]);

  const query = useQuery({
    queryKey: ["roles", "list", page, debounced],
    queryFn: ({ signal }) => searchRoles({ pageNumber: page, pageSize: 20, search: debounced }, signal),
    enabled: canView,
  });
  // Preserve the server's stable name/id ordering across page boundaries.
  const filtered = query.data?.items ?? [];

  const searchActive = debounced.length > 0;

  return (
    <div className="space-y-4 sm:space-y-6">
      <EntityPageHeader
        icon={Shield}
        title={t("roles.title")}
        total={query.data?.totalCount ?? null}
        unit={t("roles.unit")}
        description={t("roles.description")}
      >
        {canCreate && <Button
          onClick={() => setCreateOpen(true)}
          className="h-9 flex-1 gap-1.5 rounded-lg px-4 text-[13px] font-semibold sm:flex-none"
        >
          <Plus className="size-4" />
          {t("roles.newRole")}
        </Button>}
      </EntityPageHeader>

      {/* Search */}
      <div className="relative w-full max-w-sm">
        <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[var(--color-muted-foreground)]">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
        </span>
        <input
          type="search"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t("roles.searchPlaceholder")}
          aria-label={t("roles.searchAria")}
          className="h-9 w-full rounded-md border border-[var(--color-input)] bg-transparent pl-9 pr-3 text-[13px] outline-none transition-colors placeholder:text-[oklch(from_var(--color-muted-foreground)_l_c_h_/_0.7)] focus-visible:border-[var(--color-ring)] focus-visible:ring-[3px] focus-visible:ring-[oklch(from_var(--color-ring)_l_c_h_/_0.5)]"
        />
      </div>

      {query.isError && (
        <div className="space-y-2">
        <ErrorBand
          message={
            query.error instanceof ApiRequestError
              ? query.error.problem?.detail ?? query.error.message
              : t("roles.loadFailed")
          }
        />
        <Button variant="outline" disabled={query.isFetching} onClick={() => void query.refetch()}>{t("workbench.retry")}</Button>
        </div>
      )}

      {query.isLoading && <LoadingRow label={t("roles.loading")} />}

      {!query.isLoading && filtered.length === 0 && !query.isError && (
        searchActive ? (
          <div className="py-16 text-center">
            <p className="font-display text-2xl">{t("roles.noneFound")}</p>
            <p className="mt-1 text-sm text-[var(--color-muted-foreground)]">
              {t("roles.noneMatch").replace("{q}", debounced)}
            </p>
            <Button
              variant="outline"
              className="mt-4 h-9 rounded-lg px-4 text-[13px]"
              onClick={() => setSearch("")}
            >
              {t("roles.clearSearch")}
            </Button>
          </div>
        ) : (
          <EmptyState
            icon={ShieldCheck}
            kicker={t("roles.emptyKicker")}
            title={t("roles.emptyTitle")}
            description={t("roles.emptyBody")}
            action={canCreate ? (
              <Button onClick={() => setCreateOpen(true)} className="h-9 rounded-lg px-4 text-[13px]">
                <Plus className="mr-1.5 h-4 w-4" /> {t("roles.newRole")}
              </Button>
            ) : undefined}
          />
        )
      )}

      {filtered.length > 0 && (
        <div>
          <p className="mb-3 text-[12px] font-medium text-[var(--color-muted-foreground)]">
            {t(query.data?.totalCount === 1 ? "roles.foundOne" : "roles.foundMany").replace(
              "{n}",
              String(query.data?.totalCount ?? 0),
            )}
          </p>

          {/* Mobile card list */}
          <div className="space-y-2 md:hidden">
            {filtered.map((role) => (
              <RoleMobileCard
                key={role.id}
                role={role}
                onClick={() => navigate(`/roles/${role.id}`)}
              />
            ))}
          </div>

          {/* Desktop table */}
          <div className="hidden overflow-hidden rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] shadow-xs md:block">
            {/* Table header */}
            <div
              className={`grid items-center gap-3 border-b border-[var(--color-border)] bg-[var(--color-muted)]/40 px-4 py-2.5 ${DESKTOP_COLS}`}
            >
              <span className="text-[11.5px] font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)]">
                {t("roles.colName")}
              </span>
              <span className="hidden text-[11.5px] font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)] lg:block">
                {t("roles.colDescription")}
              </span>
              <span className="text-[11.5px] font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)]">
                {t("roles.colPermissions")}
              </span>
              <span />
            </div>

            <ol className="divide-y divide-[var(--color-border)]">
              {filtered.map((role, i) => (
                <RoleDesktopRow
                  key={role.id}
                  role={role}
                  isLast={i === filtered.length - 1}
                  onClick={() => navigate(`/roles/${role.id}`)}
                />
              ))}
            </ol>
          </div>
        </div>
      )}

      {query.data && <Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} shown={filtered.length} hasPrev={page > 1} hasNext={query.data.hasNext} fetching={query.isFetching} onPrev={() => setPage(value => Math.max(1, value - 1))} onNext={() => setPage(value => value + 1)} />}
      {canCreate && <CreateRoleDialog open={createOpen} onOpenChange={setCreateOpen} />}
    </div>
  );
}

function permissionCountLabel(
  count: number,
  t: (key: string, fallback?: string) => string,
): string {
  return t(count === 1 ? "roles.permissionOne" : "roles.permissionMany").replace(
    "{n}",
    String(count),
  );
}

// ─── Mobile card ────────────────────────────────────────────────────────

function RoleMobileCard({
  role,
  onClick,
}: {
  role: RoleDto;
  onClick: () => void;
}) {
  const t = useT();
  const isSystem = ROOT_ROLE_NAMES.has(role.name);
  return (
    <li className="list-none">
      <button
        type="button"
        onClick={onClick}
        aria-label={t("roles.openRole").replace("{name}", role.name)}
        className="group w-full overflow-hidden rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-4 text-left shadow-xs transition-colors hover:border-[var(--color-border-strong)] hover:bg-[var(--color-accent)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-ring)]"
      >
        <div className="flex items-center justify-between">
          <div className="flex min-w-0 items-center gap-3">
            <span
              aria-hidden
              className="grid size-10 shrink-0 place-items-center rounded-lg"
              style={{
                backgroundColor: "oklch(from var(--color-primary) l c h / 0.10)",
                boxShadow: "inset 0 0 0 1px oklch(from var(--color-primary) l c h / 0.22)",
                color: "var(--color-primary)",
              }}
            >
              <Shield className="size-4" />
            </span>
            <div className="min-w-0">
              <div className="flex items-center gap-1.5">
                <p className="truncate text-[14px] font-medium text-[var(--color-foreground)]">
                  {role.name}
                </p>
                {isSystem && (
                  <Badge variant="outline" className="font-mono text-[10px] uppercase tracking-[0.14em]">
                    {t("roles.system")}
                  </Badge>
                )}
              </div>
              <p className="mt-0.5 truncate text-[11px] text-[var(--color-muted-foreground)]">
                {role.description ?? <span className="italic opacity-60">{t("roles.noDescription")}</span>}
              </p>
            </div>
          </div>
          <ChevronRight className="size-4 shrink-0 text-[var(--color-border)] transition-colors group-hover:text-[var(--color-muted-foreground)]" />
        </div>
        {role.permissions != null && (
          <div className="mt-2 ml-[52px]">
            <span className="inline-flex items-center rounded-full bg-[oklch(from_var(--color-info)_l_c_h_/_0.12)] px-2 py-0.5 text-[10.5px] font-medium text-[var(--color-info)]">
              {permissionCountLabel(role.permissions.length, t)}
            </span>
          </div>
        )}
      </button>
    </li>
  );
}

// ─── Desktop row ────────────────────────────────────────────────────────

function RoleDesktopRow({
  role,
  isLast,
  onClick,
}: {
  role: RoleDto;
  isLast: boolean;
  onClick: () => void;
}) {
  const t = useT();
  const isSystem = ROOT_ROLE_NAMES.has(role.name);
  const permLabel =
    role.permissions === undefined || role.permissions === null
      ? "—"
      : permissionCountLabel(role.permissions.length, t);

  return (
    <li className="list-none">
      <button
        type="button"
        onClick={onClick}
        className={`group grid w-full items-center gap-3 px-4 py-3.5 text-left transition-colors hover:bg-[var(--color-accent)] focus-visible:outline-none focus-visible:bg-[var(--color-accent)] ${DESKTOP_COLS} ${isLast ? "" : ""}`}
      >
        {/* Name */}
        <div className="flex min-w-0 items-center gap-3">
          <span
            aria-hidden
            className="grid size-9 shrink-0 place-items-center rounded-lg"
            style={{
              backgroundColor: "oklch(from var(--color-primary) l c h / 0.10)",
              boxShadow: "inset 0 0 0 1px oklch(from var(--color-primary) l c h / 0.22)",
              color: "var(--color-primary)",
            }}
          >
            <Shield className="size-4" />
          </span>
          <span className="truncate text-[14px] font-medium text-[var(--color-foreground)] transition-colors group-hover:text-[var(--color-primary)]">
            {role.name}
          </span>
          {isSystem && (
            <Badge variant="outline" className="shrink-0 font-mono text-[10px] uppercase tracking-[0.14em]">
              {t("roles.system")}
            </Badge>
          )}
        </div>

        {/* Description (lg+) */}
        <div className="hidden lg:block">
          <p className="truncate text-[12.5px] text-[var(--color-muted-foreground)]">
            {role.description ?? <span className="italic opacity-60">{t("roles.noDescription")}</span>}
          </p>
        </div>

        {/* Permission count */}
        <span className="font-mono text-[12px] tabular-nums text-[var(--color-muted-foreground)]">
          {permLabel}
        </span>

        <div className="flex items-center justify-end">
          <ChevronRight className="size-4 text-[var(--color-border)] transition-colors group-hover:text-[var(--color-muted-foreground)]" />
        </div>
      </button>
    </li>
  );
}
