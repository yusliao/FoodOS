import { apiFetch } from "@/lib/api-client";

export type OpsKpis = {
  date: string;
  fulfillmentRate: number;
  stockoutRate: number;
  shrinkageRate: number;
  temperatureComplianceRate: number | null;
  committedOrderCount: number;
  fulfilledOrderCount: number;
  orderedQty: number;
  inboundQty: number;
  lossQty: number;
};

export function getOpsKpis(date?: string, signal?: AbortSignal) {
  const qs = date ? `?date=${encodeURIComponent(date)}` : "";
  return apiFetch<OpsKpis>(`/api/v1/ops/kpis${qs}`, { signal });
}
