import { useFulfillmentCapabilities } from "@/api/fulfillment";
import { Button } from "@/components/ui/button";
import { useT } from "@/i18n/locale-provider";

export function WmsStatusNotice() {
  const t = useT();
  const status = useFulfillmentCapabilities();
  return <section className="space-y-3 rounded-lg border p-4" aria-label={t("wms.title")}>
    <h2 className="font-semibold">{t("wms.title")}</h2>
    <p role={status.isError ? "alert" : "status"} className="text-sm">
      {status.isPending ? t("wms.loading") : status.isError ? t("wms.failed") : t("wms.unavailable")}
    </p>
    <p className="text-sm">{t("wms.boundary")}</p>
    {status.isError && <Button type="button" variant="outline" disabled={status.isFetching} onClick={() => void status.refetch()}>{t("wms.retry")}</Button>}
  </section>;
}
