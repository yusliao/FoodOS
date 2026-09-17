import { useRef, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { UserRound } from "lucide-react";
import { createDriver, searchDrivers } from "@/api/logistics";
import { searchUsers, type UserDto } from "@/api/users";
import { useAuth } from "@/auth/use-auth";
import { EntityPageHeader, ErrorBand, Field, LoadingRow, Pagination } from "@/components/list";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { useT } from "@/i18n/locale-provider";
import { IdentityPermissions, LogisticsPermissions } from "@/lib/permissions";
import { describe } from "@/pages/customers/request-error";

const key = ["logistics", "drivers"] as const;
function label(user: UserDto) { return [user.firstName, user.lastName].filter(Boolean).join(" ") || user.userName || user.id; }
export function DriversPage() {
  const t = useT();
  const { user } = useAuth();
  const [creating, setCreating] = useState(false);
  const canView = !!user?.permissions.includes(LogisticsPermissions.Drivers.View);
  const canCreate = canView && !!user?.permissions.includes(LogisticsPermissions.Drivers.Create);
  const canChoose = !!user?.permissions.includes(IdentityPermissions.Users.View);
  const query = useQuery({ queryKey: key, queryFn: ({ signal }) => searchDrivers(signal), enabled: canView });
  return <div className="space-y-6">
    <EntityPageHeader icon={UserRound} title={t("drivers.title")} description={t("drivers.description")}>
      {canCreate && <Button disabled={!canChoose} onClick={() => setCreating(true)}>{t("drivers.create")}</Button>}
    </EntityPageHeader>
    {canCreate && !canChoose && <p role="status">{t("drivers.lookupRequired")}</p>}
    {query.isPending && <LoadingRow label={t("drivers.loading")} />}
    {query.isError && <><ErrorBand message={describe(query.error, t("drivers.failed"))} /><Button variant="outline" onClick={() => void query.refetch()}>{t("workbench.retry")}</Button></>}
    {query.isSuccess && query.data.length === 0 && <p role="status">{t("drivers.empty")}</p>}
    {query.isSuccess && <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{query.data.map(driver => <article key={driver.id} className="min-w-0 space-y-3 rounded-xl border p-5">
      <h2 className="break-all font-semibold">{driver.phone}</h2><dl className="text-sm"><dt>{t("drivers.userId")}</dt><dd className="break-all">{driver.userId}</dd></dl>
    </article>)}</div>}
    {creating && canCreate && canChoose && <CreateDriverDialog onClose={() => setCreating(false)} />}
  </div>;
}

function CreateDriverDialog({ onClose }: { onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<UserDto | null>(null);
  const [phone, setPhone] = useState("");
  const users = useQuery({ queryKey: ["logistics", "driver-users", search, page], queryFn: () => searchUsers({ search, pageNumber: page, pageSize: 10, isActive: true }) });
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const mutation = useMutation({ mutationFn: createDriver, onSuccess: async () => { await cache.invalidateQueries({ queryKey: key }); onClose(); } });
  const valid = selected?.isActive && !!phone.trim();
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!valid || !selected || mutation.isPending) return;
    const body = { userId: selected.id, phone: phone.trim() };
    const serialized = JSON.stringify(body);
    if (attempt.current?.body !== serialized) attempt.current = { body: serialized, key: crypto.randomUUID() };
    mutation.mutate({ body, key: attempt.current.key });
  }
  return <Dialog open onOpenChange={open => !open && !mutation.isPending && onClose()}><DialogContent>
    <DialogHeader><DialogTitle>{t("drivers.create")}</DialogTitle><DialogDescription>{t("drivers.createHint")}</DialogDescription></DialogHeader>
    <form onSubmit={submit}><DialogBody className="space-y-4"><fieldset disabled={mutation.isPending} className="min-w-0 space-y-4">
      <Field id="driver-user-search" label={t("drivers.search")}><Input id="driver-user-search" value={search} onChange={event => { setSearch(event.target.value); setPage(1); }} /></Field>
      {selected && <p className="break-words text-sm">{t("drivers.selected")}: {label(selected)} · {selected.id}</p>}
      {users.isPending && <LoadingRow label={t("drivers.loading")} />}
      {users.isError && <><ErrorBand message={describe(users.error, t("drivers.failed"))} /><Button type="button" onClick={() => void users.refetch()}>{t("workbench.retry")}</Button></>}
      {users.isSuccess && <>
        {users.data.items.length === 0 && <p role="status">{t("drivers.noUsers")}</p>}
        <div className="max-h-48 space-y-2 overflow-y-auto">{users.data.items.map(user => <Button className="h-auto w-full justify-start whitespace-normal text-left break-words" type="button" key={user.id} variant="outline" disabled={!user.isActive} aria-pressed={selected?.id === user.id} onClick={() => setSelected(user)}>{label(user)} · {user.userName || user.id}</Button>)}</div>
        <Pagination page={page} totalPages={users.data.totalPages} totalCount={users.data.totalCount} shown={users.data.items.length} hasPrev={users.data.hasPrevious} hasNext={users.data.hasNext} fetching={users.isFetching} onPrev={() => setPage(value => value - 1)} onNext={() => setPage(value => value + 1)} />
      </>}
      <Field id="driver-phone" label={t("drivers.phone")} required><Input id="driver-phone" required maxLength={32} type="tel" value={phone} onChange={event => setPhone(event.target.value)} /></Field>
    </fieldset>{mutation.isError && <ErrorBand message={describe(mutation.error, t("drivers.failed"))} />}</DialogBody>
    <DialogFooter><Button type="button" variant="outline" onClick={onClose} disabled={mutation.isPending}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!valid || mutation.isPending}>{t("drivers.save")}</Button></DialogFooter></form>
  </DialogContent></Dialog>;
}
