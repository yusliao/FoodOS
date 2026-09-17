import { useEffect, useMemo, useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { z } from "zod";
import { CreditCard, Gauge } from "lucide-react";
import { toast } from "sonner";
import {
  createPlan,
  updatePlan,
  type BillingPlanDto,
  type PlanInterval,
  type QuotaResource,
} from "@/api/billing";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Field, Select, type SelectOption } from "@/components/list";
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
import { BillingPermissions } from "@/lib/permissions";

const PLAN_KEY_PATTERN = /^[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$/;

type Translate = (key: string, fallback?: string) => string;

function makeMoneySchemas(t: Translate) {
  const nonNegative = t("billing.nonNegative");
  return {
    requiredNonNegative: z
      .string()
      .trim()
      .min(1, t("billing.required"))
      .refine((v) => Number.isFinite(Number(v)) && Number(v) >= 0, nonNegative),
    optionalNonNegative: z
      .string()
      .trim()
      .refine((v) => v === "" || (Number.isFinite(Number(v)) && Number(v) >= 0), nonNegative),
  };
}

type OverageState = Record<string, string>;

function toOverageNumbers(
  state: OverageState,
  resources: { key: QuotaResource }[],
): Record<string, number> | null {
  const out: Record<string, number> = {};
  let any = false;
  for (const { key } of resources) {
    const raw = state[key];
    if (raw === undefined || raw.trim() === "") continue;
    const n = Number(raw);
    if (!Number.isFinite(n) || n < 0) continue;
    out[key] = n;
    any = true;
  }
  return any ? out : null;
}

function fieldError(schema: z.ZodTypeAny, value: string): string | undefined {
  const result = schema.safeParse(value);
  return result.success ? undefined : result.error.issues[0]?.message;
}

function describe(err: unknown, fallback: string): string {
  if (err instanceof ApiRequestError) return err.problem?.detail ?? err.problem?.title ?? err.message;
  if (err instanceof Error) return err.message;
  return fallback;
}

function SectionLabel({
  icon: Icon,
  title,
  description,
}: {
  icon: React.ComponentType<{ className?: string }>;
  title: string;
  description: string;
}) {
  return (
    <div className="flex items-start gap-2.5 pb-1">
      <span
        aria-hidden
        className="mt-0.5 grid h-6 w-6 shrink-0 place-items-center rounded-md bg-[var(--color-accent)] text-[var(--color-muted-foreground)]"
      >
        <Icon className="h-3.5 w-3.5" />
      </span>
      <div className="min-w-0">
        <p className="text-[12.5px] font-semibold text-[var(--color-foreground)]">{title}</p>
        <p className="text-[11.5px] leading-relaxed text-[var(--color-muted-foreground)]">{description}</p>
      </div>
    </div>
  );
}

export function PlanFormDialog({
  open,
  onOpenChange,
  plan,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  plan?: BillingPlanDto;
}) {
  const t = useT();
  const { user } = useAuth();
  const canManage = !!user?.permissions.includes(BillingPermissions.View)
    && user.permissions.includes(BillingPermissions.Manage);
  const queryClient = useQueryClient();
  const isEdit = !!plan;
  const { requiredNonNegative, optionalNonNegative } = useMemo(() => makeMoneySchemas(t), [t]);

  const intervalOptions: SelectOption<PlanInterval>[] = useMemo(
    () => [
      { value: "Monthly", label: t("billing.monthly"), hint: t("billing.intervalMonthlyHint") },
      { value: "Yearly", label: t("billing.yearly"), hint: t("billing.intervalYearlyHint") },
    ],
    [t],
  );

  const overageResources: { key: QuotaResource; label: string; placeholder: string }[] = useMemo(
    () => [
      { key: "ApiCalls", label: t("billing.resApiCalls"), placeholder: "0.0010" },
      { key: "StorageBytes", label: t("billing.resStorageBytes"), placeholder: "0.00000001" },
      { key: "Users", label: t("billing.resUsers"), placeholder: "5.00" },
      { key: "ActiveFeatureFlags", label: t("billing.resFeatureFlags"), placeholder: "1.00" },
    ],
    [t],
  );

  const [key, setKey] = useState("");
  const [name, setName] = useState("");
  const [currency, setCurrency] = useState("USD");
  const [monthlyBasePrice, setMonthlyBasePrice] = useState("");
  const [interval, setInterval] = useState<PlanInterval>("Monthly");
  const [annualPrice, setAnnualPrice] = useState("");
  const [overage, setOverage] = useState<OverageState>({});

  useEffect(() => {
    if (!open) return;
    setKey(plan?.key ?? "");
    setName(plan?.name ?? "");
    setCurrency(plan?.currency ?? "USD");
    setMonthlyBasePrice(plan ? String(plan.monthlyBasePrice) : "");
    setInterval(plan?.interval === "Yearly" ? "Yearly" : "Monthly");
    setAnnualPrice(plan?.annualPrice != null ? String(plan.annualPrice) : "");
    const next: OverageState = {};
    for (const [resource, rate] of Object.entries(plan?.overageRates ?? {})) {
      if (rate !== undefined && rate !== null) next[resource] = String(rate);
    }
    setOverage(next);
  }, [open, plan]);

  const keyInvalid = !isEdit && !PLAN_KEY_PATTERN.test(key.trim());
  const nameInvalid = !name.trim() || name.trim().length > 128;
  const currencyInvalid = !isEdit && currency.trim().length !== 3;
  const priceNum = Number(monthlyBasePrice);
  const priceError =
    monthlyBasePrice.length > 0 ? fieldError(requiredNonNegative, monthlyBasePrice) : undefined;
  const annualNum = Number(annualPrice);
  const annualError = interval === "Yearly" ? fieldError(optionalNonNegative, annualPrice) : undefined;
  const annualPricePayload = interval === "Yearly" && annualPrice.trim().length > 0 ? annualNum : null;

  const overageErrors = useMemo(() => {
    const out: Partial<Record<string, string>> = {};
    for (const { key: resKey } of overageResources) {
      const err = fieldError(optionalNonNegative, overage[resKey] ?? "");
      if (err) out[resKey] = err;
    }
    return out;
  }, [overage, optionalNonNegative, overageResources]);
  const hasOverageError = Object.keys(overageErrors).length > 0;
  const pricingInvalid =
    !!fieldError(requiredNonNegative, monthlyBasePrice) || !!annualError || hasOverageError;

  const onClose = () => onOpenChange(false);

  const createMutation = useMutation({
    mutationFn: createPlan,
    onSuccess: () => {
      toast.success(t("billing.createdPlan").replace("{name}", name));
      queryClient.invalidateQueries({ queryKey: ["billing", "plans"] });
      onClose();
    },
    onError: (err) =>
      toast.error(t("billing.createFailed"), { description: describe(err, t("billing.createFailedBody")) }),
  });

  const updateMutation = useMutation({
    mutationFn: updatePlan,
    onSuccess: () => {
      toast.success(t("billing.updatedPlan").replace("{name}", name));
      queryClient.invalidateQueries({ queryKey: ["billing", "plans"] });
      onClose();
    },
    onError: (err) =>
      toast.error(t("billing.updateFailed"), { description: describe(err, t("billing.updateFailedBody")) }),
  });

  const pending = createMutation.isPending || updateMutation.isPending;

  const onSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!canManage || pending || pricingInvalid || nameInvalid || currencyInvalid || keyInvalid) return;
    const overageRates = toOverageNumbers(overage, overageResources);

    if (isEdit && plan) {
      updateMutation.mutate({
        planId: plan.id,
        name: name.trim(),
        monthlyBasePrice: priceNum,
        overageRates,
        interval,
        annualPrice: annualPricePayload,
      });
      return;
    }
    if (keyInvalid) return;
    createMutation.mutate({
      key: key.trim(),
      name: name.trim(),
      currency: currency.trim().toUpperCase(),
      monthlyBasePrice: priceNum,
      overageRates,
      interval,
      annualPrice: annualPricePayload,
    });
  };

  return (
    <Dialog open={open && canManage} onOpenChange={(next) => { if (!pending) onOpenChange(next); }}>
      <DialogContent size="lg">
        <DialogHeader>
          <div className="flex items-center gap-3">
            <span
              aria-hidden
              className="grid h-9 w-9 shrink-0 place-items-center rounded-xl
                bg-[oklch(from_var(--color-primary)_l_c_h_/_0.12)] text-[var(--color-primary)]
                ring-1 ring-inset ring-[oklch(from_var(--color-primary)_l_c_h_/_0.18)]"
            >
              <CreditCard className="h-[18px] w-[18px]" />
            </span>
            <DialogTitle className="text-[16px]">
              {isEdit ? t("billing.editPlanTitle") : t("billing.newPlanTitle")}
            </DialogTitle>
          </div>
          <DialogDescription className="mt-1">
            {isEdit ? t("billing.editPlanDesc") : t("billing.newPlanDesc")}
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={onSubmit}>
          <fieldset disabled={pending} className="min-w-0">
          <DialogBody className="space-y-6">
            <div className="space-y-3">
              <SectionLabel
                icon={CreditCard}
                title={t("billing.planDetails")}
                description={t("billing.planDetailsDesc")}
              />
              <div className="h-px bg-[var(--color-border)] opacity-60" />
              <div className="grid gap-4 sm:grid-cols-2">
                <Field
                  id="pf-key"
                  label={t("billing.key")}
                  hint={t("billing.keyHint")}
                  required={!isEdit}
                  error={key.length > 0 && keyInvalid ? t("billing.invalidSlug") : undefined}
                >
                  <Input
                    id="pf-key"
                    maxLength={64}
                    value={key}
                    onChange={(e) => setKey(e.target.value)}
                    placeholder="pro"
                    className="font-mono"
                    disabled={isEdit}
                    autoComplete="off"
                  />
                </Field>
                <Field id="pf-name" label={t("billing.displayName")} required error={name.length > 0 && nameInvalid ? t("billing.planNameConstraint") : undefined}>
                  <Input id="pf-name" maxLength={128} value={name} onChange={(e) => setName(e.target.value)} placeholder="Pro" />
                </Field>
                <Field id="pf-currency" label={t("billing.currency")} hint={t("billing.currencyHint")} required={!isEdit} error={currencyInvalid ? t("billing.planCurrencyConstraint") : undefined}>
                  <Input
                    id="pf-currency"
                    maxLength={3}
                    value={currency}
                    onChange={(e) => setCurrency(e.target.value.toUpperCase())}
                    placeholder="USD"
                    className="font-mono"
                    disabled={isEdit}
                    autoComplete="off"
                  />
                </Field>
                <Field
                  id="pf-monthlyBasePrice"
                  label={t("billing.monthlyBase")}
                  hint={t("billing.monthlyBaseHint")}
                  required
                  error={priceError}
                >
                  <Input
                    id="pf-monthlyBasePrice"
                    value={monthlyBasePrice}
                    onChange={(e) => setMonthlyBasePrice(e.target.value)}
                    inputMode="decimal"
                    placeholder="29.00"
                  />
                </Field>
                <Field id="pf-interval" label={t("billing.billingInterval")} required>
                  <Select<PlanInterval>
                    id="pf-interval"
                    value={interval}
                    onValueChange={(v) => setInterval(v === "Yearly" ? "Yearly" : "Monthly")}
                    options={intervalOptions}
                  />
                </Field>
                {interval === "Yearly" && (
                  <Field
                    id="pf-annualPrice"
                    label={t("billing.annualPrice")}
                    hint={t("billing.annualPriceHint")}
                    error={annualError}
                  >
                    <Input
                      id="pf-annualPrice"
                      value={annualPrice}
                      onChange={(e) => setAnnualPrice(e.target.value)}
                      inputMode="decimal"
                      placeholder={monthlyBasePrice ? String(Number(monthlyBasePrice) * 12) : "290.00"}
                    />
                  </Field>
                )}
              </div>
            </div>

            <div className="space-y-3">
              <SectionLabel
                icon={Gauge}
                title={t("billing.overageRates")}
                description={t("billing.overageRatesDesc")}
              />
              <div className="h-px bg-[var(--color-border)] opacity-60" />
              <div className="grid gap-4 sm:grid-cols-2">
                {overageResources.map((res) => (
                  <Field
                    key={res.key}
                    id={`pf-overage-${res.key}`}
                    label={res.label}
                    error={overageErrors[res.key]}
                  >
                    <Input
                      id={`pf-overage-${res.key}`}
                      value={overage[res.key] ?? ""}
                      onChange={(e) => setOverage((s) => ({ ...s, [res.key]: e.target.value }))}
                      inputMode="decimal"
                      placeholder={res.placeholder}
                    />
                  </Field>
                ))}
              </div>
            </div>
          </DialogBody>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={pending}>
              {t("chrome.cancel")}
            </Button>
            <Button type="submit" disabled={!canManage || pending || keyInvalid || nameInvalid || currencyInvalid || pricingInvalid}>
              {pending ? t("billing.saving") : isEdit ? t("billing.saveChanges") : t("billing.createPlan")}
            </Button>
          </DialogFooter>
          </fieldset>
        </form>
      </DialogContent>
    </Dialog>
  );
}
