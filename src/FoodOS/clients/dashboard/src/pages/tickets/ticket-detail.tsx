import {
  useEffect,
  useState,
  type FormEvent,
} from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  AlertOctagon,
  AlertTriangle,
  CalendarDays,
  CheckCircle2,
  Clock,
  Info,
  MessageCircle,
  RefreshCw,
  RotateCcw,
  Send,
  Sparkles,
  Ticket as TicketIcon,
  User,
  UserCheck,
  UserX,
} from "lucide-react";
import { toast } from "sonner";
import { useAuth } from "@/auth/use-auth";
import {
  addTicketComment,
  assignTicket,
  getTicketById,
  listTicketComments,
  reopenTicket,
  resolveTicket,
  TICKET_PRIORITIES,
  type TicketDto,
} from "@/api/tickets";
import {
  PRIORITY_TONE,
  STATUS_TONE,
  ticketPriorityLabel,
  ticketStatusLabel,
} from "@/lib/ticket-enums";
import { UserPicker } from "@/components/identity/user-picker";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogBody,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import {
  EntityDetailAvatar,
  EntityDetailBack,
  EntityDetailHero,
  EntityDetailMeta,
  EntityDetailSection,
  EntityDetailStat,
  EntityStatusBadge,
  ErrorBand,
  Field,
} from "@/components/list";
import { cn } from "@/lib/cn";
import { useUserDisplay } from "@/lib/use-user-display";
import {
  describe,
  formatDate,
  formatRelative,
} from "@/lib/list-helpers";
import { useT } from "@/i18n/locale-provider";

type DialogState =
  | { mode: "closed" }
  | { mode: "resolve" }
  | { mode: "assign" };


// ───────────────────────────────────────────────────────────────────────
//  Page
// ───────────────────────────────────────────────────────────────────────

export function TicketDetailPage() {
  const t = useT();
  const { ticketId = "" } = useParams<{ ticketId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [dialog, setDialog] = useState<DialogState>({ mode: "closed" });

  const ticketQuery = useQuery({
    queryKey: ["tickets", "detail", ticketId],
    queryFn: () => getTicketById(ticketId),
    enabled: !!ticketId,
  });

  const commentsQuery = useQuery({
    queryKey: ["tickets", "comments", ticketId],
    queryFn: () => listTicketComments(ticketId),
    enabled: !!ticketId,
  });

  const ticket = ticketQuery.data;

  return (
    <div className="space-y-5 pb-12">
      <EntityDetailBack to="/tickets" label={t("tickets.back")} />

      {ticketQuery.isError && <ErrorBand message={describe(ticketQuery.error)} />}

      {ticketQuery.isLoading ? (
        <DetailSkeleton />
      ) : ticket ? (
        <>
          <Hero
            ticket={ticket}
            commentCount={commentsQuery.data?.length ?? ticket.commentCount ?? 0}
            isFetching={ticketQuery.isFetching}
            onRefresh={() => void ticketQuery.refetch()}
            onResolve={() => setDialog({ mode: "resolve" })}
            onReopen={() => {
              void (async () => {
                try {
                  await reopenTicket(ticket.id);
                  toast.success(t("tickets.reopened"));
                  await queryClient.invalidateQueries({ queryKey: ["tickets"] });
                } catch (e) {
                  toast.error(describe(e));
                }
              })();
            }}
            onAssign={() => setDialog({ mode: "assign" })}
          />

          <div className="grid grid-cols-1 gap-5 lg:grid-cols-[1fr_320px]">
            <div className="space-y-5">
              <DescriptionSection ticket={ticket} />
              <CommentsSection
                ticketId={ticket.id}
                comments={commentsQuery.data ?? []}
                isLoading={commentsQuery.isLoading}
                onPosted={() => {
                  void queryClient.invalidateQueries({ queryKey: ["tickets", "comments", ticket.id] });
                  void queryClient.invalidateQueries({ queryKey: ["tickets", "detail", ticket.id] });
                }}
                disabled={ticket.status === "Closed"}
              />
            </div>
            <PropertiesSection ticket={ticket} />
          </div>

          <ResolveDialog
            open={dialog.mode === "resolve"}
            ticket={ticket}
            onClose={() => setDialog({ mode: "closed" })}
          />
          <AssignDialog
            open={dialog.mode === "assign"}
            ticket={ticket}
            onClose={() => setDialog({ mode: "closed" })}
          />
        </>
      ) : (
        <NotFoundPanel onBack={() => navigate("/tickets")} />
      )}
    </div>
  );
}

// ───────────────────────────────────────────────────────────────────────
//  Hero — identity row + status/priority badges + stats + meta
// ───────────────────────────────────────────────────────────────────────

function Hero({
  ticket,
  commentCount,
  isFetching,
  onRefresh,
  onResolve,
  onReopen,
  onAssign,
}: {
  ticket: TicketDto;
  commentCount: number;
  isFetching: boolean;
  onRefresh: () => void;
  onResolve: () => void;
  onReopen: () => void;
  onAssign: () => void;
}) {
  const t = useT();
  const canResolve = ticket.status !== "Resolved" && ticket.status !== "Closed";
  const canReopen = ticket.status === "Resolved" || ticket.status === "Closed";

  const reporter = useUserDisplay(ticket.reporterUserId);
  const assignee = useUserDisplay(ticket.assignedToUserId);

  return (
    <EntityDetailHero
      avatar={<EntityDetailAvatar icon={TicketIcon} name={ticket.title} />}
      title={ticket.title}
      badges={
        <>
          <EntityStatusBadge tone={STATUS_TONE[ticket.status]}>
            {ticketStatusLabel(t, ticket.status)}
          </EntityStatusBadge>
          <EntityStatusBadge tone={PRIORITY_TONE[ticket.priority]}>
            {ticket.priority === "Critical" && (
              <AlertOctagon className="mr-1 size-2.5" />
            )}
            {ticket.priority === "High" && (
              <AlertTriangle className="mr-1 size-2.5" />
            )}
            {ticketPriorityLabel(t, ticket.priority)}
          </EntityStatusBadge>
        </>
      }
      subtitle={
        <>
          <span className="font-mono tabular-nums">{ticket.number}</span>
          <span className="mx-1.5 text-[var(--color-border)]">·</span>
          {t("tickets.openedBy")
            .replace("{rel}", formatRelative(ticket.createdAtUtc))
            .replace("{name}", reporter.name)}
        </>
      }
      actions={
        <>
          <Button
            variant="outline"
            size="sm"
            disabled={isFetching}
            onClick={onRefresh}
            className="gap-1.5"
          >
            <RefreshCw className={cn("h-3.5 w-3.5", isFetching && "animate-spin")} />
            <span className="hidden sm:inline">{t("tickets.refresh")}</span>
          </Button>
          <Button variant="outline" size="sm" onClick={onAssign} className="gap-1.5">
            {ticket.assignedToUserId ? (
              <UserCheck className="h-3.5 w-3.5" />
            ) : (
              <UserX className="h-3.5 w-3.5" />
            )}
            <span className="hidden sm:inline">
              {ticket.assignedToUserId ? t("tickets.reassign") : t("tickets.assign")}
            </span>
          </Button>
          {canResolve && (
            <Button onClick={onResolve} size="sm" className="gap-1.5">
              <CheckCircle2 className="h-3.5 w-3.5" />
              {t("tickets.resolve")}
            </Button>
          )}
          {canReopen && (
            <Button onClick={onReopen} size="sm" variant="outline" className="gap-1.5">
              <RotateCcw className="h-3.5 w-3.5" />
              {t("tickets.reopen")}
            </Button>
          )}
        </>
      }
      stats={
        <>
          <EntityDetailStat
            icon={MessageCircle}
            tone="primary"
            value={commentCount}
            label={commentCount === 1 ? t("tickets.comment") : t("tickets.comments")}
          />
          {ticket.updatedAtUtc && (
            <EntityDetailStat
              icon={Clock}
              tone="default"
              value={formatRelative(ticket.updatedAtUtc)}
              label={t("tickets.updated")}
            />
          )}
          {ticket.resolvedAtUtc && (
            <EntityDetailStat
              icon={CheckCircle2}
              tone="success"
              value={formatRelative(ticket.resolvedAtUtc)}
              label={t("tickets.resolved")}
            />
          )}
        </>
      }
      meta={
        <>
          <EntityDetailMeta icon={User}>
            {t("tickets.assigneeMeta")}&nbsp;
            <span className="font-medium text-[var(--color-foreground)]">
              {ticket.assignedToUserId ? assignee.name : t("tickets.unassigned")}
            </span>
          </EntityDetailMeta>
          <EntityDetailMeta icon={UserCheck}>
            {t("tickets.reporterMeta")}&nbsp;
            <span className="font-medium text-[var(--color-foreground)]">
              {reporter.name}
            </span>
          </EntityDetailMeta>
          <EntityDetailMeta icon={CalendarDays}>
            {t("tickets.createdMeta")}&nbsp;
            <span className="font-medium text-[var(--color-foreground)]">
              {formatDate(ticket.createdAtUtc)}
            </span>
          </EntityDetailMeta>
        </>
      }
    />
  );
}

// ───────────────────────────────────────────────────────────────────────
//  Description section
// ───────────────────────────────────────────────────────────────────────

function DescriptionSection({ ticket }: { ticket: TicketDto }) {
  const t = useT();
  return (
    <EntityDetailSection title={t("tickets.description")} icon={Info}>
      {ticket.description ? (
        <p className="whitespace-pre-wrap text-[14px] leading-relaxed text-[var(--color-foreground)]/90">
          {ticket.description}
        </p>
      ) : (
        <p className="italic text-[13px] leading-relaxed text-[var(--color-muted-foreground)]">
          {t("tickets.noDescription")}
        </p>
      )}

      {ticket.resolutionNote && (
        <div
          className={cn(
            "mt-5 rounded-xl border px-4 py-3",
            "border-[oklch(from_var(--color-success)_l_c_h_/_0.30)]",
            "bg-[oklch(from_var(--color-success)_l_c_h_/_0.06)]",
          )}
        >
          <div className="mb-1 flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wider text-[var(--color-success)]">
            <CheckCircle2 className="h-3 w-3" />
            {t("tickets.resolution")}
          </div>
          <p className="whitespace-pre-wrap text-[13.5px] leading-relaxed text-[var(--color-foreground)]/90">
            {ticket.resolutionNote}
          </p>
        </div>
      )}
    </EntityDetailSection>
  );
}

// ───────────────────────────────────────────────────────────────────────
//  Comments section
// ───────────────────────────────────────────────────────────────────────

function CommentsSection({
  ticketId,
  comments,
  isLoading,
  onPosted,
  disabled,
}: {
  ticketId: string;
  comments: { id: string; authorUserId: string; body: string; createdAtUtc: string }[];
  isLoading: boolean;
  onPosted: () => void;
  disabled: boolean;
}) {
  const t = useT();
  const { user } = useAuth();
  const [body, setBody] = useState("");

  const mutation = useMutation({
    // Per-call data flows through mutate(arg), never closed-over state (golden rule #9).
    mutationFn: (text: string) => addTicketComment(ticketId, text),
    onSuccess: () => {
      setBody("");
      toast.success(t("tickets.posted"));
      onPosted();
    },
    onError: (e) => toast.error(describe(e)),
  });

  const countLabel =
    comments.length === 0
      ? t("tickets.noCommentsYet")
      : `${comments.length} ${comments.length === 1 ? t("tickets.comment") : t("tickets.comments")}`;

  return (
    <EntityDetailSection title={t("tickets.conversation")} icon={MessageCircle} description={countLabel}>
      <div className="space-y-4">
        {isLoading ? (
          <div className="space-y-3">
            <Skeleton className="h-12 w-full rounded-xl" />
            <Skeleton className="h-12 w-3/4 rounded-xl" />
          </div>
        ) : comments.length === 0 ? (
          <p className="italic text-[13px] leading-relaxed text-[var(--color-muted-foreground)]">
            {t("tickets.noCommentsBody")}
          </p>
        ) : (
          <ul className="space-y-3">
            {comments.map((c) => (
              <CommentItem
                key={c.id}
                comment={c}
                isSelf={!!user && c.authorUserId === user.id}
              />
            ))}
          </ul>
        )}
      </div>

      {/* Composer */}
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (!body.trim() || disabled) return;
          mutation.mutate(body.trim());
        }}
        className={cn(
          "mt-5 rounded-xl border bg-[var(--color-card)]",
          "border-[var(--color-border)] p-3",
          "transition-colors focus-within:border-[oklch(from_var(--color-primary)_l_c_h_/_0.4)]",
        )}
      >
        <textarea
          value={body}
          onChange={(e) => setBody(e.target.value)}
          aria-label={t("tickets.addCommentAria")}
          placeholder={
            disabled
              ? t("tickets.commentDisabled")
              : t("tickets.commentPlaceholder")
          }
          disabled={disabled}
          rows={3}
          className={cn(
            "block w-full bg-transparent text-sm leading-relaxed",
            "placeholder:text-[var(--color-muted-foreground)]",
            "resize-none outline-none focus:outline-none focus-visible:outline-none",
          )}
          maxLength={8192}
        />
        <div className="mt-2 flex items-center justify-between gap-2">
          <span className="text-[10.5px] text-[var(--color-muted-foreground)]">
            {body.length} / 8192
          </span>
          <Button
            type="submit"
            size="sm"
            disabled={!body.trim() || disabled || mutation.isPending}
            className="gap-1.5"
          >
            <Send className="h-3.5 w-3.5" />
            {mutation.isPending ? t("tickets.posting") : t("tickets.postComment")}
          </Button>
        </div>
      </form>
    </EntityDetailSection>
  );
}

function CommentItem({
  comment,
  isSelf,
}: {
  comment: { authorUserId: string; body: string; createdAtUtc: string };
  isSelf: boolean;
}) {
  const t = useT();
  const initial =
    comment.authorUserId.replace(/[^a-z0-9]/gi, "").charAt(0).toUpperCase() || "?";

  return (
    <li className={cn("flex gap-3", isSelf && "flex-row-reverse")}>
      <span
        aria-hidden
        className={cn(
          "grid h-8 w-8 shrink-0 place-items-center rounded-full",
          "text-[11px] font-semibold",
          isSelf
            ? "bg-[oklch(from_var(--color-primary)_l_c_h_/_0.16)] text-[var(--color-primary)]"
            : "bg-[var(--color-muted)] text-[var(--color-muted-foreground)]",
        )}
      >
        {initial}
      </span>
      <div className={cn("min-w-0 flex-1", isSelf && "text-right")}>
        <div
          className={cn(
            "inline-block max-w-full rounded-xl border px-3.5 py-2.5 text-left",
            isSelf
              ? "border-[oklch(from_var(--color-primary)_l_c_h_/_0.22)] bg-[oklch(from_var(--color-primary)_l_c_h_/_0.06)]"
              : "border-[var(--color-border)] bg-[var(--color-card)]",
          )}
        >
          <div className="mb-1 flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
            <span
              title={comment.authorUserId}
              className="text-[11.5px] font-semibold text-[var(--color-foreground)]"
            >
              {isSelf ? t("tickets.you") : `${comment.authorUserId.slice(0, 8)}…`}
            </span>
            <span className="text-[10.5px] text-[var(--color-muted-foreground)]">
              {formatRelative(comment.createdAtUtc)}
            </span>
          </div>
          <p className="whitespace-pre-wrap text-[13.5px] leading-relaxed text-[var(--color-foreground)]/90">
            {comment.body}
          </p>
        </div>
      </div>
    </li>
  );
}

// ───────────────────────────────────────────────────────────────────────
//  Properties sidebar
// ───────────────────────────────────────────────────────────────────────

function PropertiesSection({ ticket }: { ticket: TicketDto }) {
  const t = useT();
  const reporter = useUserDisplay(ticket.reporterUserId);
  const assignee = useUserDisplay(ticket.assignedToUserId);
  return (
    <EntityDetailSection title={t("tickets.properties")} icon={Info}>
      <dl className="space-y-3 text-[13px]">
        <Prop label={t("tickets.reporter")}>
          <span
            title={reporter.handle ?? ticket.reporterUserId}
            className="font-medium text-[var(--color-foreground)]"
          >
            {reporter.name}
          </span>
        </Prop>
        <Prop label={t("tickets.assignee")}>
          {ticket.assignedToUserId ? (
            <span
              title={assignee.handle ?? ticket.assignedToUserId}
              className="font-medium text-[var(--color-primary)]"
            >
              {assignee.name}
            </span>
          ) : (
            <span className="text-[var(--color-muted-foreground)]">{t("tickets.unassigned")}</span>
          )}
        </Prop>
        <Prop label={t("tickets.colStatus")}>
          <EntityStatusBadge tone={STATUS_TONE[ticket.status]}>
            {ticketStatusLabel(t, ticket.status)}
          </EntityStatusBadge>
        </Prop>
        <Prop label={t("tickets.colPriority")}>
          <EntityStatusBadge tone={PRIORITY_TONE[ticket.priority]}>
            {ticketPriorityLabel(t, ticket.priority)}
          </EntityStatusBadge>
        </Prop>
        <Prop
          label={t("tickets.created")}
          value={formatDate(ticket.createdAtUtc)}
          hint={formatRelative(ticket.createdAtUtc)}
        />
        {ticket.updatedAtUtc && (
          <Prop
            label={t("tickets.updatedCap")}
            value={formatDate(ticket.updatedAtUtc)}
            hint={formatRelative(ticket.updatedAtUtc)}
          />
        )}
        {ticket.resolvedAtUtc && (
          <Prop
            label={t("tickets.resolvedCap")}
            value={formatDate(ticket.resolvedAtUtc)}
            hint={formatRelative(ticket.resolvedAtUtc)}
          />
        )}
      </dl>
    </EntityDetailSection>
  );
}

function Prop({
  label,
  value,
  hint,
  children,
}: {
  label: string;
  value?: React.ReactNode;
  hint?: string;
  children?: React.ReactNode;
}) {
  return (
    <div>
      <dt className="text-[11px] font-semibold uppercase tracking-wider text-[var(--color-muted-foreground)]">
        {label}
      </dt>
      <dd className="mt-1 flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
        {children ?? (
          <span className="font-medium text-[var(--color-foreground)] tabular-nums">
            {value}
          </span>
        )}
        {hint && (
          <span className="text-[11px] text-[var(--color-muted-foreground)]">
            {hint}
          </span>
        )}
      </dd>
    </div>
  );
}

// ───────────────────────────────────────────────────────────────────────
//  Resolve dialog
// ───────────────────────────────────────────────────────────────────────

function ResolveDialog({
  open,
  ticket,
  onClose,
}: {
  open: boolean;
  ticket: TicketDto;
  onClose: () => void;
}) {
  const t = useT();
  const queryClient = useQueryClient();
  const [note, setNote] = useState("");

  useEffect(() => {
    if (open) setNote("");
  }, [open]);

  const mutation = useMutation({
    mutationFn: () => resolveTicket(ticket.id, note.trim() || null),
    onSuccess: () => {
      toast.success(t("tickets.ticketResolved"));
      void queryClient.invalidateQueries({ queryKey: ["tickets"] });
      onClose();
    },
    onError: (e) => toast.error(describe(e)),
  });

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <span
              aria-hidden
              className="grid h-6 w-6 place-items-center rounded-md bg-[oklch(from_var(--color-success)_l_c_h_/_0.16)] text-[var(--color-success)]"
            >
              <CheckCircle2 className="h-3.5 w-3.5" />
            </span>
            {ticket.number} — {ticket.title}
          </DialogTitle>
          <DialogDescription>
            {t("tickets.resolveDesc")}
          </DialogDescription>
        </DialogHeader>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            mutation.mutate();
          }}
        >
          <DialogBody className="space-y-4">
            <Field id="resolve-note" label={t("tickets.resolutionNote")} hint={t("tickets.resolutionHint")}>
              <textarea
                id="resolve-note"
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder={t("tickets.resolutionPlaceholder")}
                rows={4}
                className={cn(
                  "block w-full rounded-md border border-[var(--color-input)] bg-[var(--color-card)]",
                  "px-3 py-2 text-sm shadow-[var(--shadow-xs)] placeholder:text-[var(--color-muted-foreground)]",
                  "focus-visible:border-[var(--color-input)]",
                )}
                maxLength={4096}
              />
            </Field>
          </DialogBody>
          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline">{t("chrome.cancel")}</Button>
            </DialogClose>
            <Button
              type="submit"
              disabled={mutation.isPending}
              className="gap-1.5"
            >
              <CheckCircle2 className="h-3.5 w-3.5" />
              {mutation.isPending ? t("tickets.resolving") : t("tickets.markResolved")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

// ───────────────────────────────────────────────────────────────────────
//  Assign dialog
// ───────────────────────────────────────────────────────────────────────

function AssignDialog({
  open,
  ticket,
  onClose,
}: {
  open: boolean;
  ticket: TicketDto;
  onClose: () => void;
}) {
  const t = useT();
  const queryClient = useQueryClient();
  const [assignee, setAssignee] = useState(ticket.assignedToUserId ?? "");
  const [touched, setTouched] = useState(false);

  useEffect(() => {
    if (open) {
      setAssignee(ticket.assignedToUserId ?? "");
      setTouched(false);
    }
  }, [open, ticket.assignedToUserId]);

  const mutation = useMutation({
    mutationFn: () => assignTicket(ticket.id, assignee.trim() || null),
    onSuccess: () => {
      toast.success(assignee.trim() ? t("tickets.assigned") : t("tickets.unassignedToast"));
      void queryClient.invalidateQueries({ queryKey: ["tickets"] });
      onClose();
    },
    onError: (e) => toast.error(describe(e)),
  });

  const onSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setTouched(true);
    mutation.mutate();
  };

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <span
              aria-hidden
              className="grid h-6 w-6 place-items-center rounded-md bg-[oklch(from_var(--color-primary)_l_c_h_/_0.16)] text-[var(--color-primary)]"
            >
              <UserCheck className="h-3.5 w-3.5" />
            </span>
            {ticket.number} — {ticket.title}
          </DialogTitle>
          <DialogDescription>
            {t("tickets.assignDesc")}
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={onSubmit}>
          <DialogBody className="space-y-4">
            <Field
              id="assignee"
              label={t("tickets.assignee")}
              hint={t("tickets.assignHint")}
            >
              <UserPicker
                value={assignee || null}
                onChange={(userId) => {
                  setAssignee(userId ?? "");
                  setTouched(true);
                }}
              />
            </Field>
            {touched && assignee.trim() === "" && (
              <p className="text-[12px] text-[var(--color-muted-foreground)]">
                {t("tickets.unassignHint")}
              </p>
            )}
          </DialogBody>
          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline">{t("chrome.cancel")}</Button>
            </DialogClose>
            <Button
              type="submit"
              disabled={mutation.isPending}
              className="gap-1.5"
            >
              <Sparkles className="h-3.5 w-3.5" />
              {mutation.isPending ? t("identity.saving") : t("tickets.saveAssignment")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

// ───────────────────────────────────────────────────────────────────────
//  Skeleton + NotFound
// ───────────────────────────────────────────────────────────────────────

function DetailSkeleton() {
  return (
    <>
      <div className="overflow-hidden rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] shadow-xs">
        <div className="h-1 w-full bg-[var(--color-muted)]" />
        <div className="space-y-4 p-5 sm:px-6">
          <div className="flex items-center gap-4">
            <Skeleton className="size-11 rounded-xl sm:size-14 sm:rounded-2xl" />
            <div className="flex-1 space-y-2">
              <Skeleton className="h-5 w-2/3" />
              <Skeleton className="h-3 w-1/3" />
            </div>
          </div>
          <Skeleton className="h-7 w-40" />
        </div>
      </div>
      <div className="grid grid-cols-1 gap-5 lg:grid-cols-[1fr_320px]">
        <div className="space-y-5">
          <Skeleton className="h-32 rounded-xl" />
          <Skeleton className="h-64 rounded-xl" />
        </div>
        <Skeleton className="h-64 rounded-xl" />
      </div>
    </>
  );
}

function NotFoundPanel({ onBack }: { onBack: () => void }) {
  const t = useT();
  return (
    <div
      className={cn(
        "rounded-xl border border-[var(--color-border)] bg-[var(--color-card)]",
        "shadow-xs",
        "flex flex-col items-center gap-4 px-8 py-16 text-center",
      )}
    >
      <span
        aria-hidden
        className={cn(
          "grid h-14 w-14 place-items-center rounded-2xl",
          "bg-[oklch(from_var(--color-primary)_l_c_h_/_0.10)]",
          "ring-1 ring-inset ring-[oklch(from_var(--color-primary)_l_c_h_/_0.22)]",
        )}
      >
        <TicketIcon className="h-6 w-6 text-[var(--color-primary)]" />
      </span>
      <h3 className="font-display text-xl font-semibold tracking-tight text-[var(--color-foreground)]">
        {t("tickets.notFound")}
      </h3>
      <p className="max-w-md text-sm leading-relaxed text-[var(--color-muted-foreground)]">
        {t("tickets.notFoundBody")}
      </p>
      <div className="flex items-center gap-2">
        <Button onClick={onBack} variant="outline">{t("tickets.back")}</Button>
        <Link to="/tickets">
          <Button>{t("tickets.desk")}</Button>
        </Link>
      </div>
    </div>
  );
}

// Reserved for future filtering — suppress unused-import warnings.
void TICKET_PRIORITIES;
