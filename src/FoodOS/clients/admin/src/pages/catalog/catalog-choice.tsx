import { useId, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { searchBrands, searchCategories, searchProducts, type PagedResponse } from "@/api/catalog";
import { useAuth } from "@/auth/use-auth";
import { CatalogPermissions } from "@/lib/permissions";
import { useT } from "@/i18n/locale-provider";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Field } from "@/components/list";

type Choice = { id: string; name: string; sku?: string };
export function CatalogChoice({ kind, initial, value, onChange, disabled, parentCategory, excludedId }: {
  kind: "brands" | "categories" | "products";
  initial?: PagedResponse<Choice>;
  value: string;
  onChange: (value: string) => void;
  disabled: boolean;
  parentCategory?: boolean;
  excludedId?: string;
}) {
  const t = useT();
  const { user } = useAuth();
  const allowed = !!user?.permissions.includes(kind === "products" ? CatalogPermissions.Products.View : kind === "brands" ? CatalogPermissions.Brands.View : CatalogPermissions.Categories.View);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<Choice | undefined>(() => initial?.items.find(item => item.id === value));
  const firstPage = !!initial && page === 1 && !search.trim();
  const query = useQuery({
    queryKey: ["catalog", kind, "choice", search.trim(), page],
    queryFn: ({ signal }) => (kind === "products" ? searchProducts : kind === "brands" ? searchBrands : searchCategories)({ pageNumber: page, pageSize: 50, search: search.trim() }, signal),
    enabled: allowed && !disabled && !firstPage,
  });
  const data = firstPage ? initial : query.isSuccess ? query.data : undefined;
  const items: Choice[] = (data?.items ?? []).filter(item => item.id !== excludedId);
  const loading = !firstPage && query.isFetching;
  const failed = !firstPage && query.isError;
  const label = t(kind === "products" ? "pricing.product" : parentCategory ? "catalog.categories.parent" : kind === "brands" ? "catalog.products.brand" : "catalog.products.category");
  const generatedId = useId();
  const id = kind === "products" ? generatedId : parentCategory ? "category-parent" : `product-${kind}`;
  return <section aria-label={label} className="min-w-0 space-y-2">
    <Field id={id} label={label} required={!parentCategory}>
      <select id={id} required={!parentCategory} value={value} disabled={disabled || !allowed || loading} className="h-10 w-full rounded-lg border bg-[var(--color-card)] px-3" onChange={event => {
        if (!allowed || disabled) return;
        setSelected(items.find(item => item.id === event.target.value));
        onChange(event.target.value);
      }}>
        <option value="">{t(kind === "products" ? "pricing.chooseProduct" : parentCategory ? "catalog.categories.noParent" : kind === "brands" ? "catalog.products.chooseBrand" : "catalog.products.chooseCategory")}</option>
        {value && !items.some(item => item.id === value) && <option value={value}>{selected?.id === value ? selected.name : value}</option>}
        {items.map(item => <option key={item.id} value={item.id}>{kind === "products" && item.sku ? `${item.sku} · ${item.name}` : item.name}</option>)}
      </select>
    </Field>
    <Input type="search" aria-label={`${t("catalog.search")} ${label}`} value={search} disabled={disabled || !allowed} onChange={event => { setSearch(event.target.value); setPage(1); }} />
    {loading && <p role="status">{t("common.loading")}</p>}
    {failed && <div className="space-y-2"><p role="alert">{t(kind === "products" ? "pricing.productsFailed" : "catalog.products.lookupFailed")}</p><Button type="button" variant="outline" disabled={disabled || !allowed || loading} onClick={() => { if (allowed && !disabled) void query.refetch(); }}>{t("workbench.retry")}</Button></div>}
    {data && items.length === 0 && <p role="status">{t("common.emptyDefault")}</p>}
    <div className="flex flex-wrap gap-2">
      <Button type="button" variant="outline" size="sm" disabled={disabled || !allowed || loading || page <= 1} onClick={() => setPage(current => current - 1)}>{t("common.previous")}</Button>
      <Button type="button" variant="outline" size="sm" disabled={disabled || !allowed || loading || !data?.hasNext} onClick={() => setPage(current => current + 1)}>{t("common.next")}</Button>
    </div>
  </section>;
}
