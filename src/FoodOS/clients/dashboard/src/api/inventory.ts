import { apiFetch } from "@/lib/api-client";

export type AvailableQtyDto = {
  warehouseId: string;
  productId: string;
  available: number;
  zoneKind: string;
};

export type GetAvailableQtyParams = {
  warehouseId: string;
  productId: string;
  zone?: string | null;
};

export function getAvailableQty(params: GetAvailableQtyParams): Promise<AvailableQtyDto> {
  const query = new URLSearchParams();
  query.set("warehouseId", params.warehouseId);
  query.set("productId", params.productId);
  if (params.zone) query.set("zone", params.zone);
  return apiFetch<AvailableQtyDto>(`/api/v1/inventory/stock/available?${query.toString()}`);
}
