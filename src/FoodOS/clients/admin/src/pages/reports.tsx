import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Activity } from "lucide-react";
import { getOpsKpis } from "@/api/ops";
import { useAuth } from "@/auth/use-auth";
import { EntityPageHeader, ErrorBand, Field, LoadingRow } from "@/components/list";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useLocale } from "@/i18n/locale-provider";
import { OpsPermissions } from "@/lib/permissions";
import { describe } from "@/pages/customers/request-error";

export function ReportsPage() {
  const { t, culture } = useLocale();
  const { user } = useAuth();
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const parsed = new Date(`${date}T00:00:00Z`);
  const valid = /^\d{4}-\d{2}-\d{2}$/.test(date) && date >= "2000-01-01" && date <= "2100-12-31"
    && !Number.isNaN(parsed.getTime()) && parsed.toISOString().slice(0, 10) === date;
  const canView = !!user?.permissions.includes(OpsPermissions.Kpis.View);
  const query = useQuery({ queryKey: ["ops", "kpis", date], queryFn: ({ signal }) => getOpsKpis(date, signal), enabled: canView && valid });
  const data = query.data;
  const percent = new Intl.NumberFormat(culture, { style: "percent", maximumFractionDigits: 2 });
  const number = new Intl.NumberFormat(culture, { maximumFractionDigits: 4 });
  const empty = data && data.committedOrderCount === 0 && data.fulfilledOrderCount === 0 && data.orderedQty === 0 && data.inboundQty === 0 && data.lossQty === 0 && data.temperatureComplianceRate == null;
  return <div className="space-y-6">
    <EntityPageHeader icon={Activity} title={t("reports.title")} description={t("reports.description")} />
    <p role="note" className="rounded-lg border p-4 text-sm">{t("reports.boundary")}</p>
    <div className="max-w-xs"><Field id="report-date" label={t("reports.date")} error={!valid ? t("reports.invalidDate") : undefined}>
      <Input id="report-date" type="date" min="2000-01-01" max="2100-12-31" value={date} aria-invalid={!valid} onChange={event => setDate(event.target.value)} />
    </Field></div>
    {valid && query.isPending && <LoadingRow label={t("reports.loading")} />}
    {valid && query.isError && <><ErrorBand message={describe(query.error, t("reports.failed"))} /><Button variant="outline" disabled={query.isFetching} onClick={() => void query.refetch()}>{t("workbench.retry")}</Button></>}
    {valid && query.isSuccess && data && <>
      <p className="text-sm">{t("reports.resultDate")}: <time dateTime={data.date}>{data.date}</time></p>
      {empty && <p role="status">{t("reports.empty")}</p>}
      <dl className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        {([
          ["fulfillmentRate", data.fulfillmentRate, true], ["stockoutRate", data.stockoutRate, true],
          ["shrinkageRate", data.shrinkageRate, true], ["temperatureComplianceRate", data.temperatureComplianceRate, true],
          ["committedOrderCount", data.committedOrderCount, false], ["fulfilledOrderCount", data.fulfilledOrderCount, false],
          ["orderedQty", data.orderedQty, false], ["inboundQty", data.inboundQty, false], ["lossQty", data.lossQty, false],
        ] as const).map(([key, value, rate]) => <div key={key} className="min-w-0 rounded-xl border p-4">
          <dt className="text-sm text-[var(--color-muted-foreground)]">{t(`reports.${key}`)}</dt>
          <dd className="mt-2 break-all text-2xl font-semibold">{value == null ? t("reports.unavailable") : rate ? percent.format(value) : number.format(value)}</dd>
        </div>)}
      </dl>
    </>}
    <section className="space-y-2 text-sm"><h2 className="font-semibold">{t("reports.definitions")}</h2>
      <p>{t("reports.orderBasis")}</p><p>{t("reports.stockoutBasis")}</p><p>{t("reports.inventoryBasis")}</p><p>{t("reports.temperatureBasis")}</p>
    </section>
  </div>;
}
