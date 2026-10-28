import { apiFetch } from "@/lib/api-client";

const base = "/api/v1/ordering/order-desk";

export type StoreContact = { id: string; name: string; phone: string };

export type OrderDeskCaller = {
  storeId: string; storeCode: string; storeName: string; address: string; deliveryWindow: string | null;
  customerOrgId: string; customerOrgCode: string; customerOrgName: string; creditHold: boolean;
  canPlaceOrders: boolean; blockingReason: "customerAccountMissing" | "addressMissing" | "creditHold" | null;
  contacts: StoreContact[]; lastOrderAt: string | null;
};

export type OrderDeskProduct = {
  productId: string; sku: string; name: string; baseUom: string; catchWeight: boolean; thumbnailUrl: string | null;
  unitPrice: number; currency: string; priceSource: string; isAvailable: boolean; availabilityStatus: string;
};

export type OrderDeskQuoteLine = {
  productId: string; quantity: number; unitPrice: number; currency: string; priceSource: string; lineTotal: number;
};

export type OrderDeskOrderLine = {
  lineId: string; productId: string; sku: string; name: string; baseUom: string;
  orderedQty: number; shortageQty: number; unitPrice: number; currency: string;
};

export type OrderDeskOrder = {
  id: string; number: string; status: string; businessDate: string; placedAt: string | null; cutoffAt: string;
  revision: number; placementChannel: string | null; warehouseConfirmationStatus: string;
  totalAmount: number; currency: string; lines: OrderDeskOrderLine[];
};

export type OrderDeskFrequentProduct = {
  productId: string; sku: string; name: string; baseUom: string; isActive: boolean;
  orderCount: number; totalQty: number; lastQty: number; lastOrderedAt: string;
};

export type OrderDeskStoreHistory = { recentOrders: OrderDeskOrder[]; frequentProducts: OrderDeskFrequentProduct[] };

export type OrderDeskLineInput = { productId: string; quantity: number };

export function searchCallers(term: string, signal?: AbortSignal) {
  return apiFetch<OrderDeskCaller[]>(`${base}/callers?${new URLSearchParams({ term, limit: "10" })}`, { signal });
}

export function suggestProducts(storeId: string, term: string, signal?: AbortSignal) {
  return apiFetch<OrderDeskProduct[]>(
    `${base}/stores/${encodeURIComponent(storeId)}/products?${new URLSearchParams({ term, limit: "10" })}`, { signal });
}

export function quoteLines(storeId: string, lines: OrderDeskLineInput[], signal?: AbortSignal) {
  return apiFetch<OrderDeskQuoteLine[]>(`${base}/stores/${encodeURIComponent(storeId)}/quote`, {
    method: "POST", body: JSON.stringify({ lines }), signal,
  });
}

export function getStoreHistory(storeId: string, signal?: AbortSignal) {
  return apiFetch<OrderDeskStoreHistory>(`${base}/stores/${encodeURIComponent(storeId)}/history`, { signal });
}

export function placeOrderDeskOrder(input: { storeId: string; lines: OrderDeskLineInput[]; key: string }) {
  return apiFetch<string>(`${base}/stores/${encodeURIComponent(input.storeId)}/orders`, {
    method: "POST", body: JSON.stringify({ lines: input.lines }), headers: { "Idempotency-Key": input.key },
  });
}

export function getStoreContacts(storeId: string, signal?: AbortSignal) {
  return apiFetch<StoreContact[]>(`/api/v1/ordering/stores/${encodeURIComponent(storeId)}/contacts`, { signal });
}

export function replaceStoreContacts(input: { storeId: string; contacts: { name: string; phone: string }[] }) {
  return apiFetch<StoreContact[]>(`/api/v1/ordering/stores/${encodeURIComponent(input.storeId)}/contacts`, {
    method: "PUT", body: JSON.stringify({ contacts: input.contacts }),
  });
}
