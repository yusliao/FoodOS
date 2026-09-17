import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useIsMutating, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Bell, BellRing, CheckCheck } from "lucide-react";
import { toast } from "sonner";
import {
  getUnreadCount,
  listNotifications,
  markAllNotificationsRead,
  markNotificationRead,
  type NotificationDto,
} from "@/api/notifications";
import { useRealtimeEvent } from "@/realtime/realtime-context";
import { useAuth } from "@/auth/use-auth";
import { cn } from "@/lib/cn";
import { useT } from "@/i18n/locale-provider";
import { NotificationPermissions } from "@/lib/permissions";
import { Button } from "@/components/ui/button";
import { NotificationLink } from "@/components/notifications/notification-link";

/**
 * NotificationBell — topbar trigger with unread badge and a popover preview
 * of the most recent items. Live-updates via the SignalR NotificationCreated
 * event (bumps the unread query + flashes the bell). Full inbox lives at
 * /notifications.
 */
export function NotificationBell() {
  const t = useT();
  const { isAuthenticated, user } = useAuth();
  const canView = isAuthenticated && !!user?.permissions.includes(NotificationPermissions.Inbox.View);
  const canMark = canView && !!user?.permissions.includes(NotificationPermissions.Inbox.MarkRead);
  const busy = useIsMutating({ mutationKey: ["notifications", "write"] }) > 0;
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [pulse, setPulse] = useState(false);

  const unread = useQuery({
    queryKey: ["notifications", "unread-count"],
    queryFn: ({ signal }) => getUnreadCount(signal),
    enabled: canView,
    staleTime: 30_000,
    refetchInterval: 60_000,
  });

  const recent = useQuery({
    queryKey: ["notifications", "recent"],
    queryFn: ({ signal }) => listNotifications({ pageSize: 8 }, signal),
    enabled: canView && open,
    staleTime: 15_000,
  });

  // Coalesce a burst of NotificationCreated events into a single refetch so a
  // flood doesn't trigger a flood of badge/preview queries.
  const refreshTimer = useRef<number | null>(null);
  useRealtimeEvent<unknown>("NotificationCreated", () => {
    setPulse(true);
    window.setTimeout(() => setPulse(false), 1500);
    if (refreshTimer.current !== null) return;
    refreshTimer.current = window.setTimeout(() => {
      refreshTimer.current = null;
      queryClient.invalidateQueries({ queryKey: ["notifications"] });
    }, 800);
  });
  useEffect(
    () => () => {
      if (refreshTimer.current !== null) window.clearTimeout(refreshTimer.current);
    },
    [],
  );

  // Close the popover on Escape (it isn't a focus-trapping modal).
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(false);
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [open]);

  const markOne = useMutation({
    mutationKey: ["notifications", "write"],
    mutationFn: (id: string) => markNotificationRead(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["notifications"] }),
    onError: () => toast.error(t("notifications.markReadFailed")),
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
    onError: () => toast.error(t("notifications.markAllFailed")),
  });

  if (!canView) return null;

  const count = unread.isError ? 0 : unread.data ?? 0;
  const items = recent.isError ? [] : recent.data ?? [];

  return (
    <div className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-label={count > 0 ? t("notifications.unreadCount").replace("{n}", String(count)) : t("notifications.title")}
        aria-haspopup="true"
        aria-expanded={open}
        className={cn(
          "relative grid h-8 w-8 place-items-center rounded-md text-[var(--color-muted-foreground)]",
          "transition-colors hover:bg-[var(--color-accent)] hover:text-[var(--color-foreground)]",
          "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-ring)]",
        )}
      >
        {pulse ? <BellRing className="h-4 w-4 text-[var(--color-accent-signal)]" /> : <Bell className="h-4 w-4" />}
        {count > 0 && (
          <span
            aria-hidden
            className="absolute right-1 top-1 inline-flex h-3.5 min-w-[14px] items-center justify-center rounded-full bg-[var(--color-accent-signal)] px-[3px] font-mono text-[9px] font-bold tabular-nums text-[var(--color-accent-signal-foreground)] ring-2 ring-[var(--color-background)]"
          >
            {count > 99 ? "99+" : count}
          </span>
        )}
      </button>

      {open && (
        <>
          {/* Click-away catcher — not in the tab order or AT tree. */}
          <button
            type="button"
            aria-hidden
            tabIndex={-1}
            onClick={() => setOpen(false)}
            className="fixed inset-0 z-40 cursor-default bg-transparent"
          />
          <div
            aria-label={t("notifications.title")}
            role="region"
            className="fixed right-4 sm:absolute sm:right-0 z-50 mt-2 w-[22rem] max-w-[calc(100vw-2rem)] overflow-hidden rounded-xl card-shell shadow-[0_24px_64px_-24px_oklch(0_0_0/0.30)]"
          >
            <div className="flex items-center justify-between border-b border-[var(--color-border)] px-3 py-2.5">
              <div className="meta text-[var(--color-muted-foreground)]">{t("notifications.kicker")}</div>
              {canMark && count > 0 && (
                <button
                  type="button"
                  onClick={() => { if (canMark && !busy) markAll.mutate(); }}
                  disabled={busy}
                  className="inline-flex items-center gap-1 font-mono text-[10.5px] uppercase tracking-[0.14em] text-[var(--color-muted-foreground)] transition-colors hover:text-[var(--color-foreground)]"
                >
                  <CheckCheck className="h-3 w-3" />
                  {t("notifications.markAll")}
                </button>
              )}
            </div>

            <div className="max-h-[24rem] overflow-y-auto">
              {recent.isLoading && (
                <p
                  role="status"
                  className="px-3 py-6 text-center font-mono text-xs uppercase tracking-[0.18em] text-[var(--color-muted-foreground)]"
                >
                  {t("notifications.loadingShort")}<span className="caret text-[var(--color-accent-signal)]" />
                </p>
              )}

              {(recent.isError || unread.isError) && <div className="space-y-2 p-3">
                <p role="alert">{t("notifications.loadFailed")}</p>
                <Button size="sm" variant="outline" disabled={recent.isFetching || unread.isFetching} onClick={() => {
                  if (canView) { void recent.refetch(); void unread.refetch(); }
                }}>{t("notifications.refresh")}</Button>
              </div>}
              {!recent.isLoading && !recent.isError && !unread.isError && items.length === 0 && (
                <p className="px-3 py-8 text-center text-sm text-[var(--color-muted-foreground)]">
                  {t("notifications.caughtUp")}
                </p>
              )}

              <ul className="divide-y divide-[var(--color-border)]">
                {items.map((n) => (
                  <Row
                    key={n.id}
                    notif={n}
                    canMark={canMark}
                    busy={busy}
                    onMarkRead={() => { if (canMark && !busy && !n.readAtUtc) markOne.mutate(n.id); }}
                    onClick={() => setOpen(false)}
                    t={t}
                  />
                ))}
              </ul>
            </div>

            <div className="border-t border-[var(--color-border)] px-3 py-2">
              <Link
                to="/notifications"
                onClick={() => setOpen(false)}
                className="inline-flex items-center gap-1 font-mono text-[10.5px] uppercase tracking-[0.14em] text-[var(--color-foreground)] hover:underline"
              >
                {t("notifications.viewAll")}
              </Link>
            </div>
          </div>
        </>
      )}
    </div>
  );
}

function Row({
  notif,
  onMarkRead,
  canMark,
  busy,
  onClick,
  t,
}: {
  notif: NotificationDto;
  onMarkRead: () => void;
  canMark: boolean;
  busy: boolean;
  onClick: () => void;
  t: (key: string, fallback?: string) => string;
}) {
  const unread = !notif.readAtUtc;
  const body = (
    <div className="min-w-0 flex-1">
      <div className="flex flex-wrap items-baseline gap-x-2">
        <span className="truncate font-medium">{notif.title}</span>
        <span className="font-mono text-[10px] uppercase tracking-[0.14em] text-[var(--color-muted-foreground)]">
          {formatRelative(notif.createdAtUtc, t)}
        </span>
      </div>
      {notif.body && (
        <p className="mt-0.5 line-clamp-2 text-[12px] text-[var(--color-muted-foreground)]">
          {notif.body}
        </p>
      )}
    </div>
  );

  return (
    <li
      className={cn(
        "group/notif flex items-start gap-2.5 px-3 py-2.5 text-sm transition-colors",
        unread && "bg-[oklch(from_var(--color-accent-signal)_l_c_h_/_0.04)]",
        "hover:bg-[var(--color-muted)]/40",
      )}
    >
      <span
        aria-hidden
        className={cn(
          "mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full",
          unread ? "bg-[var(--color-accent-signal)]" : "bg-transparent",
        )}
      />
        <NotificationLink href={notif.link} fallback={body} onClick={onClick} className="block min-w-0 flex-1">
          {body}
        </NotificationLink>
      {unread && canMark && (
        <button
          type="button"
          disabled={busy}
          onClick={(e) => {
            e.preventDefault();
            e.stopPropagation();
            onMarkRead();
          }}
          aria-label={t("notifications.markReadAria")}
          className="mt-0.5 inline-flex h-7 w-7 shrink-0 items-center justify-center rounded text-[var(--color-muted-foreground)] transition-colors hover:bg-[var(--color-muted)] hover:text-[var(--color-foreground)]"
        >
          <CheckCheck className="h-3 w-3" />
        </button>
      )}
    </li>
  );
}

function formatRelative(value: string, t: (key: string, fallback?: string) => string): string {
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  const diff = Date.now() - d.getTime();
  const sec = Math.round(diff / 1000);
  if (sec < 60) return t("notifications.relSeconds").replace("{n}", String(sec));
  const min = Math.round(sec / 60);
  if (min < 60) return t("notifications.relMinutes").replace("{n}", String(min));
  const hr = Math.round(min / 60);
  if (hr < 24) return t("notifications.relHours").replace("{n}", String(hr));
  const day = Math.round(hr / 24);
  if (day < 14) return t("notifications.relDays").replace("{n}", String(day));
  return d.toLocaleDateString();
}
