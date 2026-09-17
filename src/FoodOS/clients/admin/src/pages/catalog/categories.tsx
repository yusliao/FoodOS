import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { FolderTree } from "lucide-react";
import { toast } from "sonner";
import { createCategory, deleteCategory, searchCategories, updateCategory, type CategoryDto } from "@/api/catalog";
import { useAuth } from "@/auth/use-auth";
import { EntityPageHeader, ErrorBand, Field, LoadingRow, Pagination } from "@/components/list";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n/locale-provider";
import { CatalogPermissions } from "@/lib/permissions";
import { describe } from "@/pages/customers/request-error";

import { CatalogChoice } from "./catalog-choice";
export function CategoriesPage() {
  const t = useT();
  const { user } = useAuth();
  const granted = user?.permissions ?? [];
  const cache = useQueryClient();
  const [draft, setDraft] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<CategoryDto | null | undefined>(undefined);
  const [deleting, setDeleting] = useState<CategoryDto | null>(null);
  const query = useQuery({ queryKey: ["catalog", "categories", search, page], queryFn: ({ signal }) => searchCategories({ search, pageNumber: page }, signal) });
  const remove = useMutation({ mutationFn: deleteCategory, onSuccess: async () => { await cache.invalidateQueries({ queryKey: ["catalog", "categories"] }); toast.success(t("catalog.deleted")); setDeleting(null); } });
  const canCreate = granted.includes(CatalogPermissions.Categories.Create);
  const canUpdate = granted.includes(CatalogPermissions.Categories.Update);
  const canDelete = granted.includes(CatalogPermissions.Categories.Delete);
  function submitSearch(event: FormEvent) { event.preventDefault(); setPage(1); setSearch(draft.trim()); }
  return <div className="space-y-6">
    <EntityPageHeader icon={FolderTree} title={t("catalog.categories.title")} description={t("catalog.categories.description")}>
      {canCreate && <Button onClick={() => setEditing(null)}>{t("catalog.categories.new")}</Button>}
    </EntityPageHeader>
    <form onSubmit={submitSearch} className="flex max-w-xl flex-col gap-2 sm:flex-row"><Input type="search" aria-label={t("catalog.search")} value={draft} onChange={event => setDraft(event.target.value)} /><Button type="submit" variant="outline">{t("catalog.search")}</Button></form>
    {query.isPending && <LoadingRow label={t("catalog.loading")} />}
    {query.isError && <div className="space-y-2"><ErrorBand message={describe(query.error, t("catalog.loadFailed"))} /><Button variant="outline" onClick={() => void query.refetch()}>{t("workbench.retry")}</Button></div>}
    {query.isSuccess && query.data.items.length === 0 && <p role="status">{t("catalog.categories.empty")}</p>}
    {query.isSuccess && <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{query.data.items.map(category => <article key={category.id} className="space-y-3 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-5"><div><h2 className="font-semibold break-words">{category.name}</h2><p className="text-sm text-[var(--color-muted-foreground)]">{category.slug}</p></div><p className="min-h-10 text-sm break-words">{category.description || t("catalog.noDescription")}</p><p className="text-xs text-[var(--color-muted-foreground)]">{category.parentCategoryId ? t("catalog.categories.child") : t("catalog.categories.root")}</p><div className="flex flex-wrap gap-2">{canUpdate && <Button size="sm" variant="outline" onClick={() => setEditing(category)}>{t("catalog.edit")}</Button>}{canDelete && <Button size="sm" variant="destructive" onClick={() => setDeleting(category)}>{t("catalog.delete")}</Button>}</div></article>)}</div>}
    {query.isSuccess && <Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} shown={query.data.items.length} hasPrev={query.data.hasPrevious} hasNext={query.data.hasNext} fetching={query.isFetching} onPrev={() => setPage(value => value - 1)} onNext={() => setPage(value => value + 1)} />}
    {editing !== undefined && ((editing && canUpdate) || (!editing && canCreate)) && <CategoryDialog category={editing} onClose={() => setEditing(undefined)} />}
    <ConfirmDialog open={!!deleting && canDelete} onOpenChange={open => { if (!open && !remove.isPending) { setDeleting(null); remove.reset(); } }} title={t("catalog.categories.deleteTitle")} description={<>{t("catalog.deleteConfirm").replace("{name}", deleting?.name ?? "")}{remove.isError && <span role="alert" className="mt-3 block text-[var(--color-destructive)]">{describe(remove.error, t("catalog.requestFailed"))}</span>}</>} confirmLabel={t("catalog.delete")} destructive pending={remove.isPending} onConfirm={() => { if (deleting && canDelete && !remove.isPending) remove.mutate(deleting.id); }} />
  </div>;
}

function CategoryDialog({ category, onClose }: { category: CategoryDto | null; onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [name, setName] = useState(category?.name ?? "");
  const [description, setDescription] = useState(category?.description ?? "");
  const [parentCategoryId, setParentCategoryId] = useState(category?.parentCategoryId ?? "");
  const mutation = useMutation({ mutationFn: (input: Parameters<typeof createCategory>[0]) => category ? updateCategory({ ...input, categoryId: category.id }) : createCategory(input), onSuccess: async () => { await cache.invalidateQueries({ queryKey: ["catalog", "categories"] }); toast.success(t(category ? "catalog.updated" : "catalog.created")); onClose(); } });
  function submit(event: FormEvent) { event.preventDefault(); if (name.trim() && !mutation.isPending) mutation.mutate({ name: name.trim(), description: description.trim(), parentCategoryId: parentCategoryId || null }); }
  return <Dialog open onOpenChange={open => !open && !mutation.isPending && onClose()}><DialogContent><DialogHeader><DialogTitle>{t(category ? "catalog.categories.editTitle" : "catalog.categories.new")}</DialogTitle><DialogDescription>{t("catalog.categories.formHint")}</DialogDescription></DialogHeader><form onSubmit={submit}><DialogBody className="space-y-4"><fieldset disabled={mutation.isPending} className="space-y-4"><Field id="category-name" label={t("catalog.name")} required><Input id="category-name" required maxLength={128} value={name} onChange={event => setName(event.target.value)} /></Field><Field id="category-description" label={t("catalog.description")}><textarea id="category-description" className="min-h-24 w-full rounded-lg border border-[var(--color-border)] bg-transparent p-3" value={description} onChange={event => setDescription(event.target.value)} /></Field><CatalogChoice kind="categories" parentCategory excludedId={category?.id} value={parentCategoryId} onChange={setParentCategoryId} disabled={mutation.isPending} /></fieldset>{mutation.isError && <p role="alert" className="text-sm text-[var(--color-destructive)]">{describe(mutation.error, t("catalog.requestFailed"))}</p>}</DialogBody><DialogFooter><Button type="button" variant="outline" disabled={mutation.isPending} onClick={onClose}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!name.trim() || mutation.isPending}>{t(mutation.isPending ? "catalog.saving" : "catalog.save")}</Button></DialogFooter></form></DialogContent></Dialog>;
}
