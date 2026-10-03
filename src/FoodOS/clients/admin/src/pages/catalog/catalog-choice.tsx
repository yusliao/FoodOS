import { useEffect, useId, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Check, ChevronDown } from "lucide-react";
import { searchBrands, searchCategories, searchProducts, type PagedResponse } from "@/api/catalog";
import { useAuth } from "@/auth/use-auth";
import { CatalogPermissions } from "@/lib/permissions";
import { useT } from "@/i18n/locale-provider";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Field } from "@/components/list";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";

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
  const [open, setOpen] = useState(false);
  const searchRef = useRef<HTMLInputElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const timer = setTimeout(() => searchRef.current?.focus(), 0);
    return () => clearTimeout(timer);
  }, [open]);
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
  const label = t(kind === "products" ? "pricing.product" : parentCategory ? "catalog.categories.parent" : kind === "brands" ? "catalog.products.brandLabel" : "catalog.products.categoryLabel");
  const searchLabel = t(kind === "products" ? "catalog.products.searchProduct" : kind === "brands" ? "catalog.products.searchBrand" : "catalog.products.searchCategory");
  const generatedId = useId();
  const id = kind === "products" ? generatedId : parentCategory ? "category-parent" : `product-${kind}`;
  const searchInput = <Input ref={searchRef} type="search" aria-label={searchLabel} placeholder={searchLabel} value={search} disabled={disabled || !allowed} onChange={event => { setSearch(event.target.value); setPage(1); }} onKeyDown={event => {
    if (event.key === "Escape" || event.key === "Tab") return;
    event.stopPropagation();
    if (event.key === "Enter") event.preventDefault();
    if (event.key === "ArrowDown") {
      event.preventDefault();
      menuRef.current?.querySelector<HTMLElement>('[role="menuitemradio"]:not([data-disabled])')?.focus();
    }
  }} />;
  const feedback = <>
    {loading && <p role="status">{t("common.loading")}</p>}
    {failed && <div className="space-y-2"><p role="alert">{t(kind === "products" ? "pricing.productsFailed" : "catalog.products.lookupFailed")}</p><Button type="button" variant="outline" disabled={disabled || !allowed || loading} onClick={() => { if (allowed && !disabled) void query.refetch(); }}>{t("workbench.retry")}</Button></div>}
    {data && items.length === 0 && <p role="status">{t("common.emptyDefault")}</p>}
  </>;
  const pagination = <div className="flex flex-wrap gap-2">
    <Button type="button" variant="outline" size="sm" disabled={disabled || !allowed || loading || page <= 1} onClick={() => setPage(current => current - 1)}>{t("common.previous")}</Button>
    <Button type="button" variant="outline" size="sm" disabled={disabled || !allowed || loading || !data?.hasNext} onClick={() => setPage(current => current + 1)}>{t("common.next")}</Button>
  </div>;
  if (kind !== "products" && !parentCategory) {
    const current = items.find(item => item.id === value) ?? (selected?.id === value ? selected : undefined);
    const choices = value && !items.some(item => item.id === value) ? [{ id: value, name: current?.name ?? value }, ...items] : items;
    return <section aria-label={label} className="min-w-0">
      <DropdownMenu open={open} onOpenChange={next => { if (!disabled && allowed) setOpen(next); }}>
        <Field id={id} label={label} required>
          <DropdownMenuTrigger id={id} type="button" disabled={disabled || !allowed} className="flex h-10 w-full items-center justify-between gap-2 rounded-lg border bg-[var(--color-card)] px-3 text-left">
            <span className="truncate">{current?.name ?? (value || t(kind === "brands" ? "catalog.products.chooseBrand" : "catalog.products.chooseCategory"))}</span>
            <ChevronDown aria-hidden className="h-4 w-4 shrink-0" />
          </DropdownMenuTrigger>
        </Field>
        <DropdownMenuContent ref={menuRef} aria-label={label} align="start" className="w-[var(--radix-dropdown-menu-trigger-width)] min-w-0 space-y-2 p-2">
          {searchInput}
          {feedback}
          <div className="max-h-52 overflow-y-auto">
            {choices.map(item => <DropdownMenuItem key={item.id} role="menuitemradio" aria-checked={value === item.id} disabled={disabled || !allowed || loading} onSelect={() => {
              setSelected(item);
              onChange(item.id);
              setOpen(false);
            }}>
              <span className="min-w-0 flex-1 break-words">{item.name}</span>
              {value === item.id && <Check aria-hidden className="h-4 w-4 shrink-0" />}
            </DropdownMenuItem>)}
          </div>
          {pagination}
        </DropdownMenuContent>
      </DropdownMenu>
    </section>;
  }
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
    {searchInput}
    {feedback}
    {pagination}
  </section>;
}
