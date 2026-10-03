import { apiFetch } from "@/lib/api-client";

export type WmsProductAvailability = {
  warehouseCode: string;
  inventory: {
    sku: string;
    uom: string;
    availableQuantity: number;
    isAvailable: boolean;
    asOf: string | null;
    status: string;
  };
};

export function getWmsAvailability(sku: string, uom: string, signal?: AbortSignal) {
  const query = new URLSearchParams({ sku, uom });
  return apiFetch<WmsProductAvailability>(`/api/v1/wms/availability?${query}`, { signal });
}
