import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { searchRoles, type RoleDto } from "@/api/roles";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ErrorBand, LoadingRow, Pagination } from "@/components/list";
import { useT } from "@/i18n/locale-provider";
import { describe } from "@/pages/customers/request-error";

/** Mounted only for users with Roles.View. Selection survives search and paging. */
export function RoleFilter({ value, onChange }: { value: string; onChange: (id: string) => void }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [term, setTerm] = useState("");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<RoleDto | null>(null);
  useEffect(() => {
    if (search.trim() === term) return;
    const timer = setTimeout(() => { setTerm(search.trim()); setPage(1); }, 200);
    return () => clearTimeout(timer);
  }, [search, term]);
  const query = useQuery({
    queryKey: ["roles", "filter", page, term],
    queryFn: ({ signal }) => searchRoles({ pageNumber: page, pageSize: 10, search: term }, signal),
    enabled: open,
  });
  return <section className="w-full space-y-3 rounded-lg border p-3" aria-label={t("users.role")}>
    <div className="flex flex-wrap items-center gap-2">
      <Button variant="outline" aria-expanded={open} onClick={() => setOpen(value => !value)}>{t("users.role")}: {value ? selected?.name ?? value : t("users.anyRole")}</Button>
      {value && <Button variant="ghost" onClick={() => { onChange(""); setSelected(null); }}>{t("users.anyRole")}</Button>}
    </div>
    {open && <>
      <Input aria-label={t("roles.searchAria")} placeholder={t("roles.searchPlaceholder")} value={search} onChange={event => setSearch(event.target.value)} />
      {query.isPending && <LoadingRow label={t("roles.loading")} />}
      {query.isError && <><ErrorBand message={describe(query.error, t("roles.loadFailed"))} /><Button variant="outline" onClick={() => void query.refetch()} disabled={query.isFetching}>{t("workbench.retry")}</Button></>}
      {query.isSuccess && <>
        {query.data.items.length === 0 && <p role="status">{t("roles.noneFound")}</p>}
        <div className="grid gap-2 sm:grid-cols-2">{query.data.items.map(role => <Button key={role.id} variant="outline" className="h-auto justify-start whitespace-normal break-all text-left" aria-pressed={value === role.id} onClick={() => { setSelected(role); onChange(role.id); }}>{role.name}</Button>)}</div>
        <Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} shown={query.data.items.length} hasPrev={page > 1} hasNext={query.data.hasNext} fetching={query.isFetching} onPrev={() => setPage(value => Math.max(1, value - 1))} onNext={() => setPage(value => value + 1)} />
      </>}
    </>}
  </section>;
}
