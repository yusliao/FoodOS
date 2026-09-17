import { useRef, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Truck } from "lucide-react";
import { createSupplier, searchSuppliers } from "@/api/procurement";
import { useAuth } from "@/auth/use-auth";
import { EntityPageHeader, ErrorBand, Field, LoadingRow } from "@/components/list";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { useT } from "@/i18n/locale-provider";
import { ProcurementPermissions } from "@/lib/permissions";
import { describe } from "@/pages/customers/request-error";

const key = ["procurement", "suppliers"] as const;

export function SuppliersPage() {
  const t = useT();
  const { user } = useAuth();
  const [search, setSearch] = useState("");
  const [creating, setCreating] = useState(false);
  const canCreate = !!user?.permissions.includes(ProcurementPermissions.Suppliers.Create);
  const query = useQuery({ queryKey: [...key, search], queryFn: ({ signal }) => searchSuppliers(search, signal) });
  return <div className="space-y-6">
    <EntityPageHeader icon={Truck} title={t("suppliers.title")} description={t("suppliers.description")}>
      {canCreate && <Button onClick={() => setCreating(true)}>{t("suppliers.create")}</Button>}
    </EntityPageHeader>
    <label className="block max-w-md space-y-2 text-sm"><span>{t("suppliers.search")}</span><Input value={search} onChange={event => setSearch(event.target.value)} /></label>
    {query.isPending && <LoadingRow label={t("suppliers.loading")} />}
    {query.isError && <div className="space-y-2"><ErrorBand message={describe(query.error, t("suppliers.failed"))} /><Button variant="outline" onClick={() => void query.refetch()}>{t("workbench.retry")}</Button></div>}
    {query.isSuccess && query.data.length === 0 && <p role="status">{t("suppliers.empty")}</p>}
    {query.isSuccess && <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{query.data.map(supplier => <article key={supplier.id} className="min-w-0 space-y-3 rounded-xl border p-5">
      <h2 className="break-words font-semibold">{supplier.name}</h2>
      <dl className="space-y-2 text-sm">
        <div><dt className="text-[var(--color-muted-foreground)]">{t("suppliers.code")}</dt><dd className="break-all">{supplier.code}</dd></div>
        <div><dt className="text-[var(--color-muted-foreground)]">{t("suppliers.categories")}</dt><dd className="break-words">{supplier.categories || "—"}</dd></div>
        <div><dt className="text-[var(--color-muted-foreground)]">{t("suppliers.leadDays")}</dt><dd>{supplier.leadDays}</dd></div>
        <div><dt className="text-[var(--color-muted-foreground)]">{t("suppliers.status")}</dt><dd>{t(`suppliers.${supplier.status}`, supplier.status)}</dd></div>
      </dl>
    </article>)}</div>}
    {creating && canCreate && <CreateSupplierDialog onClose={() => setCreating(false)} />}
  </div>;
}

function CreateSupplierDialog({ onClose }: { onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [categories, setCategories] = useState("");
  const [leadDays, setLeadDays] = useState("0");
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const mutation = useMutation({ mutationFn: createSupplier, onSuccess: async () => {
    await cache.invalidateQueries({ queryKey: key }); onClose();
  } });
  const valid = !!code.trim() && !!name.trim() && leadDays !== "" && Number.isInteger(Number(leadDays)) && Number(leadDays) >= 0 && Number(leadDays) <= 2147483647;
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!valid || mutation.isPending) return;
    const body = { code: code.trim(), name: name.trim(), categories: categories.trim() || null, leadDays: Number(leadDays) };
    const serialized = JSON.stringify(body);
    if (attempt.current?.body !== serialized) attempt.current = { body: serialized, key: crypto.randomUUID() };
    mutation.mutate({ body, key: attempt.current.key });
  }
  return <Dialog open onOpenChange={open => !open && !mutation.isPending && onClose()}><DialogContent>
    <DialogHeader><DialogTitle>{t("suppliers.create")}</DialogTitle><DialogDescription>{t("suppliers.createHint")}</DialogDescription></DialogHeader>
    <form onSubmit={submit}><DialogBody className="space-y-4"><fieldset disabled={mutation.isPending} className="space-y-4">
      <Field id="supplier-code" label={t("suppliers.code")} required><Input id="supplier-code" required maxLength={16} value={code} onChange={event => setCode(event.target.value)} /></Field>
      <Field id="supplier-name" label={t("suppliers.name")} required><Input id="supplier-name" required maxLength={128} value={name} onChange={event => setName(event.target.value)} /></Field>
      <Field id="supplier-categories" label={t("suppliers.categories")}><Input id="supplier-categories" maxLength={256} value={categories} onChange={event => setCategories(event.target.value)} /></Field>
      <Field id="supplier-lead" label={t("suppliers.leadDays")} required><Input id="supplier-lead" type="number" required min="0" max="2147483647" step="1" value={leadDays} onChange={event => setLeadDays(event.target.value)} /></Field>
    </fieldset>{mutation.isError && <ErrorBand message={describe(mutation.error, t("suppliers.failed"))} />}</DialogBody>
    <DialogFooter><Button type="button" variant="outline" onClick={onClose} disabled={mutation.isPending}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!valid || mutation.isPending}>{t("suppliers.save")}</Button></DialogFooter></form>
  </DialogContent></Dialog>;
}
