import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Pencil, Plus, Tag } from "lucide-react";
import { getPlans, planTermPrice, type BillingPlanDto } from "@/api/billing";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { StatStrip, Stat, SettingsSection } from "@/components/list";
import { PlanFormDialog } from "@/components/billing/plan-form-dialog";
import { ApiRequestError } from "@/lib/api-client";
import { useAuth } from "@/auth/use-auth";
import { BillingPermissions } from "@/lib/permissions";
import { useT } from "@/i18n/locale-provider";

function formatMoney(amount: number, currency: string) {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(amount);
  } catch {
    return `${amount.toFixed(2)} ${currency}`;
  }
}

function formatOverageRates(rates: BillingPlanDto["overageRates"], currency: string) {
  const entries = Object.entries(rates).filter(([, v]) => v && v > 0);
  if (entries.length === 0) return "—";
  return entries
    .map(([resource, rate]) => `${resource} ${formatMoney(rate ?? 0, currency)}`)
    .join(" · ");
}

function describe(
  err: unknown,
  t: (key: string, fallback?: string) => string,
): string {
  if (err instanceof ApiRequestError) return err.problem?.detail ?? err.problem?.title ?? err.message;
  if (err instanceof Error) return err.message;
  return t("billing.loadPlansFailed");
}

export function PlansListPage() {
  const t = useT();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingPlan, setEditingPlan] = useState<BillingPlanDto | undefined>(undefined);
  const { user: currentUser } = useAuth();
  const canManageBilling = (currentUser?.permissions ?? []).includes(BillingPermissions.Manage);

  const openCreate = () => {
    setEditingPlan(undefined);
    setDialogOpen(true);
  };
  const openEdit = (plan: BillingPlanDto) => {
    setEditingPlan(plan);
    setDialogOpen(true);
  };

  const query = useQuery({
    queryKey: ["billing", "plans", { includeInactive: true }],
    queryFn: () => getPlans(true),
  });

  const plans = useMemo<BillingPlanDto[]>(() => query.data ?? [], [query.data]);

  const totals = useMemo(() => {
    if (plans.length === 0) {
      return { count: 0, active: 0, averagePrice: 0, currency: "USD" };
    }
    const active = plans.filter((p) => p.isActive).length;
    const sum = plans.reduce((acc, p) => acc + p.monthlyBasePrice, 0);
    return {
      count: plans.length,
      active,
      averagePrice: sum / plans.length,
      currency: plans[0].currency,
    };
  }, [plans]);

  return (
    <div className="space-y-6">
      <StatStrip cols={3}>
        <Stat
          label={t("billing.plans")}
          value={query.isLoading ? <Skeleton className="h-7 w-16" /> : totals.count}
          hint={t("billing.activeHint").replace("{n}", String(totals.active))}
        />
        <Stat
          label={t("billing.active")}
          value={query.isLoading ? <Skeleton className="h-7 w-16" /> : totals.active}
          hint={
            totals.count - totals.active > 0
              ? t("billing.inactiveHint").replace("{n}", String(totals.count - totals.active))
              : t("billing.allActive")
          }
        />
        <Stat
          label={t("billing.averageBase")}
          value={
            query.isLoading ? (
              <Skeleton className="h-7 w-24" />
            ) : (
              formatMoney(totals.averagePrice, totals.currency)
            )
          }
          hint={t("billing.monthlyFeeHint")}
        />
      </StatStrip>

      <SettingsSection
        icon={Tag}
        title={t("billing.allPlans")}
        description={t("billing.allPlansDesc")}
        footer={
          canManageBilling ? (
            <Button onClick={openCreate}>
              <Plus className="mr-1 h-4 w-4" /> {t("billing.newPlan")}
            </Button>
          ) : undefined
        }
      >
        {query.isError && (
          <div className="mb-4 rounded-md border border-[oklch(from_var(--color-destructive)_l_c_h_/_0.30)] bg-[oklch(from_var(--color-destructive)_l_c_h_/_0.05)] px-4 py-3 text-sm text-[var(--color-destructive)]">
            {describe(query.error, t)}
          </div>
        )}

        {query.isLoading ? (
          <ul className="-mx-5 divide-y divide-[var(--color-border)] border-t border-[var(--color-border)]">
            {Array.from({ length: 3 }).map((_, i) => (
              <li key={i} className="px-5 py-5">
                <Skeleton className="h-5 w-1/3" />
                <Skeleton className="mt-2 h-3 w-1/2" />
              </li>
            ))}
          </ul>
        ) : plans.length === 0 ? (
          <div className="py-10 text-center text-sm text-[var(--color-muted-foreground)]">
            {t("billing.emptyPlans")}
          </div>
        ) : (
          <ul className="-mx-5 border-t border-[var(--color-border)]">
            {plans.map((plan, i) => (
              <li
                key={plan.id}
                className="fsh-enter grid grid-cols-[1fr_auto] items-center gap-x-6 gap-y-1 border-b border-[var(--color-border)] last:border-b-0 px-5 py-4 transition-colors hover:bg-[var(--color-muted)]"
                style={{ animationDelay: `${Math.min(i, 6) * 30}ms` }}
              >
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <code className="rounded bg-[var(--color-surface-2)] px-1.5 py-0.5 font-mono text-[11px] font-medium tracking-tight">
                      {plan.key}
                    </code>
                    <span className="font-display text-base font-semibold">{plan.name}</span>
                    <Badge variant="outline">
                      {plan.interval === "Yearly" ? t("billing.yearly") : t("billing.monthly")}
                    </Badge>
                    {plan.isActive ? (
                      <Badge variant="success">{t("billing.active")}</Badge>
                    ) : (
                      <Badge variant="muted">{t("billing.inactive")}</Badge>
                    )}
                  </div>
                  <div className="mt-1 font-mono text-[11px] tracking-tight text-[var(--color-muted-foreground)]">
                    {t("billing.currencyOverage")
                      .replace("{currency}", plan.currency)
                      .replace("{overage}", formatOverageRates(plan.overageRates, plan.currency))}
                  </div>
                </div>

                <div className="flex items-center gap-4">
                  <div className="text-right">
                    <div className="text-display text-lg font-semibold leading-none tabular-nums">
                      {formatMoney(planTermPrice(plan), plan.currency)}
                    </div>
                    <div className="mt-1 font-mono text-[10.5px] uppercase tracking-[0.18em] text-[var(--color-muted-foreground)]">
                      {plan.interval === "Yearly" ? t("billing.perYear") : t("billing.perMonth")}
                    </div>
                  </div>
                  {canManageBilling && (
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label={t("billing.editPlan").replace("{name}", plan.name)}
                      onClick={() => openEdit(plan)}
                    >
                      <Pencil className="h-4 w-4" />
                    </Button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </SettingsSection>

      <PlanFormDialog open={dialogOpen} onOpenChange={setDialogOpen} plan={editingPlan} />
    </div>
  );
}
