import { apiFetch } from "@/lib/api-client";
import type { PagedResponse } from "@/api/catalog";

export type OrderLine = {
  id: string; productId: string; zone: string; orderedQty: number; reservedQty: number;
  deliveredQty: number; returnedQty: number; shortageQty: number;
  shortageReason: string | null; varianceReason: string | null;
  unitPrice: number; currency: string; reservationId: string | null;
  lots: { lotId: string; lotNo: string; shippedQty: number; deliveredQty: number; returnedQty: number }[];
};
export type SalesOrder = {
  id: string; number: string; customerTenantId: string | null; storeId: string;
  customerOrgId: string; warehouseId: string; routeId: string | null; status: string;
  businessDate: string; cutoffAt: string; placedAt: string | null; revision: number; lines: OrderLine[];
};
export const orderStatuses = ["Draft", "Reserved", "Planned", "Picking", "Packed", "InTransit", "Received", "Reconciled", "Cancelled"] as const;
export function searchOrders(pageNumber: number, status: string, signal?: AbortSignal, storeId = "") {
  const query = new URLSearchParams({ pageNumber: String(pageNumber), pageSize: "20" });
  if (status) query.set("status", status);
  if (storeId) query.set("storeId", storeId);
  return apiFetch<PagedResponse<SalesOrder>>(`/api/v1/ordering/orders?${query}`, { signal });
}
export function getOrder(id: string, signal?: AbortSignal) {
  return apiFetch<SalesOrder>(`/api/v1/ordering/orders/${encodeURIComponent(id)}`, { signal });
}
export function reconcileOrder(input: { id: string; key: string }) {
  return apiFetch<string>(`/api/v1/ordering/orders/${encodeURIComponent(input.id)}/reconcile`, {
    method: "POST", headers: { "Idempotency-Key": input.key },
  });
}
export function cancelOrder(input: { id: string; key: string }) {
  return apiFetch<string>(`/api/v1/ordering/orders/${encodeURIComponent(input.id)}/cancel`, {
    method: "POST", headers: { "Idempotency-Key": input.key },
  });
}
export function amendOrder(input: { id: string; key: string; lines: { productId: string; quantity: number }[] }) {
  return apiFetch<string>(`/api/v1/ordering/orders/${encodeURIComponent(input.id)}/amend`, {
    method: "POST", headers: { "Idempotency-Key": input.key }, body: JSON.stringify({ orderId: input.id, lines: input.lines }),
  });
}

export type AfterSalesTicket = {
  id: string; customerTenantId: string | null; orderId: string; storeId: string;
  orderLineId: string; type: string; quantity: number; reason: string;
  status: string; createdByUserId: string; createdAt: string;
};
export type AfterSalesInput = { orderId: string; orderLineId: string; type: string; quantity: number; reason: string };
export function getAfterSales(storeId: string, orderId: string, signal?: AbortSignal) {
  return apiFetch<AfterSalesTicket[]>(`/api/v1/ordering/after-sales?${new URLSearchParams({ storeId, orderId })}`, { signal });
}
export function createAfterSales(input: { body: AfterSalesInput; key: string }) {
  return apiFetch<AfterSalesTicket>("/api/v1/ordering/after-sales", {
    method: "POST", body: JSON.stringify(input.body), headers: { "Idempotency-Key": input.key },
  });
}
