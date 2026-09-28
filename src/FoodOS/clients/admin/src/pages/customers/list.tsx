import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { Building2 } from "lucide-react";
import { searchCustomers, updateCustomer } from "@/api/customers";
import { useAuth } from "@/auth/use-auth";
import { OrderingPermissions } from "@/lib/permissions";
import { EntityPageHeader, LoadingRow, ErrorBand } from "@/components/list";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n/locale-provider";
import { describe } from "./request-error";
import { CustomerCreate } from "./customer-create";

export function CustomersPage() {
  const t = useT();
  const { user } = useAuth();
  const granted = user?.permissions ?? [];
  const canCreate = granted.includes(OrderingPermissions.Customers.Create);
  const canEdit = granted.includes(OrderingPermissions.Customers.Update);
  const canViewStores = granted.includes(OrderingPermissions.Stores.View);
  const [draft, setDraft] = useState("");
  const [search, setSearch] = useState("");
  const [creating, setCreating] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draftName, setDraftName] = useState("");
  const queryClient = useQueryClient();
  const update = useMutation({
    mutationFn: updateCustomer,
    onSuccess: async () => {
      setEditingId(null);
      await queryClient.invalidateQueries({ queryKey: ["customers"] });
    },
  });
  const query = useQuery({ queryKey: ["customers", search], queryFn: ({ signal }) => searchCustomers(search, signal) });
  function submit(event: FormEvent) { event.preventDefault(); setSearch(draft.trim()); }
  return <div className="space-y-6">
    <EntityPageHeader icon={Building2} title={t("partners.customers")} description={t("partners.description")}>
      {canCreate && <Button onClick={() => setCreating(true)}>{t("partners.newCustomer")}</Button>}
    </EntityPageHeader>
    <form onSubmit={submit} className="flex max-w-lg gap-2"><Input type="search" aria-label={t("partners.search")} value={draft} onChange={e => setDraft(e.target.value)} /><Button type="submit" variant="outline">{t("partners.search")}</Button></form>
    {query.isPending && <LoadingRow label={t("partners.loading")} />}
    {query.isError && <div className="space-y-2"><ErrorBand message={describe(query.error, t("partners.requestFailed"))} /><Button variant="outline" onClick={() => void query.refetch()}>{t("workbench.retry")}</Button></div>}
    {query.isSuccess && query.data.length === 0 && <p role="status">{t("partners.emptyCustomers")}</p>}
    {query.isSuccess && <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      {query.data.map(customer => <article key={customer.id} className="space-y-3 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-5">
        <h2 className="font-semibold break-words">{customer.name}</h2>
        <dl className="space-y-2 text-sm break-words">
          <div><dt className="text-[var(--color-muted-foreground)]">{t("partners.code")}</dt><dd>{customer.code}</dd></div>
          <div><dt className="text-[var(--color-muted-foreground)]">{t("partners.tenant")}</dt><dd>{customer.customerTenantId ?? t("partners.unlinked")}</dd></div>
          <div><dt className="text-[var(--color-muted-foreground)]">{t("partners.creditStatus")}</dt><dd>{t(customer.creditHold ? "partners.held" : "partners.clear")}</dd></div>
        </dl>
        {canEdit && editingId !== customer.id && <Button variant="outline" onClick={() => { setEditingId(customer.id); setDraftName(customer.name); update.reset(); }}>{t("partners.editCustomer")}</Button>}
        {canEdit && editingId === customer.id && <form className="space-y-3" onSubmit={event => { event.preventDefault(); update.mutate({ id: customer.id, name: draftName.trim() }); }}>
          <Input aria-label={t("partners.name")} value={draftName} maxLength={128} onChange={event => setDraftName(event.target.value)} required />
          {update.isError && <ErrorBand message={describe(update.error, t("partners.requestFailed"))} />}
          <div className="flex gap-2"><Button type="submit" disabled={update.isPending || !draftName.trim()}>{t("partners.saveChanges")}</Button><Button type="button" variant="outline" onClick={() => setEditingId(null)}>{t("partners.cancel")}</Button></div>
        </form>}
        {canViewStores && <Link className="inline-block text-sm underline" to={`/stores?customerOrgId=${encodeURIComponent(customer.id)}`}>{t("partners.viewStores")}</Link>}
      </article>)}
    </div>}
    {creating && canCreate && <CustomerCreate onClose={() => setCreating(false)} />}
  </div>;
}
