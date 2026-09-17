import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Package } from "lucide-react";
import { toast } from "sonner";
import { changeProductPrice, createProduct, deleteProduct, searchBrands, searchCategories, searchProducts, updateProduct, type ProductDto, type PagedResponse } from "@/api/catalog";
import { useAuth } from "@/auth/use-auth";
import { EntityPageHeader, ErrorBand, Field, LoadingRow, Pagination } from "@/components/list";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n/locale-provider";
import { CatalogPermissions } from "@/lib/permissions";
import { CatalogChoice } from "./catalog-choice";
import { describe } from "@/pages/customers/request-error";

export function ProductsPage() {
  const t = useT();
  const { user } = useAuth();
  const granted = user?.permissions ?? [];
  const cache = useQueryClient();
  const [draft, setDraft] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<ProductDto | null | undefined>(undefined);
  const [pricing, setPricing] = useState<ProductDto | null>(null);
  const [deleting, setDeleting] = useState<ProductDto | null>(null);
  const query = useQuery({ queryKey: ["catalog", "products", search, page], queryFn: ({ signal }) => searchProducts({ search, pageNumber: page }, signal) });
  const canReadLookups = granted.includes(CatalogPermissions.Brands.View) && granted.includes(CatalogPermissions.Categories.View);
  const canCreate = granted.includes(CatalogPermissions.Products.Create) && canReadLookups;
  const canUpdate = granted.includes(CatalogPermissions.Products.Update);
  const canEditProduct = canUpdate && canReadLookups;
  const canDelete = granted.includes(CatalogPermissions.Products.Delete);
  const lookupsNeeded = editing !== undefined && (editing ? canEditProduct : canCreate);
  const brands = useQuery({ queryKey: ["catalog", "brands", "product-form"], queryFn: ({ signal }) => searchBrands({ pageSize: 50 }, signal), enabled: lookupsNeeded && canReadLookups });
  const categories = useQuery({ queryKey: ["catalog", "categories", "product-form"], queryFn: ({ signal }) => searchCategories({ pageSize: 50 }, signal), enabled: lookupsNeeded && canReadLookups });
  function cancelFormPreparation() {
    setEditing(undefined);
    void cache.cancelQueries({ queryKey: ["catalog", "brands", "product-form"], exact: true });
    void cache.cancelQueries({ queryKey: ["catalog", "categories", "product-form"], exact: true });
  }
  const remove = useMutation({ mutationFn: deleteProduct, onSuccess: async () => { await cache.invalidateQueries({ queryKey: ["catalog", "products"] }); toast.success(t("catalog.deleted")); setDeleting(null); } });
  function submitSearch(event: FormEvent) { event.preventDefault(); setPage(1); setSearch(draft.trim()); }
  return <div className="space-y-6">
    <EntityPageHeader icon={Package} title={t("catalog.products.title")} description={t("catalog.products.description")}>
      {canCreate && <Button onClick={() => setEditing(null)}>{t("catalog.products.new")}</Button>}
    </EntityPageHeader>
    <form onSubmit={submitSearch} className="flex max-w-xl flex-col gap-2 sm:flex-row"><Input type="search" aria-label={t("catalog.search")} value={draft} onChange={event => setDraft(event.target.value)} /><Button type="submit" variant="outline">{t("catalog.search")}</Button></form>
    {query.isPending && <LoadingRow label={t("catalog.loading")} />}
    {query.isError && <div className="space-y-2"><ErrorBand message={describe(query.error, t("catalog.loadFailed"))} /><Button variant="outline" onClick={() => void query.refetch()}>{t("workbench.retry")}</Button></div>}
    {query.isSuccess && query.data.items.length === 0 && <p role="status">{t("catalog.products.empty")}</p>}
    {query.isSuccess && <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{query.data.items.map(product => <article key={product.id} className="space-y-3 rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-5"><div className="flex items-start justify-between gap-3"><div><h2 className="font-semibold break-words">{product.name}</h2><p className="font-mono text-xs text-[var(--color-muted-foreground)]">{product.sku}</p></div><span className="rounded-full border px-2 py-1 text-xs">{t(product.isActive ? "catalog.products.active" : "catalog.products.hidden")}</span></div><p className="text-lg font-semibold">{new Intl.NumberFormat(undefined, { style: "currency", currency: product.price.currency }).format(product.price.amount)}</p><dl className="grid grid-cols-2 gap-2 text-xs text-[var(--color-muted-foreground)]"><div><dt>{t("catalog.products.brand")}</dt><dd className="break-all">{product.brandId}</dd></div><div><dt>{t("catalog.products.category")}</dt><dd className="break-all">{product.categoryId}</dd></div></dl><div className="flex flex-wrap gap-2">{canEditProduct && <Button size="sm" variant="outline" onClick={() => setEditing(product)}>{t("catalog.edit")}</Button>}{canUpdate && <Button size="sm" variant="outline" onClick={() => setPricing(product)}>{t("catalog.products.changePrice")}</Button>}{canDelete && <Button size="sm" variant="destructive" onClick={() => setDeleting(product)}>{t("catalog.delete")}</Button>}</div></article>)}</div>}
    {query.isSuccess && <Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} shown={query.data.items.length} hasPrev={query.data.hasPrevious} hasNext={query.data.hasNext} fetching={query.isFetching} onPrev={() => setPage(value => value - 1)} onNext={() => setPage(value => value + 1)} />}
    {editing !== undefined && ((editing && canEditProduct) || (!editing && canCreate)) && brands.isSuccess && categories.isSuccess && <ProductDialog product={editing} brands={brands.data} categories={categories.data} onClose={() => setEditing(undefined)} />}
    {lookupsNeeded && !(brands.isSuccess && categories.isSuccess) && <section aria-label={t("catalog.products.loadingForm")} className="space-y-3">
      {(brands.isFetching || categories.isFetching) && <LoadingRow label={t("catalog.products.loadingForm")} />}
      {(brands.isError || categories.isError) && <ErrorBand message={t("catalog.products.lookupFailed")} />}
      <div className="flex flex-wrap gap-2">
        {(brands.isError || categories.isError) && <Button variant="outline" disabled={brands.isFetching || categories.isFetching} onClick={() => {
          if (!lookupsNeeded || !canReadLookups) return;
          if (brands.isError) void brands.refetch();
          if (categories.isError) void categories.refetch();
        }}>{t("workbench.retry")}</Button>}
        <Button variant="ghost" onClick={cancelFormPreparation}>{t("chrome.cancel")}</Button>
      </div>
    </section>}
    {pricing && canUpdate && <PriceDialog product={pricing} onClose={() => setPricing(null)} />}
    <ConfirmDialog open={!!deleting && canDelete} onOpenChange={open => { if (!open && !remove.isPending) { setDeleting(null); remove.reset(); } }} title={t("catalog.products.deleteTitle")} description={<>{t("catalog.deleteConfirm").replace("{name}", deleting?.name ?? "")}{remove.isError && <span role="alert" className="mt-3 block text-[var(--color-destructive)]">{describe(remove.error, t("catalog.requestFailed"))}</span>}</>} confirmLabel={t("catalog.delete")} destructive pending={remove.isPending} onConfirm={() => { if (deleting && canDelete && !remove.isPending) remove.mutate(deleting.id); }} />
  </div>;
}

function ProductDialog({ product, brands, categories, onClose }: { product: ProductDto | null; brands: PagedResponse<{ id: string; name: string }>; categories: PagedResponse<{ id: string; name: string }>; onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [sku, setSku] = useState(product?.sku ?? "");
  const [name, setName] = useState(product?.name ?? "");
  const [description, setDescription] = useState(product?.description ?? "");
  const [brandId, setBrandId] = useState(product?.brandId ?? "");
  const [categoryId, setCategoryId] = useState(product?.categoryId ?? "");
  const [amount, setAmount] = useState(String(product?.price.amount ?? ""));
  const [isActive, setIsActive] = useState(product?.isActive ?? true);
  const mutation = useMutation({ mutationFn: () => product ? updateProduct({ productId: product.id, name: name.trim(), description: description.trim(), brandId, categoryId, isActive }) : createProduct({ sku: sku.trim(), name: name.trim(), description: description.trim(), brandId, categoryId, priceAmount: Number(amount), priceCurrency: "USD", stock: 0 }), onSuccess: async () => { await cache.invalidateQueries({ queryKey: ["catalog", "products"] }); toast.success(t(product ? "catalog.updated" : "catalog.created")); onClose(); } });
  const valid = name.trim() && brandId && categoryId && (product || (sku.trim() && Number(amount) >= 0));
  function submit(event: FormEvent) { event.preventDefault(); if (valid && !mutation.isPending) mutation.mutate(); }
  return <Dialog open onOpenChange={open => !open && !mutation.isPending && onClose()}><DialogContent size="lg"><DialogHeader><DialogTitle>{t(product ? "catalog.products.editTitle" : "catalog.products.new")}</DialogTitle><DialogDescription>{t("catalog.products.formHint")}</DialogDescription></DialogHeader><form onSubmit={submit}><DialogBody className="grid gap-4 sm:grid-cols-2"><fieldset disabled={mutation.isPending} className="contents"><Field id="product-sku" label={t("catalog.products.sku")} required><Input id="product-sku" required={!product} disabled={!!product} value={sku} onChange={event => setSku(event.target.value)} /></Field><Field id="product-name" label={t("catalog.name")} required><Input id="product-name" required value={name} onChange={event => setName(event.target.value)} /></Field><CatalogChoice kind="brands" initial={brands} value={brandId} onChange={setBrandId} disabled={mutation.isPending} /><CatalogChoice kind="categories" initial={categories} value={categoryId} onChange={setCategoryId} disabled={mutation.isPending} />{!product && <Field id="product-price" label={t("catalog.products.priceUsd")} required><Input id="product-price" type="number" min="0" step="0.01" required value={amount} onChange={event => setAmount(event.target.value)} /></Field>}<label className="flex items-center gap-2 self-end pb-2 text-sm"><input type="checkbox" checked={isActive} onChange={event => setIsActive(event.target.checked)} />{t("catalog.products.active")}</label><div className="sm:col-span-2"><Field id="product-description" label={t("catalog.description")}><textarea id="product-description" className="min-h-24 w-full rounded-lg border border-[var(--color-border)] bg-transparent p-3" value={description} onChange={event => setDescription(event.target.value)} /></Field></div></fieldset>{mutation.isError && <p role="alert" className="sm:col-span-2 text-sm text-[var(--color-destructive)]">{describe(mutation.error, t("catalog.requestFailed"))}</p>}</DialogBody><DialogFooter><Button type="button" variant="outline" disabled={mutation.isPending} onClick={onClose}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!valid || mutation.isPending}>{t(mutation.isPending ? "catalog.saving" : "catalog.save")}</Button></DialogFooter></form></DialogContent></Dialog>;
}

function PriceDialog({ product, onClose }: { product: ProductDto; onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [amount, setAmount] = useState(String(product.price.amount));
  const valid = amount.trim() !== "" && Number.isFinite(Number(amount)) && Number(amount) >= 0;
  const mutation = useMutation({
    mutationFn: changeProductPrice,
    onSuccess: async () => {
      await cache.invalidateQueries({ queryKey: ["catalog", "products"] });
      toast.success(t("catalog.products.priceUpdated"));
      onClose();
    },
  });
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!valid || mutation.isPending) return;
    mutation.mutate({ productId: product.id, amount: Number(amount), currency: product.price.currency });
  }
  return <Dialog open onOpenChange={open => !open && !mutation.isPending && onClose()}>
    <DialogContent size="sm">
      <DialogHeader>
        <DialogTitle>{t("catalog.products.changePrice")}</DialogTitle>
        <DialogDescription>{product.name} · {product.price.currency}</DialogDescription>
      </DialogHeader>
      <form onSubmit={submit}>
        <DialogBody className="space-y-4">
          <Field id="price-amount" label={t("catalog.products.newPrice")} required>
            <Input id="price-amount" type="number" min="0" step="0.01" required disabled={mutation.isPending}
              value={amount} onChange={event => setAmount(event.target.value)} />
          </Field>
          {mutation.isError && <p role="alert" className="text-sm text-[var(--color-destructive)]">{describe(mutation.error, t("catalog.requestFailed"))}</p>}
        </DialogBody>
        <DialogFooter>
          <Button type="button" variant="outline" disabled={mutation.isPending} onClick={onClose}>{t("chrome.cancel")}</Button>
          <Button type="submit" disabled={!valid || mutation.isPending}>{t(mutation.isPending ? "catalog.saving" : "catalog.save")}</Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>;
}
