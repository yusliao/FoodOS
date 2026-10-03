import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { getWmsAvailability } from "@/api/wms";
import type { ProductDto } from "@/api/catalog";
import { useAuth } from "@/auth/use-auth";
import { Button } from "@/components/ui/button";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { useLocale } from "@/i18n/locale-provider";
import { WmsPermissions } from "@/lib/permissions";
import { describe } from "@/pages/customers/request-error";

export function ProductInventory({ product }: { product: ProductDto }) {
  const { t, culture } = useLocale();
  const { user } = useAuth();
  const [open, setOpen] = useState(false);
  const allowed = user?.permissions.includes(WmsPermissions.View) ?? false;
  const query = useQuery({
    queryKey: ["wms", "availability", product.sku, product.baseUom],
    queryFn: ({ signal }) => getWmsAvailability(product.sku, product.baseUom, signal),
    enabled: open && allowed,
    staleTime: 0,
  });
  if (!allowed) return null;
  const inventory = query.data?.inventory;
  const known = ["available", "insufficient", "notConfigured", "notSynced", "stale"].includes(inventory?.status ?? "");
  return <>
    <Button size="sm" variant="outline" onClick={() => setOpen(true)}>{t("catalog.inventory.view")}</Button>
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("catalog.inventory.title")} · {product.name}</DialogTitle>
          <DialogDescription>{t("catalog.inventory.hint")}</DialogDescription>
        </DialogHeader>
        <DialogBody className="space-y-4">
          {query.isPending && <p role="status">{t("catalog.loading")}</p>}
          {query.isError && <p role="alert">{describe(query.error, t("catalog.loadFailed"))}</p>}
          {query.isSuccess && inventory && <>
            <p role="status">{t(`catalog.inventory.${known ? inventory.status : "unknown"}`)}</p>
            <dl className="grid grid-cols-2 gap-3 text-sm">
              <dt>{t("catalog.inventory.warehouse")}</dt><dd>{query.data.warehouseCode || "—"}</dd>
              <dt>{t("catalog.products.sku")}</dt><dd>{product.sku}</dd>
              <dt>{t("catalog.inventory.quantity")}</dt><dd>{inventory.asOf ? `${inventory.availableQuantity.toLocaleString(culture)} ${inventory.uom}` : "—"}</dd>
              <dt>{t("catalog.inventory.asOf")}</dt><dd>{inventory.asOf ? new Date(inventory.asOf).toLocaleString(culture) : "—"}</dd>
            </dl>
          </>}
          <Button type="button" variant="outline" disabled={query.isFetching} onClick={() => void query.refetch()}>{t("catalog.inventory.refresh")}</Button>
        </DialogBody>
      </DialogContent>
    </Dialog>
  </>;
}
