import { apiFetch } from "@/lib/api-client";

export type WarehouseDto = { id: string; code: string; name: string; city: string };
export function searchWarehouses(search: string, pageNumber: number, signal?: AbortSignal) {
  const query = new URLSearchParams({ search, pageNumber: String(pageNumber), pageSize: "20" });
  return apiFetch<{ items: WarehouseDto[]; totalCount: number; totalPages: number; hasNext: boolean; hasPrevious: boolean }>(
    `/api/v1/inventory/warehouses?${query}`, { signal },
  );
}
