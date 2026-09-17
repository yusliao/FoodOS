import { useRef, useState, type FormEvent } from "react";
import { Link, useParams } from "react-router-dom";
import { useIsMutating, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { MessageSquare } from "lucide-react";
import { addComment, getComments, getTicket, searchTickets, type Ticket } from "@/api/tickets";
import { useAuth } from "@/auth/use-auth";
import { TicketsPermissions } from "@/lib/permissions";
import { useLocale } from "@/i18n/locale-provider";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { EntityPageHeader, ErrorBand, Field, LoadingRow, Pagination } from "@/components/list";
import { describe } from "@/pages/customers/request-error";
import { TicketActions } from "./actions";
import { TicketEditor } from "./editor";
import { DeleteTicketButton } from "./trash";
import { TicketAttachments } from "./attachments";

export function TicketsPage() {
  const { t } = useLocale();
  const { user } = useAuth();
  const allowed = !!user?.permissions.includes(TicketsPermissions.View);
  const [draft, setDraft] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const query = useQuery({ queryKey: ["tickets", "list", search, page], queryFn: ({ signal }) => searchTickets(search, page, signal), enabled: allowed });
  function submit(event: FormEvent) { event.preventDefault(); setSearch(draft.trim()); setPage(1); }
  return <div className="space-y-6">
    <EntityPageHeader icon={MessageSquare} title={t("tickets.title")} description={t("tickets.description")}><TicketEditor /></EntityPageHeader>
    <form onSubmit={submit} className="flex max-w-xl gap-2"><Input aria-label={t("tickets.search")} value={draft} onChange={event => setDraft(event.target.value)} /><Button type="submit" disabled={!allowed}>{t("tickets.search")}</Button></form>
    {query.isPending && <LoadingRow label={t("common.loading")} />}
    {query.isError && <Failure error={query.error} retry={() => { if (allowed) void query.refetch(); }} pending={query.isFetching} />}
    {query.isSuccess && <>
      {query.data.items.length === 0 && <p role="status">{t("tickets.empty")}</p>}
      <div className="grid gap-4 md:grid-cols-2">{query.data.items.map(ticket => <article className="min-w-0 space-y-2 rounded-xl border p-4" key={ticket.id}>
        <p className="break-all text-sm">{ticket.number} · {t(`tickets.status.${ticket.status}`)}</p>
        <h2 className="break-words font-semibold"><Link to={`/tickets/${ticket.id}`} className="underline">{ticket.title}</Link></h2>
        <p className="break-all text-sm">{t("tickets.customer")}: {ticket.customerTenantId ?? "—"}</p>
      </article>)}</div>
      <Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} shown={query.data.items.length} hasPrev={query.data.hasPrevious} hasNext={query.data.hasNext} fetching={query.isFetching} onPrev={() => setPage(value => value - 1)} onNext={() => setPage(value => value + 1)} />
    </>}
  </div>;
}
export function TicketDetailPage() {
  const { ticketId = "" } = useParams();
  const { t } = useLocale();
  const { user } = useAuth();
  const allowed = !!user?.permissions.includes(TicketsPermissions.View);
  const valid = /^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(ticketId);
  const query = useQuery({ queryKey: ["tickets", "detail", ticketId], queryFn: ({ signal }) => getTicket(ticketId, signal), enabled: allowed && valid });
  return <div className="space-y-6">
    <Link to="/tickets" className="underline">{t("tickets.back")}</Link>
    {!valid ? <ErrorBand message={t("tickets.invalidId")} /> : <>
      {query.isPending && <LoadingRow label={t("common.loading")} />}
      {query.isError && <Failure error={query.error} retry={() => { if (allowed) void query.refetch(); }} pending={query.isFetching} />}
      {query.isSuccess && <TicketContent key={query.data.id} ticket={query.data} />}
    </>}
  </div>;
}
function TicketContent({ ticket }: { ticket: Ticket }) {
  const { t, culture } = useLocale();
  const { user } = useAuth();
  const cache = useQueryClient();
  const canView = !!user?.permissions.includes(TicketsPermissions.View);
  const canReply = canView && !!user?.permissions.includes(TicketsPermissions.Comment) && ticket.status !== "Closed";
  const comments = useQuery({ queryKey: ["tickets", "comments", ticket.id], queryFn: ({ signal }) => getComments(ticket.id, signal), enabled: canView });
  const [body, setBody] = useState("");
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const mutation = useMutation({ mutationKey: ["tickets", "write", ticket.id], mutationFn: addComment, onSuccess: async () => { await cache.invalidateQueries({ queryKey: ["tickets"] }); setBody(""); attempt.current = null; } });
  const busy = useIsMutating({ mutationKey: ["tickets", "write", ticket.id] }) > 0;
  function submit(event: FormEvent) {
    event.preventDefault();
    const text = body.trim();
    if (!canReply || busy || !text || text.length > 8192) return;
    if (attempt.current?.body !== text) attempt.current = { body: text, key: crypto.randomUUID() };
    mutation.mutate({ ticketId: ticket.id, body: text, key: attempt.current.key });
  }
  return <>
    <EntityPageHeader icon={MessageSquare} title={ticket.title} description={ticket.number} />
    <TicketActions ticket={ticket} />
    <TicketEditor ticket={ticket} />
    <DeleteTicketButton ticket={ticket} />
    <dl className="grid gap-4 rounded-xl border p-4 sm:grid-cols-2">
      {[[t("tickets.customer"), ticket.customerTenantId ?? "—"], [t("tickets.state"), t(`tickets.status.${ticket.status}`)], [t("tickets.priorityLabel"), t(`tickets.priority.${ticket.priority}`)], [t("tickets.reporter"), ticket.reporterUserId], [t("tickets.assignee"), ticket.assignedToUserId ?? "—"]].map(([label, value]) => <div key={label} className="min-w-0"><dt className="text-sm text-[var(--color-muted-foreground)]">{label}</dt><dd className="break-all">{value}</dd></div>)}
    </dl>
    <p className="whitespace-pre-wrap break-words">{ticket.description || t("tickets.noDescription")}</p>
    {ticket.resolutionNote && <section><h2 className="font-semibold">{t("tickets.resolution")}</h2><p className="whitespace-pre-wrap break-words">{ticket.resolutionNote}</p></section>}
    <TicketAttachments ticketId={ticket.id} />
    <section className="space-y-4" aria-label={t("tickets.comments")}>
      <h2 className="font-semibold">{t("tickets.comments")}</h2>
      {comments.isPending && <LoadingRow label={t("common.loading")} />}
      {comments.isError && <Failure error={comments.error} retry={() => { if (canView) void comments.refetch(); }} pending={comments.isFetching} />}
      {comments.isSuccess && (comments.data.length === 0 ? <p role="status">{t("tickets.noComments")}</p> : comments.data.map(comment => <article key={comment.id} className="space-y-2 rounded-xl border p-4"><p className="break-all text-sm">{comment.authorUserId} · {new Intl.DateTimeFormat(culture, { dateStyle: "medium", timeStyle: "short" }).format(new Date(comment.createdAtUtc))}</p><p className="whitespace-pre-wrap break-words">{comment.body}</p></article>))}
    </section>
    {canReply && <form onSubmit={submit} className="space-y-3"><Field id="ticket-reply" label={t("tickets.reply")} required><textarea id="ticket-reply" required maxLength={8192} value={body} disabled={busy} onChange={event => setBody(event.target.value)} className="min-h-28 w-full rounded-lg border bg-transparent p-3" /></Field>
      {mutation.isError && <ErrorBand message={describe(mutation.error, t("tickets.failed"))} />}
      <Button type="submit" disabled={!body.trim() || busy}>{t(busy ? "common.working" : "tickets.send")}</Button>
    </form>}
  </>;
}
function Failure({ error, retry, pending }: { error: unknown; retry: () => void; pending: boolean }) {
  const { t } = useLocale();
  return <div className="space-y-2"><ErrorBand message={describe(error, t("tickets.failed"))} /><Button variant="outline" onClick={retry} disabled={pending}>{t("workbench.retry")}</Button></div>;
}
