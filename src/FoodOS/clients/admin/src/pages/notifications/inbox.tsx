import { useState } from "react";
import { useIsMutating, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Bell, CheckCheck, ExternalLink, RefreshCw } from "lucide-react";
import { toast } from "sonner";
import {
  listNotifications,
  markAllNotificationsRead,
  markNotificationRead,
  type NotificationDto,
} from "@/api/notifications";
import { useRealtimeEvent } from "@/realtime/realtime-context";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { NotificationLink } from "@/components/notifications/notification-link";
import {
  EntityPageHeader,
  ErrorBand,
  FilterBar,
  LoadingRow,
  Select,
} from "@/components/list";
import { EmptyState } from "@/components/empty-state";
import { ApiRequestError } from "@/lib/api-client";
import { cn } from "@/lib/cn";
import { useT } from "@/i18n/locale-provider";
import { useAuth } from "@/auth/use-auth";
import { NotificationPermissions } from "@/lib/permissions";

type Filter = "all" | "unread";
type Translate = (key: string, fallback?: string) => string;

export function NotificationsInboxPage() {
  const t = useT();
  const { user } = useAuth();
  const canView = !!user?.permissions.includes(NotificationPermissions.Inbox.View);
  const canMark = canView && !!user?.permissions.includes(NotificationPermissions.Inbox.MarkRead);
  const busy = useIsMutating({ mutationKey: ["notifications", "write"] }) > 0;
  const queryClient = useQueryClient();
  const [filter, setFilter] = useState<Filter>("unread");

  const query = useQuery({
    queryKey: ["notifications", "inbox", filter],
    queryFn: ({ signal }) =>
      listNotifications({ unreadOnly: filter === "unread", pageSize: 100 }, signal),
    enabled: canView,
    staleTime: 15_000,
  });

  // Live append on new notification.
  useRealtimeEvent<unknown>("NotificationCreated", () => {
    queryClient.invalidateQueries({ queryKey: ["notifications"] });
  });

  const markOne = useMutation({
    mutationKey: ["notifications", "write"],
    mutationFn: (id: string) => markNotificationRead(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["notifications"] }),
    onError: (err) => toast.error(t("notifications.markReadFailed"), { description: describe(err) }),
  });

  const markAll = useMutation({
    mutationKey: ["notifications", "write"],
    mutationFn: markAllNotificationsRead,
    onSuccess: async (data) => {
      toast.success(
        (data.updated === 1 ? t("notifications.markedOne") : t("notifications.markedMany")).replace(
          "{n}",
          String(data.updated),
        ),
      );
      await queryClient.invalidateQueries({ queryKey: ["notifications"] });
    },
    onError: (err) => toast.error(t("notifications.markAllFailed"), { description: describe(err) }),
  });

  const items = query.isError ? [] : query.data ?? [];
  const filterOptions = [
    { value: "unread", label: t("notifications.filterUnread") },
    { value: "all", label: t("notifications.filterAll") },
  ];

  return (
    <div className="space-y-8">
      <EntityPageHeader
        icon={Bell}
        title={t("notifications.title")}
        total={items.length}
        unit={t("notifications.unit")}
        description={t("notifications.description")}
      >
        <Button
          variant="outline"
          size="sm"
          disabled={query.isFetching}
          onClick={() => { if (canView) void query.refetch(); }}
          className="flex-1 sm:flex-none"
        >
          <RefreshCw className={cn("mr-1.5 h-3.5 w-3.5", query.isFetching && "animate-spin")} />
          {t("notifications.refresh")}
        </Button>
        {canMark && <Button
          variant="signal"
          size="sm"
          onClick={() => { if (canMark && !busy && !query.isError) markAll.mutate(); }}
          disabled={busy || query.isLoading || query.isError}
          className="flex-1 sm:flex-none"
        >
          <CheckCheck className="mr-1.5 h-3.5 w-3.5" />
          {markAll.isPending ? t("notifications.marking") : t("notifications.markAll")}
        </Button>}
      </EntityPageHeader>

      <FilterBar>
        <Select
          value={filter}
          onValueChange={(v) => setFilter((v as Filter) || "all")}
          options={filterOptions}
          className="min-w-[10rem]"
        />
      </FilterBar>

      {query.isError && (
        <ErrorBand
          message={
            query.error instanceof ApiRequestError
              ? query.error.problem?.detail ?? query.error.message
              : t("notifications.loadFailed")
          }
        />
      )}

      {query.isLoading && <LoadingRow label={t("notifications.loading")} />}

      {!query.isLoading && items.length === 0 && !query.isError && (
        <EmptyState
          icon={Bell}
          kicker={t("notifications.emptyKicker")}
          title={filter === "unread" ? t("notifications.emptyUnreadTitle") : t("notifications.emptyAllTitle")}
          description={
            filter === "unread" ? t("notifications.emptyUnreadBody") : t("notifications.emptyAllBody")
          }
        />
      )}

      {items.length > 0 && (
        <ul className="divide-y divide-[var(--color-border)] border-y border-[var(--color-border)]">
          {items.map((n) => (
            <Row key={n.id} notif={n} canMark={canMark} busy={busy} onMarkRead={() => { if (canMark && !busy && !n.readAtUtc) markOne.mutate(n.id); }} t={t} />
          ))}
        </ul>
      )}
    </div>
  );
}

function Row({
  notif,
  onMarkRead,
  canMark,
  busy,
  t,
}: {
  notif: NotificationDto;
  onMarkRead: () => void;
  canMark: boolean;
  busy: boolean;
  t: Translate;
}) {
  const unread = !notif.readAtUtc;
  return (
    <li
      className={cn(
        "grid grid-cols-[auto_auto_minmax(0,1fr)] sm:grid-cols-[auto_auto_minmax(0,1fr)_auto] items-start gap-3 px-1 py-3.5 text-sm",
        unread && "bg-[oklch(from_var(--color-accent-signal)_l_c_h_/_0.03)]",
      )}
    >
      <span
        aria-hidden
        className={cn(
          "mt-1.5 h-2 w-2 shrink-0 rounded-full",
          unread ? "bg-[var(--color-accent-signal)]" : "bg-transparent border border-[var(--color-border-strong)]",
        )}
      />
      <Badge variant="muted" className="font-mono uppercase tracking-[0.14em]">
        {notif.source}
      </Badge>
      <div className="min-w-0">
        <div className="flex flex-wrap items-baseline gap-x-2">
          <span className="font-medium">{notif.title}</span>
          <code className="code-chip">{notif.type}</code>
          <span className="font-mono text-[10.5px] uppercase tracking-[0.14em] text-[var(--color-muted-foreground)]">
            {new Date(notif.createdAtUtc).toLocaleString()}
          </span>
        </div>
        {notif.body && (
          <p className="mt-0.5 text-[13px] text-[var(--color-muted-foreground)]">
            {notif.body}
          </p>
        )}
        {notif.link && (
          <NotificationLink
            href={notif.link}
            className="mt-1 inline-flex items-center gap-1 font-mono text-[10.5px] uppercase tracking-[0.14em] text-[var(--color-foreground)] hover:underline"
          >
            <ExternalLink className="h-3 w-3" />
            {t("notifications.open")}
          </NotificationLink>
        )}
      </div>
      {unread && canMark && (
        <Button variant="ghost" size="sm" disabled={busy} onClick={onMarkRead} className="col-start-3 sm:col-start-auto">
          <CheckCheck className="mr-1 h-3.5 w-3.5" /> {t("notifications.markRead")}
        </Button>
      )}
    </li>
  );
}

function describe(err: unknown): string {
  if (err instanceof ApiRequestError) return err.problem?.detail ?? err.problem?.title ?? err.message;
  if (err instanceof Error) return err.message;
  return String(err);
}
