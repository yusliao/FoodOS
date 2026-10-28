import { useRef, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  createCustomerLevel,
  customerLevelKeys,
  getCustomerLevels,
  updateCustomerLevel,
  type CustomerLevelDto,
  type CustomerLevelInput,
} from "@/api/catalog";
import { useAuth } from "@/auth/use-auth";
import { ErrorBand, Field, LoadingRow } from "@/components/list";
import { Button } from "@/components/ui/button";
import {
  Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n/locale-provider";
import { CatalogPermissions } from "@/lib/permissions";
import { newUuid } from "@/lib/uuid";
import { describe } from "./request-error";
import { discountLabel, payPercent } from "./level-format";

/** Customer pricing levels: each level has a discount and optional per-product prices (its price lists). */
export function CustomerLevelsSection() {
  const t = useT();
  const { user } = useAuth();
  const granted = user?.permissions ?? [];
  const canCreate = granted.includes(CatalogPermissions.PriceLists.Create);
  const canUpdate = granted.includes(CatalogPermissions.PriceLists.Update);
  const [editing, setEditing] = useState<CustomerLevelDto | "new" | null>(null);
  const levels = useQuery({ queryKey: customerLevelKeys.levels, queryFn: ({ signal }) => getCustomerLevels(signal) });

  return <section aria-labelledby="customer-levels-title" className="space-y-4 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-5">
    <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
      <div>
        <h2 id="customer-levels-title" className="text-lg font-semibold">{t("levels.title")}</h2>
        <p className="max-w-2xl text-sm text-[var(--color-muted-foreground)]">{t("levels.description")}</p>
      </div>
      {canCreate && <Button className="shrink-0" onClick={() => setEditing("new")}>{t("levels.newLevel")}</Button>}
    </div>
    {levels.isPending && <LoadingRow label={t("common.loading")} />}
    {levels.isError && <div className="space-y-2"><ErrorBand message={describe(levels.error, t("levels.loadFailed"))} /><Button variant="outline" onClick={() => void levels.refetch()}>{t("workbench.retry")}</Button></div>}
    {levels.isSuccess && levels.data.length === 0 && <p role="status" className="text-sm">{t("levels.empty")}</p>}
    {levels.isSuccess && levels.data.length > 0 && <div className="overflow-x-auto">
      <table className="w-full min-w-[32rem] text-left text-sm">
        <thead><tr className="border-b border-[var(--color-border)]">
          <th className="py-2">{t("levels.code")}</th><th>{t("levels.name")}</th><th>{t("levels.discount")}</th><th>{t("levels.customerCount")}</th>{canUpdate && <th><span className="sr-only">{t("partners.actions")}</span></th>}
        </tr></thead>
        <tbody>{levels.data.map(level => <tr key={level.id} className="border-b border-[var(--color-border)]">
          <td className="py-2 font-mono">{level.code}</td>
          <td>{level.name}</td>
          <td>{discountLabel(t, level.discountPercent)}</td>
          <td>{level.customerCount}</td>
          {canUpdate && <td className="text-right"><Button size="sm" variant="outline" aria-label={`${t("levels.editLevel")} ${level.name}`} onClick={() => setEditing(level)}>{t("levels.editLevel")}</Button></td>}
        </tr>)}</tbody>
      </table>
    </div>}
    {editing && <CustomerLevelDialog level={editing === "new" ? null : editing} onClose={() => setEditing(null)} />}
  </section>;
}

function CustomerLevelDialog({ level, onClose }: { level: CustomerLevelDto | null; onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [code, setCode] = useState(level?.code ?? "");
  const [name, setName] = useState(level?.name ?? "");
  // 界面按「折」填：90 = 9 折，100 = 不打折；后端存的是优惠百分比（100 - 折扣）
  const [discount, setDiscount] = useState(level ? String(payPercent(level.discountPercent)) : "100");
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const mutation = useMutation({
    mutationFn: async ({ input, key }: { input: CustomerLevelInput; key: string }) => {
      if (level) await updateCustomerLevel(level.id, input);
      else await createCustomerLevel(input, key);
    },
    onSuccess: async () => {
      // 等级变了，报价也可能变，价目表与报价缓存一起作废
      await Promise.all([
        cache.invalidateQueries({ queryKey: customerLevelKeys.levels }),
        cache.invalidateQueries({ queryKey: ["catalog", "price-lists"] }),
      ]);
      toast.success(t(level ? "levels.saved" : "levels.created"));
      onClose();
    },
  });
  const discountValue = Number(discount);
  const valid = code.trim().length > 0 && name.trim().length > 0 && discount.trim() !== ""
    && Number.isFinite(discountValue) && discountValue > 0 && discountValue <= 100;

  function submit(event: FormEvent) {
    event.preventDefault();
    if (!valid || mutation.isPending) return;
    const input = { code: code.trim(), name: name.trim(), discountPercent: Math.round((100 - discountValue) * 100) / 100 };
    const body = JSON.stringify(input);
    if (attempt.current?.body !== body) attempt.current = { body, key: newUuid() };
    mutation.mutate({ input, key: attempt.current.key });
  }

  return <Dialog open onOpenChange={open => { if (!open && !mutation.isPending) onClose(); }}>
    <DialogContent>
      <DialogHeader>
        <DialogTitle>{t(level ? "levels.editLevel" : "levels.newLevel")}</DialogTitle>
        <DialogDescription>{t("levels.description")}</DialogDescription>
      </DialogHeader>
      <form onSubmit={submit}>
        <DialogBody className="space-y-4">
          <fieldset disabled={mutation.isPending} className="space-y-4">
            <Field id="level-code" label={t("levels.code")} required><Input id="level-code" required maxLength={32} value={code} onChange={e => setCode(e.target.value)} /></Field>
            <Field id="level-name" label={t("levels.name")} required><Input id="level-name" required maxLength={100} value={name} onChange={e => setName(e.target.value)} /></Field>
            <Field id="level-discount" label={t("levels.discount")} required>
              <Input id="level-discount" type="number" min="0.01" max="100" step="0.01" required value={discount} onChange={e => setDiscount(e.target.value)} />
            </Field>
            <p className="text-xs text-[var(--color-muted-foreground)]">{t("levels.discountHint")}</p>
          </fieldset>
          {mutation.isError && <p role="alert" className="text-sm text-[var(--color-destructive)]">{describe(mutation.error, t("levels.requestFailed"))}</p>}
        </DialogBody>
        <DialogFooter>
          <Button type="button" variant="outline" disabled={mutation.isPending} onClick={onClose}>{t("chrome.cancel")}</Button>
          <Button type="submit" disabled={!valid || mutation.isPending}>{t(mutation.isPending ? "levels.saving" : "levels.save")}</Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>;
}

export function LevelSelect({ id, value, onChange, levels, disabled }: {
  id: string;
  value: string;
  onChange: (value: string) => void;
  levels: CustomerLevelDto[];
  disabled?: boolean;
}) {
  const t = useT();
  return <label htmlFor={id} className="block space-y-1 text-sm">
    <span>{t("partners.level")}</span>
    <select id={id} disabled={disabled} className="h-10 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-card)] px-3" value={value} onChange={event => onChange(event.target.value)}>
      <option value="">{t("partners.noLevel")}</option>
      {levels.map(level => <option key={level.id} value={level.id}>{level.code} · {level.name} ({discountLabel(t, level.discountPercent)})</option>)}
    </select>
  </label>;
}
