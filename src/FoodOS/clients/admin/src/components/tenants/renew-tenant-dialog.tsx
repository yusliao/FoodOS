import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { CalendarClock } from "lucide-react";
import { toast } from "sonner";
import { renewTenant } from "@/api/tenants";
import { getPlans, planTermPrice } from "@/api/billing";
import { Button } from "@/components/ui/button";
import { ErrorBand, Field, Select, type SelectOption } from "@/components/list";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { ApiRequestError } from "@/lib/api-client";
import { useT } from "@/i18n/locale-provider";
import { useAuth } from "@/auth/use-auth";
import { BillingPermissions, MultitenancyPermissions } from "@/lib/permissions";

function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(amount);
  } catch {
    return `${amount.toFixed(2)} ${currency}`;
  }
}

function formatDate(value?: string | null): string {
  if (!value) return "—";
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? value : d.toLocaleDateString();
}

/**
 * Renew or change a tenant's plan. Renewing the same plan extends validity by one term; choosing a
 * different plan switches the tenant from the renewal forward. The server issues the term invoice.
 */
export function RenewTenantDialog({
  open,
  onOpenChange,
  tenantId,
  currentPlanKey,
  validUpto,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  tenantId: string;
  currentPlanKey?: string | null;
  validUpto?: string;
}) {
  const t = useT();
  const { user } = useAuth();
  const canRenew = !!user?.permissions.includes(MultitenancyPermissions.Tenants.View)
    && user.permissions.includes(MultitenancyPermissions.Tenants.UpgradeSubscription);
  const canViewPlans = !!user?.permissions.includes(BillingPermissions.View);
  const queryClient = useQueryClient();
  const [planKey, setPlanKey] = useState<string>("");

  const plansQuery = useQuery({
    queryKey: ["billing", "plans", "active"],
    queryFn: ({ signal }) => getPlans(false, signal),
    enabled: open && canRenew && canViewPlans,
  });

  // Default the selection to the tenant's current plan once plans (and the current key) are known.
  useEffect(() => {
    if (!open) return;
    setPlanKey(currentPlanKey ?? "");
  }, [open, currentPlanKey, tenantId]);

  const options: SelectOption[] = (plansQuery.data ?? []).filter(p => p.isActive).map((p) => ({
    value: p.key,
    label: p.key === currentPlanKey ? t("tenants.currentPlan").replace("{name}", p.name) : p.name,
    hint: `${t(p.interval === "Yearly" ? "billing.yearly" : "billing.monthly")} · ${formatMoney(planTermPrice(p), p.currency)}`,
  }));

  const mutation = useMutation({
    mutationFn: ({ id, key }: { id: string; key: string | null }) => renewTenant(id, key),
    onSuccess: async (result) => {
      toast.success(
        result.planChanged
          ? t("tenants.planChanged").replace("{plan}", result.planKey)
          : t("tenants.tenantRenewed"),
        { description: t("tenants.renewedUntil").replace("{date}", formatDate(result.validUpto)) },
      );
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["tenant", tenantId] }),
        queryClient.invalidateQueries({ queryKey: ["tenants"] }),
        queryClient.invalidateQueries({ queryKey: ["billing", "invoices"] }),
      ]);
      onOpenChange(false);
    },
    onError: (err) => {
      const detail =
        err instanceof ApiRequestError
          ? err.problem?.detail ?? err.problem?.title ?? err.message
          : (err as Error).message;
      toast.error(t("tenants.renewFailed"), { description: detail });
    },
  });

  const planChanged = canViewPlans && !!planKey && planKey !== currentPlanKey;
  const canSubmit = canRenew && !mutation.isPending && (!canViewPlans || (
    plansQuery.isSuccess && !plansQuery.isFetching && options.some(option => option.value === planKey)
  ));

  return (
    <Dialog open={open && canRenew} onOpenChange={(next) => { if (!mutation.isPending) onOpenChange(next); }}>
      <DialogContent size="md">
        <DialogHeader>
          <div className="flex items-center gap-3">
            <span
              aria-hidden
              className="grid h-9 w-9 shrink-0 place-items-center rounded-xl
                bg-[oklch(from_var(--color-primary)_l_c_h_/_0.12)] text-[var(--color-primary)]
                ring-1 ring-inset ring-[oklch(from_var(--color-primary)_l_c_h_/_0.18)]"
            >
              <CalendarClock className="h-[18px] w-[18px]" />
            </span>
            <DialogTitle className="text-[16px]">{t("tenants.renewSub")}</DialogTitle>
          </div>
          <DialogDescription className="mt-1">
            {t("tenants.renewDesc").replace("{date}", formatDate(validUpto))}
          </DialogDescription>
        </DialogHeader>

        <DialogBody className="space-y-4">
          {!canViewPlans ? <p className="text-sm">{t("tenants.renewCurrentOnly")}</p> : <>
          {plansQuery.isError && <div className="space-y-2">
            <ErrorBand message={plansQuery.error instanceof ApiRequestError ? plansQuery.error.problem?.detail ?? plansQuery.error.message : t("billing.loadPlansFailed")} />
            <Button variant="outline" disabled={plansQuery.isFetching} onClick={() => plansQuery.refetch()}>{t("workbench.retry")}</Button>
          </div>}
          <Field
            id="renew-plan"
            label={t("tenants.plan")}
            required
            hint={
              planChanged
                ? t("tenants.switchPlanHint")
                : t("tenants.samePlanHint")
            }
          >
            <Select
              id="renew-plan"
              value={planKey}
              onValueChange={setPlanKey}
              options={options}
              emptyLabel={plansQuery.isLoading ? t("tenants.loadingPlans") : t("tenants.selectRenewPlan")}
              disabled={mutation.isPending || plansQuery.isFetching || plansQuery.isError || options.length === 0}
            />
          </Field>
          {plansQuery.isSuccess && options.length === 0 && <p className="text-sm">{t("tenants.noActivePlans")}</p>}
          </>}
        </DialogBody>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={mutation.isPending}>
            {t("chrome.cancel")}
          </Button>
          <Button type="button" onClick={() => { if (canSubmit) mutation.mutate({ id: tenantId, key: canViewPlans ? planKey : null }); }} disabled={!canSubmit}>
            {mutation.isPending ? t("tenants.renewing") : planChanged ? t("tenants.changeAndRenew") : t("tenants.renew")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
