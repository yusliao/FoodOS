import { useMemo, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router-dom";
import { Building2 } from "lucide-react";
import { searchCustomers, updateCustomer } from "@/api/customers";
import {
  assignCustomerLevel,
  customerLevelKeys,
  getCustomerLevelAssignments,
  getCustomerLevels,
  type CustomerLevelDto,
} from "@/api/catalog";
import { useAuth } from "@/auth/use-auth";
import { CatalogPermissions, OrderingPermissions } from "@/lib/permissions";
import { EntityPageHeader, LoadingRow, ErrorBand } from "@/components/list";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/cn";
import { useT } from "@/i18n/locale-provider";
import { PricingPanel } from "@/pages/catalog/pricing";
import { describe } from "./request-error";
import { CustomerCreate } from "./customer-create";
import { CustomerLevelsSection, LevelSelect } from "./customer-levels";
import { discountLabel } from "./level-format";

type Tab = "customers" | "pricing";

export function CustomersPage() {
  const t = useT();
  const { user } = useAuth();
  const granted = user?.permissions ?? [];
  const canViewCustomers = granted.includes(OrderingPermissions.Customers.View);
  const canViewPricing = granted.includes(CatalogPermissions.PriceLists.View);
  const canCreate = canViewCustomers && granted.includes(OrderingPermissions.Customers.Create);
  const [params, setParams] = useSearchParams();
  const requested: Tab = params.get("tab") === "pricing" ? "pricing" : "customers";
  // 只给有权限的页签；请求的页签没权限时退回另一个
  const tab: Tab = requested === "pricing" ? (canViewPricing ? "pricing" : "customers") : (canViewCustomers ? "customers" : "pricing");
  const [creating, setCreating] = useState(false);
  const selectTab = (next: Tab) => setParams(next === "customers" ? {} : { tab: next }, { replace: true });

  return <div className="space-y-6">
    <EntityPageHeader icon={Building2} title={t("partners.customers")} description={t("partners.description")}>
      {tab === "customers" && canCreate && <Button onClick={() => setCreating(true)}>{t("partners.newCustomer")}</Button>}
    </EntityPageHeader>
    {canViewCustomers && canViewPricing && <div role="tablist" aria-label={t("partners.customers")} className="flex gap-1 border-b border-[var(--color-border)]">
      {(["customers", "pricing"] as const).map(value => <button
        key={value}
        type="button"
        role="tab"
        aria-selected={tab === value}
        onClick={() => selectTab(value)}
        className={cn(
          "-mb-px border-b-2 px-4 py-2 text-sm",
          tab === value
            ? "border-[var(--color-primary)] font-semibold text-[var(--color-foreground)]"
            : "border-transparent text-[var(--color-muted-foreground)] hover:text-[var(--color-foreground)]",
        )}
      >{t(value === "customers" ? "partners.tabCustomers" : "partners.tabPricing")}</button>)}
    </div>}
    {tab === "customers" && <CustomersTab canViewPricing={canViewPricing} />}
    {tab === "pricing" && <div role="tabpanel" className="space-y-8"><CustomerLevelsSection /><PricingPanel /></div>}
    {creating && canCreate && <CustomerCreate onClose={() => setCreating(false)} />}
  </div>;
}

function CustomersTab({ canViewPricing }: { canViewPricing: boolean }) {
  const t = useT();
  const { user } = useAuth();
  const granted = user?.permissions ?? [];
  const canEdit = granted.includes(OrderingPermissions.Customers.Update);
  const canViewStores = granted.includes(OrderingPermissions.Stores.View);
  const canAssignLevel = canViewPricing && granted.includes(CatalogPermissions.PriceLists.Update);
  const [draft, setDraft] = useState("");
  const [search, setSearch] = useState("");
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draftName, setDraftName] = useState("");
  const [draftCode, setDraftCode] = useState("");
  const [draftLevel, setDraftLevel] = useState("");
  const queryClient = useQueryClient();
  const query = useQuery({ queryKey: ["customers", search], queryFn: ({ signal }) => searchCustomers(search, signal) });
  const levels = useQuery({ queryKey: customerLevelKeys.levels, queryFn: ({ signal }) => getCustomerLevels(signal), enabled: canViewPricing });
  const assignments = useQuery({ queryKey: customerLevelKeys.assignments, queryFn: ({ signal }) => getCustomerLevelAssignments(signal), enabled: canViewPricing });
  const levelById = useMemo(() => new Map((levels.data ?? []).map(level => [level.id, level])), [levels.data]);
  const levelOf = useMemo(() => new Map((assignments.data ?? []).map(item => [item.customerOrgId, item.customerLevelId])), [assignments.data]);
  const levelsReady = levels.isSuccess && assignments.isSuccess;

  const update = useMutation({
    mutationFn: async (input: { id: string; name: string; code: string; levelId: string | null; levelChanged: boolean }) => {
      await updateCustomer({ id: input.id, name: input.name, code: input.code });
      if (input.levelChanged) await assignCustomerLevel(input.id, input.levelId);
    },
    onSuccess: async () => {
      setEditingId(null);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["customers"] }),
        queryClient.invalidateQueries({ queryKey: customerLevelKeys.levels }),
      ]);
    },
  });

  function submit(event: FormEvent) { event.preventDefault(); setSearch(draft.trim()); }
  function startEdit(id: string, name: string, code: string) {
    setEditingId(id); setDraftName(name); setDraftCode(code); setDraftLevel(levelOf.get(id) ?? ""); update.reset();
  }

  return <div role="tabpanel" className="space-y-6">
    <form onSubmit={submit} className="flex max-w-lg gap-2"><Input type="search" aria-label={t("partners.search")} value={draft} onChange={e => setDraft(e.target.value)} /><Button type="submit" variant="outline">{t("partners.search")}</Button></form>
    {query.isPending && <LoadingRow label={t("partners.loading")} />}
    {query.isError && <div className="space-y-2"><ErrorBand message={describe(query.error, t("partners.requestFailed"))} /><Button variant="outline" onClick={() => void query.refetch()}>{t("workbench.retry")}</Button></div>}
    {canViewPricing && (levels.isError || assignments.isError) && <div className="space-y-2"><ErrorBand message={describe(levels.error ?? assignments.error, t("levels.loadFailed"))} /><Button variant="outline" onClick={() => { void levels.refetch(); void assignments.refetch(); }}>{t("workbench.retry")}</Button></div>}
    {query.isSuccess && query.data.length === 0 && <p role="status">{t("partners.emptyCustomers")}</p>}
    {query.isSuccess && <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      {query.data.map(customer => {
        const level = levelById.get(levelOf.get(customer.id) ?? "");
        return <article key={customer.id} className="space-y-3 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-5">
          <h2 className="font-semibold break-words">{customer.name}</h2>
          <dl className="space-y-2 text-sm break-words">
            <div><dt className="text-[var(--color-muted-foreground)]">{t("partners.code")}</dt><dd>{customer.code}</dd></div>
            {canViewPricing && <div><dt className="text-[var(--color-muted-foreground)]">{t("partners.level")}</dt><dd>{levelsReady ? <LevelText level={level} /> : "—"}</dd></div>}
            <div><dt className="text-[var(--color-muted-foreground)]">{t("partners.tenant")}</dt><dd>{customer.customerTenantId ?? t("partners.unlinked")}</dd></div>
            <div><dt className="text-[var(--color-muted-foreground)]">{t("partners.creditStatus")}</dt><dd>{t(customer.creditHold ? "partners.held" : "partners.clear")}</dd></div>
          </dl>
          {canEdit && editingId !== customer.id && <Button variant="outline" onClick={() => startEdit(customer.id, customer.name, customer.code)}>{t("partners.editCustomer")}</Button>}
          {canEdit && editingId === customer.id && <form className="space-y-3" onSubmit={event => {
            event.preventDefault();
            const current = levelOf.get(customer.id) ?? "";
            update.mutate({ id: customer.id, name: draftName.trim(), code: draftCode.trim(), levelId: draftLevel || null, levelChanged: canAssignLevel && levelsReady && draftLevel !== current });
          }}>
            <Input aria-label={t("partners.code")} value={draftCode} maxLength={16} onChange={event => setDraftCode(event.target.value)} required />
            <p className="text-xs text-[var(--color-muted-foreground)]">{t("partners.codeUniqueHint")}</p>
            <Input aria-label={t("partners.name")} value={draftName} maxLength={128} onChange={event => setDraftName(event.target.value)} required />
            {canAssignLevel && levelsReady && <LevelSelect id={`customer-level-${customer.id}`} value={draftLevel} onChange={setDraftLevel} levels={levels.data} />}
            {update.isError && <ErrorBand message={describe(update.error, t("partners.requestFailed"))} />}
            <div className="flex gap-2"><Button type="submit" disabled={update.isPending || !draftName.trim() || !draftCode.trim()}>{t("partners.saveChanges")}</Button><Button type="button" variant="outline" onClick={() => setEditingId(null)}>{t("partners.cancel")}</Button></div>
          </form>}
          {canViewStores && <Link className="inline-block text-sm underline" to={`/stores?customerOrgId=${encodeURIComponent(customer.id)}`}>{t("partners.viewStores")}</Link>}
        </article>;
      })}
    </div>}
  </div>;
}

function LevelText({ level }: { level: CustomerLevelDto | undefined }) {
  const t = useT();
  if (!level) return <>{t("partners.noLevel")}</>;
  return <>{level.name} <span className="text-[var(--color-muted-foreground)]">· {discountLabel(t, level.discountPercent)}</span></>;
}
