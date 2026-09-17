import { useRef, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useIsMutating, useMutation, useQueryClient } from "@tanstack/react-query";
import { saveTicket, type Ticket } from "@/api/tickets";
import { useAuth } from "@/auth/use-auth";
import { TicketsPermissions } from "@/lib/permissions";
import { useT } from "@/i18n/locale-provider";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Field } from "@/components/list";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { describe } from "@/pages/customers/request-error";

export function TicketEditor({ ticket }: { ticket?: Ticket }) {
  const t = useT();
  const { user } = useAuth();
  const [open, setOpen] = useState(false);
  const busy = useIsMutating({ mutationKey: ["tickets", "write", ticket?.id ?? "new"] }) > 0;
  const allowed = user?.tenant === "root" && user.permissions.includes(TicketsPermissions.View) && user.permissions.includes(ticket ? TicketsPermissions.Update : TicketsPermissions.Create) && ticket?.status !== "Closed";
  if (!allowed) return null;
  return <>
    <Button variant={ticket ? "outline" : "default"} disabled={busy} onClick={() => setOpen(true)}>{t(ticket ? "tickets.edit" : "tickets.new")}</Button>
    {open && <EditorDialog ticket={ticket} onClose={() => setOpen(false)} />}
  </>;
}
function EditorDialog({ ticket, onClose }: { ticket?: Ticket; onClose: () => void }) {
  const t = useT();
  const { user } = useAuth();
  const cache = useQueryClient();
  const navigate = useNavigate();
  const [title, setTitle] = useState(ticket?.title ?? "");
  const [description, setDescription] = useState(ticket?.description ?? "");
  const [priority, setPriority] = useState<Ticket["priority"]>(ticket?.priority ?? "Medium");
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const allowed = user?.tenant === "root" && user.permissions.includes(TicketsPermissions.View) && user.permissions.includes(ticket ? TicketsPermissions.Update : TicketsPermissions.Create) && ticket?.status !== "Closed";
  const mutation = useMutation({ mutationKey: ["tickets", "write", ticket?.id ?? "new"], mutationFn: saveTicket, onSuccess: async id => { await cache.invalidateQueries({ queryKey: ["tickets"] }); onClose(); if (!ticket) navigate(`/tickets/${encodeURIComponent(id)}`); } });
  const busy = useIsMutating({ mutationKey: ["tickets", "write", ticket?.id ?? "new"] }) > 0;
  const valid = !!title.trim() && title.trim().length <= 160 && description.length <= 4096;
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!allowed || !valid || busy) return;
    const payload = { ticketId: ticket?.id, title: title.trim(), description: description.trim() || null, priority };
    const body = JSON.stringify(payload);
    if (attempt.current?.body !== body) attempt.current = { body, key: crypto.randomUUID() };
    mutation.mutate({ ...payload, key: attempt.current.key });
  }
  return <Dialog open onOpenChange={open => { if (!open && !busy) onClose(); }}><DialogContent>
    <DialogHeader><DialogTitle>{t(ticket ? "tickets.edit" : "tickets.new")}</DialogTitle><DialogDescription>{ticket ? `${ticket.number} · ${ticket.customerTenantId ?? "—"}` : t("tickets.rootCreation")}</DialogDescription></DialogHeader>
    <form onSubmit={submit}><DialogBody className="space-y-4"><fieldset disabled={busy} className="space-y-3">
      <Field id="ticket-title" label={t("tickets.subject")} required><Input id="ticket-title" required maxLength={160} value={title} onChange={event => setTitle(event.target.value)} /></Field>
      <Field id="ticket-description" label={t("tickets.details")}><textarea id="ticket-description" maxLength={4096} className="min-h-28 w-full rounded-lg border bg-transparent p-3" value={description} onChange={event => setDescription(event.target.value)} /></Field>
      <Field id="ticket-priority" label={t("tickets.priorityLabel")}><select id="ticket-priority" className="h-10 w-full rounded-lg border bg-[var(--color-card)] px-3" value={priority} onChange={event => setPriority(event.target.value as Ticket["priority"])}>{(["Low", "Medium", "High", "Critical"] as const).map(value => <option key={value} value={value}>{t(`tickets.priority.${value}`)}</option>)}</select></Field>
    </fieldset>{mutation.isError && <p role="alert">{describe(mutation.error, t("tickets.failed"))}</p>}</DialogBody><DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={onClose}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!valid || !allowed || busy}>{t(busy ? "common.working" : "tickets.save")}</Button></DialogFooter></form>
  </DialogContent></Dialog>;
}
