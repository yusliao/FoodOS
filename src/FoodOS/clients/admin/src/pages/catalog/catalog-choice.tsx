import { useId, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { searchBrands, searchCategories, searchProducts, type PagedResponse } from "@/api/catalog";
import { useAuth } from "@/auth/use-auth";
import { CatalogPermissions } from "@/lib/permissions";
import { useDebouncedSearch } from "@/hooks/use-debounced-search";
import { useT } from "@/i18n/locale-provider";
import { Button } from "@/components/ui/button";
import { Field } from "@/components/list";
import { SearchableSelect } from "@/components/list/searchable-select";

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
  const [searchTerm, searchPending] = useDebouncedSearch(search);
  const firstPage = !!initial && page === 1 && !searchTerm;
  const query = useQuery({
    queryKey: ["catalog", kind, "choice", searchTerm, page],
    queryFn: ({ signal }) => (kind === "products" ? searchProducts : kind === "brands" ? searchBrands : searchCategories)({ pageNumber: page, pageSize: 50, search: searchTerm }, signal),
    enabled: allowed && !disabled && !firstPage && !searchPending,
  });
  const data = firstPage ? initial : query.isSuccess ? query.data : undefined;
  const items: Choice[] = (data?.items ?? []).filter(item => item.id !== excludedId);
  const loading = searchPending || (!firstPage && query.isFetching);
  const failed = !searchPending && !firstPage && query.isError;
  const label = t(kind === "products" ? "pricing.product" : parentCategory ? "catalog.categories.parent" : kind === "brands" ? "catalog.products.brandLabel" : "catalog.products.categoryLabel");
  const searchLabel = t(kind === "products" ? "catalog.products.searchProduct" : kind === "brands" ? "catalog.products.searchBrand" : "catalog.products.searchCategory");
  const generatedId = useId();
  const id = kind === "products" ? generatedId : parentCategory ? "category-parent" : `product-${kind}`;
  const feedback = <>
    {loading && <p role="status">{t("common.loading")}</p>}
    {failed && <div className="space-y-2"><p role="alert">{t(kind === "products" ? "pricing.productsFailed" : "catalog.products.lookupFailed")}</p><Button type="button" variant="outline" disabled={disabled || !allowed || loading} onClick={() => { if (allowed && !disabled) void query.refetch(); }}>{t("workbench.retry")}</Button></div>}
    {!loading && data && items.length === 0 && <p role="status">{t("common.emptyDefault")}</p>}
  </>;
  const pagination = <div className="flex flex-wrap gap-2">
    <Button type="button" variant="outline" size="sm" disabled={disabled || !allowed || loading || page <= 1} onClick={() => setPage(current => current - 1)}>{t("common.previous")}</Button>
    <Button type="button" variant="outline" size="sm" disabled={disabled || !allowed || loading || !data?.hasNext} onClick={() => setPage(current => current + 1)}>{t("common.next")}</Button>
  </div>;
  return <section aria-label={label} className="min-w-0">
    <Field id={id} label={label} required={!parentCategory}>
      <SearchableSelect id={id} label={label} value={value} onChange={onChange} disabled={disabled || !allowed} loading={loading}
        placeholder={t(kind === "products" ? "pricing.chooseProduct" : parentCategory ? "catalog.categories.noParent" : kind === "brands" ? "catalog.products.chooseBrand" : "catalog.products.chooseCategory")}
        search={search} onSearchChange={value => { setSearch(value); setPage(1); }} searchLabel={searchLabel}
        options={[...(parentCategory ? [{ value: "", label: t("catalog.categories.noParent") }] : []), ...items.map(item => ({ value: item.id, label: kind === "products" && item.sku ? `${item.sku} · ${item.name}` : item.name }))]}>
        {feedback}
        {pagination}
      </SearchableSelect>
    </Field>
  </section>;
}
