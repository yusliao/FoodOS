import { apiFetch } from "@/lib/api-client";
import type { PagedResponse } from "@/api/catalog";

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

export type WarehouseDto = {
  id: string;
  code: string;
  name: string;
  city: string;
  timeZoneId: string;
  createdAtUtc: string;
};

export type SearchWarehousesParams = {
  search?: string;
  pageNumber?: number;
  pageSize?: number;
};

export function searchWarehouses(
  params: SearchWarehousesParams = {},
): Promise<PagedResponse<WarehouseDto>> {
  const query = new URLSearchParams();
  if (params.search) query.set("search", params.search);
  query.set("pageNumber", String(params.pageNumber ?? 1));
  query.set("pageSize", String(params.pageSize ?? 20));
  return apiFetch<PagedResponse<WarehouseDto>>(`/api/v1/inventory/warehouses?${query.toString()}`);
}
