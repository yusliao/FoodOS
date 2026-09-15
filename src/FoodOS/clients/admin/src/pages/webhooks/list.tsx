import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ChevronRight,
  Plus,
  RefreshCw,
  Send,
  Trash2,
  Webhook,
} from "lucide-react";
import { toast } from "sonner";
import {
  deleteWebhookSubscription,
  listWebhookSubscriptions,
  testWebhookSubscription,
  type WebhookSubscriptionDto,
} from "@/api/webhooks";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  EntityPageHeader,
  ErrorBand,
  LoadingRow,
  Pagination,
} from "@/components/list";
import { EmptyState } from "@/components/empty-state";
import { CreateWebhookDialog } from "@/components/webhooks/create-webhook-dialog";
import { ApiRequestError } from "@/lib/api-client";
import { cn } from "@/lib/cn";
import { useT } from "@/i18n/locale-provider";

const PAGE_SIZE = 25;

export function WebhooksListPage() {
  const t = useT();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ["webhooks", "subscriptions", page],
    queryFn: () => listWebhookSubscriptions(page, PAGE_SIZE),
    placeholderData: keepPreviousData,
  });

  const test = useMutation({
    mutationFn: (id: string) => testWebhookSubscription(id),
    onMutate: (id) => setBusyId(id),
    onSuccess: (data) => {
      if (data.success) {
        toast.success(t("webhooks.testDelivered"), {
          description: t("webhooks.testDeliveredBody"),
        });
      } else {
        toast.warning(t("webhooks.testFailed"), {
          description: t("webhooks.testFailedBody"),
        });
      }
    },
    onError: (err) => toast.error(t("webhooks.testFailed"), { description: describe(err) }),
    onSettled: () => setBusyId(null),
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteWebhookSubscription(id),
    onMutate: (id) => setBusyId(id),
    onSuccess: () => {
      toast.success(t("webhooks.deleted"));
      queryClient.invalidateQueries({ queryKey: ["webhooks", "subscriptions"] });
    },
    onError: (err) => toast.error(t("webhooks.deleteFailed"), { description: describe(err) }),
    onSettled: () => setBusyId(null),
  });

  const data = query.data;
  const items = data?.items ?? [];

  return (
    <div className="space-y-8">
      <EntityPageHeader
        icon={Webhook}
        title={t("webhooks.title")}
        total={data?.totalCount ?? null}
        unit={t("webhooks.unit")}
        description={t("webhooks.description")}
      >
        <Button
          variant="outline"
          size="sm"
          disabled={query.isFetching}
          onClick={() => query.refetch()}
          className="flex-1 sm:flex-none"
        >
          <RefreshCw className={cn("mr-1.5 h-3.5 w-3.5", query.isFetching && "animate-spin")} />
          {t("webhooks.refresh")}
        </Button>
        <Button onClick={() => setCreateOpen(true)} className="flex-1 sm:flex-none">
          <Plus className="mr-1 h-4 w-4" /> {t("webhooks.newSubscription")}
        </Button>
      </EntityPageHeader>

      {query.isError && (
        <ErrorBand
          message={
            query.error instanceof ApiRequestError
              ? query.error.problem?.detail ?? query.error.message
              : t("webhooks.loadFailed")
          }
        />
      )}

      {query.isLoading && <LoadingRow label={t("webhooks.loading")} />}

      {!query.isLoading && items.length === 0 && !query.isError && (
        <EmptyState
          icon={Webhook}
          kicker={t("webhooks.emptyKicker")}
          title={t("webhooks.emptyTitle")}
          description={t("webhooks.emptyBody")}
          action={
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="mr-1 h-4 w-4" /> {t("webhooks.newSubscription")}
            </Button>
          }
        />
      )}

      {items.length > 0 && (
        <ol className="divide-y divide-[var(--color-border)] border-y border-[var(--color-border)]">
          {items.map((sub, i) => (
            <Row
              key={sub.id}
              sub={sub}
              index={i + 1 + (page - 1) * PAGE_SIZE}
              busy={busyId === sub.id}
              onTest={() => test.mutate(sub.id)}
              onDelete={() => {
                if (window.confirm(t("webhooks.deleteConfirm").replace("{url}", sub.url))) {
                  remove.mutate(sub.id);
                }
              }}
              onOpen={() => navigate(`/webhooks/${sub.id}`)}
            />
          ))}
        </ol>
      )}

      {data && data.totalPages > 1 && (
        <Pagination
          page={data.pageNumber}
          totalPages={data.totalPages}
          totalCount={data.totalCount}
          shown={items.length}
          fetching={query.isFetching}
          hasPrev={data.hasPrevious}
          hasNext={data.hasNext}
          onPrev={() => setPage((p) => Math.max(1, p - 1))}
          onNext={() => setPage((p) => p + 1)}
          noun="subscriptions"
        />
      )}

      <CreateWebhookDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        onCreated={() => queryClient.invalidateQueries({ queryKey: ["webhooks", "subscriptions"] })}
      />
    </div>
  );
}

function Row({
  sub,
  index,
  busy,
  onTest,
  onDelete,
  onOpen,
}: {
  sub: WebhookSubscriptionDto;
  index: number;
  busy: boolean;
  onTest: () => void;
  onDelete: () => void;
  onOpen: () => void;
}) {
  const t = useT();
  const num = String(index).padStart(3, "0");
  return (
    <li>
      <div className="group grid grid-cols-[3rem_auto_1fr_auto_auto_auto] items-center gap-3 px-1 py-3.5">
        <span className="font-mono text-xs tabular-nums text-[var(--color-muted-foreground)]">
          #{num}
        </span>
        <span
          className={cn(
            "grid h-2 w-2 place-items-center rounded-full",
            sub.isActive ? "bg-[var(--color-accent-signal)]" : "bg-[var(--color-muted-foreground)]/50",
          )}
          aria-hidden
          title={sub.isActive ? t("webhooks.active") : t("webhooks.inactive")}
        />
        <button
          type="button"
          onClick={onOpen}
          className="min-w-0 text-left transition-colors hover:bg-[var(--color-muted)]/40 -mx-2 px-2 py-1 rounded-md"
        >
          <div className="truncate font-mono text-[13px] font-medium">{sub.url}</div>
          <div className="mt-0.5 flex flex-wrap items-center gap-1.5">
            {sub.events.slice(0, 4).map((e) => (
              <code key={e} className="code-chip">{e}</code>
            ))}
            {sub.events.length > 4 && (
              <span className="font-mono text-[10.5px] text-[var(--color-muted-foreground)]">
                {t("webhooks.moreEvents").replace("{n}", String(sub.events.length - 4))}
              </span>
            )}
            <span className="font-mono text-[10.5px] uppercase tracking-[0.18em] text-[var(--color-muted-foreground)]">
              {t("webhooks.since").replace("{date}", new Date(sub.createdAtUtc).toLocaleDateString())}
            </span>
          </div>
        </button>
        <Badge
          variant={sub.isActive ? "success" : "muted"}
          className="font-mono uppercase tracking-[0.14em]"
        >
          {sub.isActive ? t("webhooks.active") : t("webhooks.inactive")}
        </Badge>
        <Button variant="outline" size="sm" onClick={onTest} disabled={busy}>
          <Send className="mr-1 h-3.5 w-3.5" /> {t("webhooks.test")}
        </Button>
        <Button
          variant="ghost"
          size="sm"
          onClick={onDelete}
          disabled={busy}
          aria-label={t("webhooks.deleteAria").replace("{url}", sub.url)}
          className="text-[var(--color-destructive)] hover:bg-[oklch(from_var(--color-destructive)_l_c_h_/_0.08)]"
        >
          <Trash2 className="h-3.5 w-3.5" />
        </Button>
        <ChevronRight
          className="hidden h-4 w-4 text-[var(--color-muted-foreground)] transition-transform group-hover:translate-x-0.5"
          aria-hidden
        />
      </div>
    </li>
  );
}

function describe(err: unknown): string {
  if (err instanceof ApiRequestError) return err.problem?.detail ?? err.problem?.title ?? err.message;
  if (err instanceof Error) return err.message;
  return String(err);
}
