import { useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useIsMutating, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Trash2 } from "lucide-react";
import { deleteTicket, getTicketTrash, restoreTicket, type Ticket } from "@/api/tickets";
import { useAuth } from "@/auth/use-auth";
import { TicketsPermissions } from "@/lib/permissions";
import { useT } from "@/i18n/locale-provider";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { EntityPageHeader, ErrorBand, LoadingRow, Pagination } from "@/components/list";
import { describe } from "@/pages/customers/request-error";

export function DeleteTicketButton({ ticket }: { ticket: Ticket }) {
  const t = useT();
  const { user } = useAuth();
  const navigate = useNavigate();
  const cache = useQueryClient();
  const [open, setOpen] = useState(false);
  const allowed = !!user?.permissions.includes(TicketsPermissions.Delete) && !!user?.permissions.includes(TicketsPermissions.View);
  const mutation = useMutation({ mutationKey: ["tickets", "write", ticket.id], mutationFn: deleteTicket, onSuccess: async () => {
    navigate("/tickets", { replace: true });
    cache.removeQueries({ queryKey: ["tickets", "detail", ticket.id], exact: true });
    cache.removeQueries({ queryKey: ["tickets", "comments", ticket.id], exact: true });
    await cache.invalidateQueries({ queryKey: ["tickets"] });
    setOpen(false);
  } });
  const busy = useIsMutating({ mutationKey: ["tickets", "write", ticket.id] }) > 0;
  if (!allowed) return null;
  return <>
    <Button variant="destructive" disabled={busy} onClick={() => setOpen(true)}>{t("tickets.delete")}</Button>
    <ConfirmDialog open={open} title={t("tickets.delete")} description={<>{t("tickets.deleteHint")} {ticket.number}{mutation.isError && <span role="alert" className="mt-3 block">{describe(mutation.error, t("tickets.failed"))}</span>}</>} confirmLabel={t("tickets.delete")} destructive pending={busy} onOpenChange={value => { if (!value && !busy) { setOpen(false); mutation.reset(); } }} onConfirm={() => { if (allowed && !busy) mutation.mutate(ticket.id); }} />
  </>;
}
export function TicketTrashPage() {
  const t = useT();
  const { user } = useAuth();
  const cache = useQueryClient();
  const allowed = !!user?.permissions.includes(TicketsPermissions.Restore);
  const [page, setPage] = useState(1);
  const [target, setTarget] = useState<Ticket | null>(null);
  const key = useRef("");
  const query = useQuery({ queryKey: ["tickets", "trash", page], queryFn: ({ signal }) => getTicketTrash(page, signal), enabled: allowed });
  const mutation = useMutation({ mutationKey: ["tickets", "restore"], mutationFn: restoreTicket, onSuccess: async () => { await cache.invalidateQueries({ queryKey: ["tickets"] }); setTarget(null); } });
  return <div className="space-y-6">
    <EntityPageHeader icon={Trash2} title={t("tickets.trash")} description={t("tickets.trashHint")} />
    {user?.permissions.includes(TicketsPermissions.View) && <Link to="/tickets" className="underline">{t("tickets.back")}</Link>}
    {query.isPending && <LoadingRow label={t("common.loading")} />}
    {query.isError && <div><ErrorBand message={describe(query.error, t("tickets.failed"))} /><Button variant="outline" onClick={() => { if (allowed) void query.refetch(); }} disabled={query.isFetching}>{t("workbench.retry")}</Button></div>}
    {query.isSuccess && <>
      {query.data.items.length === 0 && <p role="status">{t("tickets.trashEmpty")}</p>}
      <div className="grid gap-4 md:grid-cols-2">{query.data.items.map(ticket => <article key={ticket.id} className="min-w-0 space-y-2 rounded-xl border p-4"><h2 className="break-words font-semibold">{ticket.title}</h2><p className="break-all">{ticket.number} · {ticket.customerTenantId ?? "—"}</p><Button variant="outline" disabled={!allowed || mutation.isPending} onClick={() => { mutation.reset(); key.current = crypto.randomUUID(); setTarget(ticket); }}>{t("tickets.restore")}</Button></article>)}</div>
      <Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} shown={query.data.items.length} hasPrev={query.data.hasPrevious} hasNext={query.data.hasNext} fetching={query.isFetching || mutation.isPending} onPrev={() => setPage(value => value - 1)} onNext={() => setPage(value => value + 1)} />
    </>}
    <ConfirmDialog open={!!target && allowed} title={t("tickets.restore")} description={<>{target?.number} · {target?.title}{mutation.isError && <span role="alert" className="mt-3 block">{describe(mutation.error, t("tickets.failed"))}</span>}</>} confirmLabel={t("tickets.restore")} pending={mutation.isPending} onOpenChange={open => { if (!open && !mutation.isPending) { setTarget(null); mutation.reset(); } }} onConfirm={() => { if (allowed && target && !mutation.isPending) mutation.mutate({ id: target.id, key: key.current }); }} />
  </div>;
}
