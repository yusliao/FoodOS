import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ArrowLeft,
  CheckCircle2,
  Link2,
  List,
  RefreshCw,
  Send,
  Trash2,
  XCircle,
} from "lucide-react";
import { toast } from "sonner";
import {
  deleteWebhookSubscription,
  listWebhookDeliveries,
  findWebhookSubscription,
  testWebhookSubscription,
  type WebhookDeliveryDto,
} from "@/api/webhooks";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  ErrorBand,
  LoadingRow,
  PageHeader,
  Pagination,
  SettingsSection,
} from "@/components/list";
import { ApiRequestError } from "@/lib/api-client";
import { cn } from "@/lib/cn";
import { useT } from "@/i18n/locale-provider";
import { useAuth } from "@/auth/use-auth";
import { WebhooksPermissions } from "@/lib/permissions";

const PAGE_SIZE = 25;

export function WebhookDetailPage() {
  const t = useT();
  const { user } = useAuth();
  const canView = !!user?.permissions.includes(WebhooksPermissions.Subscriptions.View);
  const canTest = canView && !!user?.permissions.includes(WebhooksPermissions.Subscriptions.Test);
  const canDelete = canView && !!user?.permissions.includes(WebhooksPermissions.Subscriptions.Delete);
  const { id = "" } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [deliveryPage, setDeliveryPage] = useState(1);

  const subsQuery = useQuery({
    queryKey: ["webhooks", "subscriptions", "detail", id],
    queryFn: ({ signal }) => findWebhookSubscription(id, signal),
    enabled: canView && !!id,
  });

  const sub = subsQuery.isError ? null : subsQuery.data;

  const deliveries = useQuery({
    queryKey: ["webhooks", "deliveries", id, deliveryPage],
    queryFn: ({ signal }) => listWebhookDeliveries(id, deliveryPage, PAGE_SIZE, signal),
    enabled: canView && Boolean(sub),
    refetchInterval: 10_000,
  });

  const test = useMutation({
    mutationFn: (subscriptionId: string) => testWebhookSubscription(subscriptionId),
    onSuccess: (data) => {
      toast[data.success ? "success" : "warning"](
        data.success ? t("webhooks.testDelivered") : t("webhooks.rejected"),
      );
      deliveries.refetch();
    },
    onError: (err) => toast.error(t("webhooks.testFailed"), { description: describe(err) }),
  });

  const remove = useMutation({
    mutationFn: (subscriptionId: string) => deleteWebhookSubscription(subscriptionId),
    onSuccess: () => {
      toast.success(t("webhooks.deleted"));
      queryClient.invalidateQueries({ queryKey: ["webhooks", "subscriptions"] });
      navigate("/webhooks");
    },
    onError: (err) => toast.error(t("webhooks.deleteFailed"), { description: describe(err) }),
  });

  return (
    <div className="space-y-8">
      <PageHeader
        className="[&_h1]:break-all"
        crumbs={[{ label: t("webhooks.crumb") }, { label: t("webhooks.endpoint"), muted: true }]}
        trailing={sub ? (sub.isActive ? t("webhooks.active").toUpperCase() : t("webhooks.inactive").toUpperCase()) : "—"}
        title={sub?.url ?? t("webhooks.fallbackTitle")}
        description={
          sub
            ? t(sub.events.length === 1 ? "webhooks.subscribedOne" : "webhooks.subscribedMany").replace(
                "{n}",
                String(sub.events.length),
              )
            : subsQuery.isLoading ? t("webhooks.loadingSub") : undefined
        }
        actions={
          <Button variant="ghost" size="sm" onClick={() => navigate("/webhooks")}>
            <ArrowLeft className="mr-1 h-3.5 w-3.5" /> {t("webhooks.subscriptions")}
          </Button>
        }
      />

      {subsQuery.isError && (
        <div className="space-y-3">
        <ErrorBand
          message={
            subsQuery.error instanceof ApiRequestError
              ? subsQuery.error.problem?.detail ?? subsQuery.error.message
              : t("webhooks.loadSubFailed")
          }
        />
        <Button variant="outline" disabled={subsQuery.isFetching || !canView} onClick={() => subsQuery.refetch()}>{t("workbench.retry")}</Button>
        </div>
      )}

      {subsQuery.isLoading && <LoadingRow label={t("webhooks.loadingSub")} />}

      {!subsQuery.isLoading && !sub && !subsQuery.isError && (
        <ErrorBand message={t("webhooks.notFound")} />
      )}

      {sub && (
        <div className="space-y-4">
          <SettingsSection
            icon={Link2}
            title={t("webhooks.endpoint")}
            description={t("webhooks.endpointDesc")}
            footer={(canTest || canDelete) &&
              <div className="flex flex-wrap items-center gap-2">
                {canTest && <Button variant="outline" size="sm" onClick={() => { if (canTest) test.mutate(id); }} disabled={test.isPending || remove.isPending || subsQuery.isFetching}>
                  <Send className="mr-1.5 h-3.5 w-3.5" />
                  {test.isPending ? t("webhooks.sending") : t("webhooks.sendTest")}
                </Button>}
                {canDelete && <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => {
                    if (canDelete && window.confirm(t("webhooks.deleteConfirm").replace("{url}", sub.url))) {
                      remove.mutate(id);
                    }
                  }}
                  disabled={remove.isPending || test.isPending || subsQuery.isFetching}
                  className="text-[var(--color-destructive)] hover:bg-[oklch(from_var(--color-destructive)_l_c_h_/_0.08)]"
                >
                  <Trash2 className="mr-1.5 h-3.5 w-3.5" />
                  {remove.isPending ? t("webhooks.deleting") : t("webhooks.deleteSubscription")}
                </Button>}
              </div>
            }
          >
            <dl className="grid grid-cols-1 gap-y-3 sm:grid-cols-2">
              <FieldRow label={t("webhooks.url")} mono value={sub.url} />
              <FieldRow label={t("webhooks.status")} value={
                <Badge variant={sub.isActive ? "success" : "muted"} className="font-mono uppercase tracking-[0.14em]">
                  {sub.isActive ? t("webhooks.active") : t("webhooks.inactive")}
                </Badge>
              } />
              <FieldRow label={t("webhooks.subscriptionId")} mono value={sub.id} />
              <FieldRow label={t("webhooks.created")} mono value={new Date(sub.createdAtUtc).toLocaleString()} />
            </dl>
          </SettingsSection>

          <SettingsSection
            icon={List}
            title={t("webhooks.events")}
            description={t("webhooks.eventsDesc")}
          >
            <div className="flex flex-wrap gap-1.5">
              {sub.events.map((e) => (
                <code key={e} className="code-chip max-w-full break-all">{e}</code>
              ))}
              {sub.events.length === 0 && (
                <span className="text-sm text-[var(--color-muted-foreground)]">{t("webhooks.noEvents")}</span>
              )}
            </div>
          </SettingsSection>

          <SettingsSection
            title={t("webhooks.deliveries")}
            description={t("webhooks.deliveriesDesc")}
            footer={
              <div className="flex items-center justify-between gap-2">
                <span className="font-mono text-[11px] uppercase tracking-[0.14em] text-[var(--color-muted-foreground)]">
                  {deliveries.data
                    ? t("webhooks.attempts").replace("{n}", String(deliveries.data.totalCount))
                    : "—"}
                </span>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => deliveries.refetch()}
                  disabled={deliveries.isFetching}
                >
                  <RefreshCw className={cn("mr-1.5 h-3.5 w-3.5", deliveries.isFetching && "animate-spin")} />
                  {t("webhooks.refresh")}
                </Button>
              </div>
            }
          >
            {deliveries.isError ? (
              <ErrorBand message={describe(deliveries.error)} />
            ) : deliveries.isLoading ? (
              <LoadingRow label={t("webhooks.loadingDeliveries")} />
            ) : (deliveries.data?.items.length ?? 0) === 0 ? (
              <p className="text-sm text-[var(--color-muted-foreground)]">
                {t(canTest ? "webhooks.noDeliveries" : "webhooks.noDeliveriesReadOnly")}
              </p>
            ) : (
              <>
                <ol className="-mx-5 divide-y divide-[var(--color-border)] border-y border-[var(--color-border)]">
                  {(deliveries.data!.items ?? []).map((d) => (
                    <DeliveryRow key={d.id} delivery={d} />
                  ))}
                </ol>
                {deliveries.data!.totalPages > 1 && (
                  <div className="mt-4">
                    <Pagination
                      page={deliveries.data!.pageNumber}
                      totalPages={deliveries.data!.totalPages}
                      totalCount={deliveries.data!.totalCount}
                      shown={deliveries.data!.items.length}
                      fetching={deliveries.isFetching}
                      hasPrev={deliveries.data!.hasPrevious}
                      hasNext={deliveries.data!.hasNext}
                      onPrev={() => setDeliveryPage((p) => Math.max(1, p - 1))}
                      onNext={() => setDeliveryPage((p) => p + 1)}
                      noun="deliveries"
                    />
                  </div>
                )}
              </>
            )}
          </SettingsSection>
        </div>
      )}
    </div>
  );
}

function DeliveryRow({ delivery }: { delivery: WebhookDeliveryDto }) {
  const t = useT();
  const Icon = delivery.success ? CheckCircle2 : XCircle;
  const tone = delivery.success ? "text-[var(--color-success)]" : "text-[var(--color-destructive)]";
  return (
    <li className="flex flex-wrap items-center gap-3 px-5 py-2.5 sm:grid sm:grid-cols-[auto_8rem_auto_1fr_auto_auto]">
      <Icon className={cn("h-4 w-4", tone)} />
      <span className="font-mono text-[11px] tabular-nums text-[var(--color-muted-foreground)]">
        {formatTimestamp(delivery.attemptedAtUtc)}
      </span>
      <code className="code-chip max-w-full break-all">{delivery.eventType}</code>
      <span className="truncate text-[11.5px] text-[var(--color-muted-foreground)]">
        {delivery.errorMessage ?? (delivery.success ? t("webhooks.ok") : t("webhooks.failed"))}
      </span>
      <Badge
        variant={delivery.success ? "success" : "danger"}
        className="font-mono uppercase tracking-[0.14em]"
      >
        {t("webhooks.http").replace("{code}", String(delivery.httpStatusCode || "—"))}
      </Badge>
      <span className="font-mono text-[10.5px] uppercase tracking-[0.14em] text-[var(--color-muted-foreground)]">
        {t("webhooks.try").replace("{n}", String(delivery.attemptCount))}
      </span>
    </li>
  );
}

function FieldRow({ label, value, mono }: { label: string; value: React.ReactNode; mono?: boolean }) {
  return (
    <div className="grid min-w-0 grid-cols-1 items-baseline gap-1 sm:grid-cols-[8rem_1fr] sm:gap-4">
      <dt className="font-mono text-[10.5px] uppercase tracking-[0.14em] text-[var(--color-muted-foreground)]">{label}</dt>
      <dd className={cn("min-w-0 break-words text-sm", mono && "font-mono text-[0.8125rem]")}>
        {value}
      </dd>
    </div>
  );
}

function formatTimestamp(value: string): string {
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
}

function describe(err: unknown): string {
  if (err instanceof ApiRequestError) return err.problem?.detail ?? err.problem?.title ?? err.message;
  if (err instanceof Error) return err.message;
  return String(err);
}
