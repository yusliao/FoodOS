import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Tags } from "lucide-react";
import { toast } from "sonner";
import { createBrand, deleteBrand, searchBrands, updateBrand, type BrandDto } from "@/api/catalog";
import { useAuth } from "@/auth/use-auth";
import { EntityPageHeader, ErrorBand, Field, LoadingRow, Pagination } from "@/components/list";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n/locale-provider";
import { CatalogPermissions } from "@/lib/permissions";
import { describe } from "@/pages/customers/request-error";

export function BrandsPage() {
  const t = useT();
  const { user } = useAuth();
  const granted = user?.permissions ?? [];
  const cache = useQueryClient();
  const [draft, setDraft] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<BrandDto | null | undefined>(undefined);
  const [deleting, setDeleting] = useState<BrandDto | null>(null);
  const query = useQuery({
    queryKey: ["catalog", "brands", search, page],
    queryFn: ({ signal }) => searchBrands({ search, pageNumber: page }, signal),
  });
  const remove = useMutation({
    mutationFn: (id: string) => deleteBrand(id),
    onSuccess: async () => {
      await cache.invalidateQueries({ queryKey: ["catalog", "brands"] });
      toast.success(t("catalog.deleted"));
      setDeleting(null);
    },
  });
  const canCreate = granted.includes(CatalogPermissions.Brands.Create);
  const canUpdate = granted.includes(CatalogPermissions.Brands.Update);
  const canDelete = granted.includes(CatalogPermissions.Brands.Delete);
  function submitSearch(event: FormEvent) { event.preventDefault(); setPage(1); setSearch(draft.trim()); }
  return <div className="space-y-6">
    <EntityPageHeader icon={Tags} title={t("catalog.brands.title")} description={t("catalog.brands.description")}>
      {canCreate && <Button onClick={() => setEditing(null)}>{t("catalog.brands.new")}</Button>}
    </EntityPageHeader>
    <form onSubmit={submitSearch} className="flex max-w-xl flex-col gap-2 sm:flex-row">
      <Input type="search" aria-label={t("catalog.search")} value={draft} onChange={event => setDraft(event.target.value)} />
      <Button type="submit" variant="outline">{t("catalog.search")}</Button>
    </form>
    {query.isPending && <LoadingRow label={t("catalog.loading")} />}
    {query.isError && <div className="space-y-2"><ErrorBand message={describe(query.error, t("catalog.loadFailed"))} /><Button variant="outline" onClick={() => void query.refetch()}>{t("workbench.retry")}</Button></div>}
    {query.isSuccess && query.data.items.length === 0 && <p role="status">{t("catalog.brands.empty")}</p>}
    {query.isSuccess && <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{query.data.items.map(brand =>
      <article key={brand.id} className="space-y-3 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-5">
        <div><h2 className="font-semibold break-words">{brand.name}</h2><p className="text-sm text-[var(--color-muted-foreground)]">{brand.slug}</p></div>
        <p className="min-h-10 text-sm break-words">{brand.description || t("catalog.noDescription")}</p>
        <div className="flex flex-wrap gap-2">{canUpdate && <Button size="sm" variant="outline" onClick={() => setEditing(brand)}>{t("catalog.edit")}</Button>}{canDelete && <Button size="sm" variant="destructive" onClick={() => setDeleting(brand)}>{t("catalog.delete")}</Button>}</div>
      </article>)}</div>}
    {query.isSuccess && <Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} shown={query.data.items.length} hasPrev={query.data.hasPrevious} hasNext={query.data.hasNext} fetching={query.isFetching} onPrev={() => setPage(value => value - 1)} onNext={() => setPage(value => value + 1)} />}
    {editing !== undefined && ((editing && canUpdate) || (!editing && canCreate)) && <BrandDialog brand={editing} onClose={() => setEditing(undefined)} />}
    <ConfirmDialog open={!!deleting} onOpenChange={open => !open && setDeleting(null)} title={t("catalog.brands.deleteTitle")} description={t("catalog.deleteConfirm").replace("{name}", deleting?.name ?? "")} confirmLabel={t("catalog.delete")} destructive pending={remove.isPending} onConfirm={() => deleting && remove.mutate(deleting.id)} />
    {remove.isError && <ErrorBand message={describe(remove.error, t("catalog.requestFailed"))} />}
  </div>;
}

function BrandDialog({ brand, onClose }: { brand: BrandDto | null; onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [name, setName] = useState(brand?.name ?? "");
  const [description, setDescription] = useState(brand?.description ?? "");
  const [logoUrl, setLogoUrl] = useState(brand?.logoUrl ?? "");
  const mutation = useMutation({
    mutationFn: () => brand ? updateBrand({ brandId: brand.id, name: name.trim(), description: description.trim(), logoUrl: logoUrl.trim() }) : createBrand({ name: name.trim(), description: description.trim(), logoUrl: logoUrl.trim() }),
    onSuccess: async () => { await cache.invalidateQueries({ queryKey: ["catalog", "brands"] }); toast.success(t(brand ? "catalog.updated" : "catalog.created")); onClose(); },
  });
  function submit(event: FormEvent) { event.preventDefault(); if (name.trim() && !mutation.isPending) mutation.mutate(); }
  return <Dialog open onOpenChange={open => !open && !mutation.isPending && onClose()}><DialogContent><DialogHeader><DialogTitle>{t(brand ? "catalog.brands.editTitle" : "catalog.brands.new")}</DialogTitle><DialogDescription>{t("catalog.brands.formHint")}</DialogDescription></DialogHeader><form onSubmit={submit}><DialogBody className="space-y-4"><fieldset disabled={mutation.isPending} className="space-y-4"><Field id="brand-name" label={t("catalog.name")} required><Input id="brand-name" required maxLength={128} value={name} onChange={event => setName(event.target.value)} /></Field><Field id="brand-description" label={t("catalog.description")}><textarea id="brand-description" className="min-h-24 w-full rounded-lg border border-[var(--color-border)] bg-transparent p-3" value={description} onChange={event => setDescription(event.target.value)} /></Field><Field id="brand-logo" label={t("catalog.logoUrl")}><Input id="brand-logo" type="url" value={logoUrl} onChange={event => setLogoUrl(event.target.value)} /></Field></fieldset>{mutation.isError && <p role="alert" className="text-sm text-[var(--color-destructive)]">{describe(mutation.error, t("catalog.requestFailed"))}</p>}</DialogBody><DialogFooter><Button type="button" variant="outline" disabled={mutation.isPending} onClick={onClose}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!name.trim() || mutation.isPending}>{t(mutation.isPending ? "catalog.saving" : "catalog.save")}</Button></DialogFooter></form></DialogContent></Dialog>;
}
