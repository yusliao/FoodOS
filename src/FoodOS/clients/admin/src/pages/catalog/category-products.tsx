import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { searchProducts, type CategoryDto } from "@/api/catalog";
import { ErrorBand, LoadingRow, Pagination } from "@/components/list";
import { Button } from "@/components/ui/button";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { useT } from "@/i18n/locale-provider";
import { describe } from "@/pages/customers/request-error";

/** 分类下的商品明细：上下架都列出来，编辑去商品列表页做。 */
export function CategoryProductsDialog({ category, onClose }: { category: CategoryDto; onClose: () => void }) {
  const t = useT();
  const [page, setPage] = useState(1);
  const query = useQuery({
    queryKey: ["catalog", "products", "by-category", category.id, page],
    queryFn: ({ signal }) => searchProducts({ categoryId: category.id, pageNumber: page, pageSize: 20 }, signal),
  });
  const money = (amount: number, currency: string) => new Intl.NumberFormat(undefined, { style: "currency", currency }).format(amount);
  return <Dialog open onOpenChange={open => !open && onClose()}>
    <DialogContent size="lg">
      <DialogHeader>
        <DialogTitle>{t("catalog.categories.productsTitle").replace("{name}", category.name)}</DialogTitle>
        <DialogDescription>{t("catalog.categories.productsHint")}</DialogDescription>
      </DialogHeader>
      <DialogBody className="space-y-3">
        {query.isPending && <LoadingRow label={t("catalog.loading")} />}
        {query.isError && <div className="space-y-2"><ErrorBand message={describe(query.error, t("catalog.loadFailed"))} /><Button variant="outline" onClick={() => void query.refetch()}>{t("workbench.retry")}</Button></div>}
        {query.isSuccess && query.data.items.length === 0 && <p role="status">{t("catalog.categories.productsEmpty")}</p>}
        {query.isSuccess && query.data.items.length > 0 && <div className="overflow-x-auto rounded-lg border border-[var(--color-border)]">
          <table className="w-full text-sm">
            <thead className="bg-[var(--color-muted)] text-left text-xs text-[var(--color-muted-foreground)]">
              <tr>
                <th className="px-3 py-2">{t("catalog.products.sku")}</th>
                <th className="px-3 py-2">{t("catalog.name")}</th>
                <th className="px-3 py-2">{t("catalog.products.brand")}</th>
                <th className="px-3 py-2">{t("catalog.categories.colUom")}</th>
                <th className="px-3 py-2 text-right">{t("catalog.categories.colPrice")}</th>
                <th className="px-3 py-2">{t("catalog.categories.colStatus")}</th>
              </tr>
            </thead>
            <tbody>
              {query.data.items.map(product => <tr key={product.id} className="border-t border-[var(--color-border)]">
                <td className="px-3 py-2 font-mono text-xs whitespace-nowrap">{product.sku}</td>
                <td className="px-3 py-2 break-words">{product.name}</td>
                <td className="px-3 py-2 break-words">{product.brandName ?? product.brandId}</td>
                <td className="px-3 py-2">{product.baseUom}</td>
                <td className="px-3 py-2 text-right whitespace-nowrap">{money(product.price.amount, product.price.currency)}</td>
                <td className="px-3 py-2 whitespace-nowrap">{t(product.isActive ? "catalog.products.active" : "catalog.products.hidden")}</td>
              </tr>)}
            </tbody>
          </table>
        </div>}
        {query.isSuccess && <Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} shown={query.data.items.length} hasPrev={query.data.hasPrevious} hasNext={query.data.hasNext} fetching={query.isFetching} onPrev={() => setPage(value => value - 1)} onNext={() => setPage(value => value + 1)} />}
      </DialogBody>
      <DialogFooter>
        <Button variant="outline" onClick={onClose}>{t("common.close")}</Button>
        <Button asChild><Link to={`/catalog/products?categoryId=${encodeURIComponent(category.id)}`}>{t("catalog.categories.openInProducts")}</Link></Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>;
}
