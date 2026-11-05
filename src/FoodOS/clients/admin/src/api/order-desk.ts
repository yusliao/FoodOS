import { apiFetch } from "@/lib/api-client";
import type { PagedResponse } from "@/api/catalog";

const base = "/api/v1/ordering/order-desk";

export type StoreContact = { id: string; name: string; phone: string };

export type OrderDeskCaller = {
  storeId: string; storeCode: string; storeName: string; address: string; deliveryWindow: string | null;
  customerOrgId: string; customerOrgCode: string; customerOrgName: string; creditHold: boolean;
  canPlaceOrders: boolean; blockingReason: "customerAccountMissing" | "addressMissing" | "creditHold" | null;
  contacts: StoreContact[]; lastOrderAt: string | null;
  /** Customer pricing level; absent when the customer pays the standard price. */
  levelCode?: string | null; levelName?: string | null; levelDiscountPercent?: number | null;
};

export type OrderDeskProduct = {
  productId: string; sku: string; name: string; baseUom: string; catchWeight: boolean; thumbnailUrl: string | null;
  unitPrice: number; currency: string; priceSource: string; isAvailable: boolean; availabilityStatus: string;
  categoryId?: string | null; categoryName?: string | null;
};

export type OrderDeskProductCategory = { id: string; name: string; productCount: number };

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

export type OrderDeskFrequentCaller = { caller: OrderDeskCaller; orderCount: number };

/** Stores the signed-in agent placed service orders for in the last 90 days, most frequent first. */
export function getMyCallers(signal?: AbortSignal) {
  return apiFetch<OrderDeskFrequentCaller[]>(`${base}/callers/mine?limit=12`, { signal });
}

/** Categories that hold active products, for the category filter. */
export function getProductCategories(signal?: AbortSignal) {
  return apiFetch<OrderDeskProductCategory[]>(`${base}/product-categories`, { signal });
}

/** One page of active products with the store's price; ordered by category and name unless a keyword is given. */
export function browseProducts(
  storeId: string,
  params: { categoryId?: string; term?: string; pageNumber: number; pageSize?: number },
  signal?: AbortSignal,
) {
  const query = new URLSearchParams({ pageNumber: String(params.pageNumber), pageSize: String(params.pageSize ?? 50) });
  if (params.categoryId) query.set("categoryId", params.categoryId);
  if (params.term) query.set("term", params.term);
  return apiFetch<PagedResponse<OrderDeskProduct>>(`${base}/stores/${encodeURIComponent(storeId)}/products?${query}`, { signal });
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

export function replaceStoreContacts(input: { storeId: string; contacts: { name: string; phone: string }[] }) {
  return apiFetch<StoreContact[]>(`/api/v1/ordering/stores/${encodeURIComponent(input.storeId)}/contacts`, {
    method: "PUT", body: JSON.stringify({ contacts: input.contacts }),
  });
}
