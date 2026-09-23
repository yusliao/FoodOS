import { apiFetch } from "@/lib/api-client";
import type { PagedResponse } from "@/lib/api-types";

export type StoreDto = {
  id: string;
  customerOrgId: string;
  code: string;
  name: string;
  address: string;
  defaultWarehouseId: string;
  defaultRouteId?: string | null;
  deliveryWindow?: string | null;
  createdAtUtc: string;
};

export type CartLineDto = {
  id: string;
  productId: string;
  quantity: number;
  zone: string;
};

export type CartDto = {
  id: string;
  storeId: string;
  lines: CartLineDto[];
  updatedAt: string;
};

export type CartLineInput = {
  productId: string;
  quantity: number;
};

export type SalesOrderLineLotDto = {
  lotId: string;
  lotNo: string;
  shippedQty: number;
  deliveredQty: number;
  returnedQty: number;
};

export type SalesOrderLineDto = {
  id: string;
  productId: string;
  zone: string;
  orderedQty: number;
  reservedQty: number;
  deliveredQty: number;
  returnedQty: number;
  shortageQty: number;
  shortageReason?: string | null;
  varianceReason?: string | null;
  unitPrice: number;
  currency: string;
  reservationId?: string | null;
  lots: SalesOrderLineLotDto[];
};

export type SalesOrderStatus =
  | "Draft"
  | "Reserved"
  | "Planned"
  | "Picking"
  | "Packed"
  | "InTransit"
  | "Received"
  | "Reconciled"
  | "Cancelled";

export type SalesOrderDto = {
  id: string;
  number: string;
  storeId: string;
  customerOrgId: string;
  warehouseId: string;
  status: SalesOrderStatus | string;
  businessDate: string;
  cutoffAt: string;
  placedAt?: string | null;
  revision: number;
  lines: SalesOrderLineDto[];
};

export type SearchOrdersParams = {
  storeId?: string | null;
  pageNumber?: number;
  pageSize?: number;
};

export function getStores(customerOrgId?: string | null): Promise<StoreDto[]> {
  const query = new URLSearchParams();
  if (customerOrgId) query.set("customerOrgId", customerOrgId);
  const qs = query.toString();
  return apiFetch<StoreDto[]>(`/api/v1/ordering/stores${qs ? `?${qs}` : ""}`);
}

export function getStoreById(storeId: string): Promise<StoreDto> {
  return apiFetch<StoreDto>(`/api/v1/ordering/stores/${encodeURIComponent(storeId)}`);
}

export function getCart(storeId: string): Promise<CartDto> {
  return apiFetch<CartDto>(`/api/v1/ordering/carts/${encodeURIComponent(storeId)}`);
}

export function updateCart(storeId: string, lines: CartLineInput[]): Promise<string> {
  return apiFetch<string>(`/api/v1/ordering/carts/${encodeURIComponent(storeId)}`, {
    method: "PUT",
    body: JSON.stringify({ storeId, lines }),
  });
}

export function searchOrders(
  params: SearchOrdersParams = {},
): Promise<PagedResponse<SalesOrderDto>> {
  const query = new URLSearchParams();
  if (params.storeId) query.set("storeId", params.storeId);
  query.set("pageNumber", String(params.pageNumber ?? 1));
  query.set("pageSize", String(params.pageSize ?? 20));
  return apiFetch<PagedResponse<SalesOrderDto>>(`/api/v1/ordering/orders?${query.toString()}`);
}

export function getOrderById(orderId: string): Promise<SalesOrderDto> {
  return apiFetch<SalesOrderDto>(`/api/v1/ordering/orders/${encodeURIComponent(orderId)}`);
}

export function placeOrder(storeId: string, idempotencyKey: string): Promise<string> {
  return apiFetch<string>("/api/v1/ordering/orders", {
    method: "POST",
    headers: { "Idempotency-Key": idempotencyKey },
    body: JSON.stringify({ storeId }),
  });
}

export type AmendOrderLineInput = {
  productId: string;
  quantity: number;
};

export function amendOrder(
  orderId: string,
  lines: AmendOrderLineInput[],
  idempotencyKey: string,
): Promise<string> {
  return apiFetch<string>(`/api/v1/ordering/orders/${encodeURIComponent(orderId)}/amend`, {
    method: "POST",
    headers: { "Idempotency-Key": idempotencyKey },
    body: JSON.stringify({ orderId, lines }),
  });
}

export function cancelOrder(orderId: string, idempotencyKey: string): Promise<string> {
  return apiFetch<string>(`/api/v1/ordering/orders/${encodeURIComponent(orderId)}/cancel`, {
    method: "POST",
    headers: { "Idempotency-Key": idempotencyKey },
  });
}

export type AfterSalesTicketType = "Shortage" | "Damage" | "Return";

export type AfterSalesTicketDto = {
  id: string;
  orderId: string;
  storeId: string;
  orderLineId: string;
  type: AfterSalesTicketType | string;
  quantity: number;
  reason: string;
  status: string;
  createdByUserId: string;
  createdAt: string;
};

export type CreateAfterSalesTicketInput = {
  orderId: string;
  orderLineId: string;
  type: AfterSalesTicketType;
  quantity: number;
  reason: string;
};

export function searchAfterSalesTickets(storeId: string, orderId?: string | null): Promise<AfterSalesTicketDto[]> {
  const query = new URLSearchParams();
  query.set("storeId", storeId);
  if (orderId) query.set("orderId", orderId);
  return apiFetch<AfterSalesTicketDto[]>(`/api/v1/ordering/after-sales?${query.toString()}`);
}

export function createAfterSalesTicket(
  input: CreateAfterSalesTicketInput,
  idempotencyKey: string,
): Promise<AfterSalesTicketDto> {
  return apiFetch<AfterSalesTicketDto>("/api/v1/ordering/after-sales", {
    method: "POST",
    headers: { "Idempotency-Key": idempotencyKey },
    body: JSON.stringify(input),
  });
}
