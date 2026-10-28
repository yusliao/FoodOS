import { useRef, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { createCustomer, type CreateCustomerInput } from "@/api/customers";
import { assignCustomerLevel, customerLevelKeys, getCustomerLevels } from "@/api/catalog";
import { useAuth } from "@/auth/use-auth";
import { CatalogPermissions } from "@/lib/permissions";
import { LevelSelect } from "./customer-levels";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Field } from "@/components/list";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogBody, DialogFooter } from "@/components/ui/dialog";
import { useT } from "@/i18n/locale-provider";
import { describe } from "./request-error";
import { newUuid } from "@/lib/uuid";

export function CustomerCreate({ onClose }: { onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [tenant, setTenant] = useState("");
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [creditHold, setCreditHold] = useState(false);
  const [levelId, setLevelId] = useState("");
  const { user } = useAuth();
  const granted = user?.permissions ?? [];
  const canAssignLevel = granted.includes(CatalogPermissions.PriceLists.View) && granted.includes(CatalogPermissions.PriceLists.Update);
  const levels = useQuery({ queryKey: customerLevelKeys.levels, queryFn: ({ signal }) => getCustomerLevels(signal), enabled: canAssignLevel });
  // Keep the key for an unchanged payload after a timeout; edits start a new request.
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const mutation = useMutation({
    // 重试时同一个幂等键会拿回同一个客户 ID，等级分配失败后重新提交不会重复建客户
    mutationFn: async ({ input, key, level }: { input: CreateCustomerInput; key: string; level: string }) => {
      const id = await createCustomer(input, key);
      if (level) {
        try {
          await assignCustomerLevel(id, level);
        } catch (err) {
          await cache.invalidateQueries({ queryKey: ["customers"] });
          throw new Error(`${t("partners.levelSaveFailed")} ${describe(err, "")}`.trim(), { cause: err });
        }
      }
    },
    onSuccess: async () => {
      await Promise.all([
        cache.invalidateQueries({ queryKey: ["customers"] }),
        cache.invalidateQueries({ queryKey: customerLevelKeys.levels }),
      ]);
      toast.success(t("partners.customerCreated"));
      onClose();
    },
  });
  const tenantValid = tenant.trim().length > 0 && tenant.trim().toLowerCase() !== "root";
  function submit(event: FormEvent) {
    event.preventDefault();
    if (mutation.isPending || !tenantValid || !code.trim() || !name.trim()) return;
    const input = { customerTenantId: tenant.trim(), code: code.trim(), name: name.trim(), creditHold };
    const body = JSON.stringify(input);
    if (attempt.current?.body !== body) attempt.current = { body, key: newUuid() };
    mutation.mutate({ input, key: attempt.current.key, level: canAssignLevel ? levelId : "" });
  }
  return <Dialog open onOpenChange={open => { if (!open && !mutation.isPending) onClose(); }}>
    <DialogContent><DialogHeader><DialogTitle>{t("partners.newCustomer")}</DialogTitle>
      <DialogDescription>{t("partners.tenantHint")}</DialogDescription></DialogHeader>
      <form onSubmit={submit}><DialogBody className="space-y-4">
        <fieldset disabled={mutation.isPending} className="space-y-4">
          <Field id="customer-tenant" label={t("partners.tenant")} required>
            <Input id="customer-tenant" required value={tenant} onChange={e => setTenant(e.target.value)} />
          </Field>
          {tenant.trim().toLowerCase() === "root" && <p role="alert">{t("partners.notRoot")}</p>}
          <Field id="customer-code" label={t("partners.code")} required><Input id="customer-code" required maxLength={16} value={code} onChange={e => setCode(e.target.value)} /></Field>
          <Field id="customer-name" label={t("partners.name")} required><Input id="customer-name" required maxLength={128} value={name} onChange={e => setName(e.target.value)} /></Field>
          {canAssignLevel && levels.isSuccess && levels.data.length > 0 && <LevelSelect id="customer-level" value={levelId} onChange={setLevelId} levels={levels.data} />}
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={creditHold} onChange={e => setCreditHold(e.target.checked)} />{t("partners.creditHold")}</label>
        </fieldset>
        {mutation.isError && <p role="alert" className="text-sm text-[var(--color-destructive)]">{describe(mutation.error, t("partners.requestFailed"))}</p>}
      </DialogBody><DialogFooter>
        <Button type="button" variant="outline" disabled={mutation.isPending} onClick={onClose}>{t("chrome.cancel")}</Button>
        <Button type="submit" disabled={mutation.isPending || !tenantValid || !code.trim() || !name.trim()}>{t(mutation.isPending ? "partners.saving" : "partners.create")}</Button>
      </DialogFooter></form>
    </DialogContent>
  </Dialog>;
}
