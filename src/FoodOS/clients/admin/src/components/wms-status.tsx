import { useFulfillmentCapabilities } from "@/api/fulfillment";
import { Button } from "@/components/ui/button";
import { useT } from "@/i18n/locale-provider";

export function WmsStatusNotice() {
  const t = useT();
  const status = useFulfillmentCapabilities();
  const reason = status.data?.blockingReasons?.[0];
  const reasonText = reason === "notConfigured" ? t("wms.notConfigured")
    : reason === "warehouseMappingMissing" ? t("wms.warehouseMappingMissing")
      : reason === "ownerMappingMissing" ? t("wms.ownerMappingMissing")
        : reason === "wmsUnreachable" ? t("wms.wmsUnreachable")
          : null;
  const canRetry = status.isError || (status.isSuccess && status.data.readiness !== "ready");
  return <section className="space-y-3 rounded-lg border p-4" aria-label={t("wms.title")}>
    <h2 className="font-semibold">{t("wms.title")}</h2>
    <p role={status.isError ? "alert" : "status"} className="text-sm">
      {status.isPending ? t("wms.loading") : status.isError ? t("wms.failed") : t("wms.unavailable")}
    </p>
    {reasonText ? <p className="text-sm text-[var(--color-muted-foreground)]">{reasonText}</p> : null}
    <p className="text-sm">{t("wms.boundary")}</p>
    {canRetry && <Button type="button" variant="outline" disabled={status.isFetching} onClick={() => void status.refetch()}>{t("wms.retry")}</Button>}
  </section>;
}
