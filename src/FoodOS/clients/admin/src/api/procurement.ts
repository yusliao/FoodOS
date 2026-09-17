import { apiFetch } from "@/lib/api-client";

export type Supplier = {
  id: string; code: string; name: string; categories: string | null;
  leadDays: number; status: string; createdAtUtc: string;
};
export type SupplierInput = { code: string; name: string; categories: string | null; leadDays: number };
export function searchSuppliers(search: string, signal?: AbortSignal) {
  return apiFetch<Supplier[]>(`/api/v1/procurement/suppliers?${new URLSearchParams({ search })}`, { signal });
}
export function createSupplier(input: { body: SupplierInput; key: string }) {
  return apiFetch<string>("/api/v1/procurement/suppliers", {
    method: "POST", body: JSON.stringify(input.body), headers: { "Idempotency-Key": input.key },
  });
}

export type PurchaseOrder = {
  id: string; number: string; supplierId: string; warehouseId: string; status: string;
  expectedAt: string; createdAt: string;
  lines: { id: string; productId: string; zone: string; quantity: number; receivedQty: number; rejectedQty: number }[];
  appointment: { id: string; dockSlot: string; vehicleNo: string | null } | null;
  qualityChecks: { id: string; lineId: string; result: string; sampleQty: number; quantity: number; lotNo: string; lotId: string | null; note: string | null; checkedAt: string }[];
};
export function searchPurchaseOrders(search: string, signal?: AbortSignal) {
  return apiFetch<PurchaseOrder[]>(`/api/v1/procurement/purchase-orders?${new URLSearchParams({ search })}`, { signal });
}
export function sendPurchaseOrder(input: { id: string; key: string }) {
  return apiFetch<string>(`/api/v1/procurement/purchase-orders/${encodeURIComponent(input.id)}/send`, { method: "POST", headers: { "Idempotency-Key": input.key } });
}
export type PurchaseOrderInput = {
  supplierId: string; warehouseId: string; expectedAt: string;
  lines: { productId: string; zone: string; quantity: number }[];
};
export function createPurchaseOrder(input: { body: PurchaseOrderInput; key: string }) {
  return apiFetch<string>("/api/v1/procurement/purchase-orders", {
    method: "POST", body: JSON.stringify(input.body), headers: { "Idempotency-Key": input.key },
  });
}
export function appointPurchaseOrder(input: { id: string; key: string; dockSlot: string; vehicleNo: string | null }) {
  return apiFetch<string>(`/api/v1/procurement/purchase-orders/${encodeURIComponent(input.id)}/appointments`, {
    method: "POST", headers: { "Idempotency-Key": input.key }, body: JSON.stringify({ purchaseOrderId: input.id, dockSlot: input.dockSlot, vehicleNo: input.vehicleNo }),
  });
}
export type QualityInput = { purchaseOrderId: string; lineId: string; quantity: number; sampleQty: number; lotNo: string; expiryDate: string; manufacturedOn: string | null; note: string | null };
export function recordQuality(input: { body: QualityInput; result: "pass" | "fail"; key: string }) {
  return apiFetch<string>(`/api/v1/procurement/purchase-orders/${encodeURIComponent(input.body.purchaseOrderId)}/lines/${encodeURIComponent(input.body.lineId)}/qc/${input.result}`, {
    method: "POST", headers: { "Idempotency-Key": input.key }, body: JSON.stringify(input.body),
  });
}
