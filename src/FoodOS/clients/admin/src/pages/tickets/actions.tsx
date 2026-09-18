import { useRef, useState, type FormEvent } from "react";
import { useIsMutating, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { actOnTicket, type Ticket, type TicketAction } from "@/api/tickets";
import { searchUsers } from "@/api/users";
import { useAuth } from "@/auth/use-auth";
import { IdentityPermissions, TicketsPermissions } from "@/lib/permissions";
import { useT } from "@/i18n/locale-provider";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Field } from "@/components/list";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { describe } from "@/pages/customers/request-error";
import { ApiRequestError } from "@/lib/api-client";

const grants = { assign: TicketsPermissions.Assign, resolve: TicketsPermissions.Resolve, reopen: TicketsPermissions.Reopen, close: TicketsPermissions.Close };
function stateAllows(ticket: Ticket, action: TicketAction) {
  return action === "close" ? ticket.status === "Resolved" : action === "reopen" ? ticket.status === "Resolved" || ticket.status === "Closed" : ticket.status === "Open" || ticket.status === "InProgress";
}
export function TicketActions({ ticket }: { ticket: Ticket }) {
  const t = useT();
  const { user } = useAuth();
  const [action, setAction] = useState<TicketAction | null>(null);
  const busy = useIsMutating({ mutationKey: ["tickets", "write", ticket.id] }) > 0;
  const allowed = (value: TicketAction) => user?.tenant === "root" && user.permissions.includes(TicketsPermissions.View) && user.permissions.includes(grants[value]) && stateAllows(ticket, value);
  return <div className="flex flex-wrap gap-2">
    {(Object.keys(grants) as TicketAction[]).filter(allowed).map(value => <Button key={value} variant="outline" disabled={busy} onClick={() => setAction(value)}>{t(`tickets.actions.${value}`)}</Button>)}
    {action && allowed(action) && <ActionDialog key={action} ticket={ticket} action={action} onClose={() => setAction(null)} />}
  </div>;
}
function ActionDialog({ ticket, action, onClose }: { ticket: Ticket; action: TicketAction; onClose: () => void }) {
  const t = useT();
  const { user } = useAuth();
  const cache = useQueryClient();
  const [assignee, setAssignee] = useState(ticket.assignedToUserId ?? "");
  const [note, setNote] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const canWrite = user?.tenant === "root" && user.permissions.includes(TicketsPermissions.View) && user.permissions.includes(grants[action]) && stateAllows(ticket, action);
  const canSearch = !!canWrite && action === "assign" && !!user?.permissions.includes(IdentityPermissions.Users.View);
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const mutation = useMutation({ mutationKey: ["tickets", "write", ticket.id], mutationFn: actOnTicket, onSuccess: async () => { await cache.invalidateQueries({ queryKey: ["tickets"] }); onClose(); }, onError: async error => {
    if (error instanceof ApiRequestError && error.status === 409) await cache.invalidateQueries({ queryKey: ["tickets", "detail", ticket.id], exact: true });
  } });
  const busy = useIsMutating({ mutationKey: ["tickets", "write", ticket.id] }) > 0;
  const users = useQuery({ queryKey: ["tickets", "assignees", search.trim(), page], queryFn: ({ signal }) => searchUsers({ search, pageNumber: page, pageSize: 20, isActive: true }, signal), enabled: canSearch && !busy });
  const choices = users.isSuccess ? users.data.items.filter(item => item.isActive) : [];
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!canWrite || busy) return;
    const payload = action === "assign" ? { assigneeUserId: assignee || null } : action === "resolve" ? { resolutionNote: note.trim() || null } : {};
    const body = JSON.stringify(payload);
    if (attempt.current?.body !== body) attempt.current = { body, key: crypto.randomUUID() };
    mutation.mutate({ ticketId: ticket.id, action, payload, key: attempt.current.key });
  }
  return <Dialog open onOpenChange={open => { if (!open && !busy) onClose(); }}><DialogContent>
    <DialogHeader><DialogTitle>{t(`tickets.actions.${action}`)}</DialogTitle><DialogDescription>{ticket.number} · {ticket.title}</DialogDescription></DialogHeader>
    <form onSubmit={submit}><DialogBody className="space-y-4"><fieldset className="space-y-3" disabled={busy}>
      {action === "assign" && <>
        <Field id="ticket-assignee" label={t("tickets.assignee")}>
          <select id="ticket-assignee" value={assignee} onChange={event => setAssignee(event.target.value)} className="h-10 w-full rounded-lg border bg-[var(--color-card)] px-3">
            <option value="">{t("tickets.unassigned")}</option>
            {assignee && !choices.some(item => item.id === assignee) && <option value={assignee}>{assignee}</option>}
            {choices.map(item => <option key={item.id} value={item.id}>{item.userName || item.email || item.id}</option>)}
          </select>
        </Field>
        {canSearch ? <>
          <Input aria-label={t("tickets.searchAssignees")} value={search} onChange={event => { setSearch(event.target.value); setPage(1); }} />
          {users.isFetching && <p role="status">{t("common.loading")}</p>}
          {users.isError && <div><p role="alert">{describe(users.error, t("tickets.failed"))}</p><Button type="button" variant="outline" disabled={busy || users.isFetching} onClick={() => { if (canSearch) void users.refetch(); }}>{t("workbench.retry")}</Button></div>}
          {users.isSuccess && choices.length === 0 && <p role="status">{t("common.emptyDefault")}</p>}
          <div className="flex gap-2"><Button type="button" variant="outline" disabled={page <= 1 || users.isFetching} onClick={() => setPage(value => value - 1)}>{t("common.previous")}</Button><Button type="button" variant="outline" disabled={!users.isSuccess || !users.data.hasNext || users.isFetching} onClick={() => setPage(value => value + 1)}>{t("common.next")}</Button></div>
        </> : <p>{t("tickets.assigneePermission")}</p>}
      </>}
      {action === "resolve" && <Field id="ticket-resolution" label={t("tickets.resolution")}><textarea id="ticket-resolution" className="min-h-28 w-full rounded-lg border bg-transparent p-3" value={note} onChange={event => setNote(event.target.value)} /></Field>}
      {(action === "close" || action === "reopen") && <p>{t("tickets.confirmState")}</p>}
    </fieldset>
      {mutation.isError && <p role="alert">{describe(mutation.error, t("tickets.failed"))}</p>}
    </DialogBody><DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={onClose}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!canWrite || busy || (action === "assign" && assignee === (ticket.assignedToUserId ?? ""))}>{t(busy ? "common.working" : "common.confirm")}</Button></DialogFooter></form>
  </DialogContent></Dialog>;
}
