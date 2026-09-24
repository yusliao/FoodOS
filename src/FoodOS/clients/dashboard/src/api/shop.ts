import { apiFetch } from "@/lib/api-client";

export type PagedResponse<T> = {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
  hasPrevious: boolean;
};

export type ShopStoreDto = {
  id: string;
  code: string;
  name: string;
  address: string;
  deliveryWindow?: string | null;
};

export type ShopProductDto = {
  id: string;
  sku: string;
  name: string;
  description?: string | null;
  brandId: string;
  categoryId: string;
  unitPrice: number;
  currency: string;
  priceSource: string;
  baseUom: string;
  catchWeight: boolean;
  thumbnailUrl?: string | null;
  isAvailable: boolean;
};

export type SearchShopProductsParams = {
  storeId?: string | null;
  search?: string;
  brandId?: string | null;
  categoryId?: string | null;
  pageNumber?: number;
  pageSize?: number;
};

export function getShopStores(): Promise<ShopStoreDto[]> {
  return apiFetch<ShopStoreDto[]>("/api/v1/shop/stores");
}

export function getShopStoreById(storeId: string): Promise<ShopStoreDto> {
  return apiFetch<ShopStoreDto>(`/api/v1/shop/stores/${encodeURIComponent(storeId)}`);
}

export function searchShopProducts(
  params: SearchShopProductsParams = {},
): Promise<PagedResponse<ShopProductDto>> {
  const query = new URLSearchParams();
  if (params.storeId) query.set("storeId", params.storeId);
  if (params.search) query.set("search", params.search);
  if (params.brandId) query.set("brandId", params.brandId);
  if (params.categoryId) query.set("categoryId", params.categoryId);
  query.set("pageNumber", String(params.pageNumber ?? 1));
  query.set("pageSize", String(params.pageSize ?? 20));
  return apiFetch<PagedResponse<ShopProductDto>>(`/api/v1/shop/products?${query.toString()}`);
}

export function getShopProductById(
  productId: string,
  storeId: string,
  quantity = 1,
): Promise<ShopProductDto> {
  const query = new URLSearchParams({ storeId, quantity: String(quantity) });
  return apiFetch<ShopProductDto>(
    `/api/v1/shop/products/${encodeURIComponent(productId)}?${query.toString()}`,
  );
}

export type ShopCartLineDto = {
  productId: string;
  quantity: number;
};

export type ShopCartDto = {
  id: string;
  storeId: string;
  lines: ShopCartLineDto[];
  updatedAt: string;
};

export type ShopCartLineInput = {
  productId: string;
  quantity: number;
};

export function getShopCart(storeId: string): Promise<ShopCartDto> {
  return apiFetch<ShopCartDto>(`/api/v1/shop/stores/${encodeURIComponent(storeId)}/cart`);
}

export function updateShopCart(storeId: string, lines: ShopCartLineInput[]): Promise<string> {
  return apiFetch<string>(`/api/v1/shop/stores/${encodeURIComponent(storeId)}/cart`, {
    method: "PUT",
    body: JSON.stringify({ lines }),
  });
}

export type ShopOrderLineDto = {
  id: string;
  productId: string;
  orderedQty: number;
  deliveredQty: number;
  returnedQty: number;
  shortageQty: number;
  shortageReason?: string | null;
  unitPrice: number;
  currency: string;
};

export type ShopOrderDto = {
  id: string;
  number: string;
  storeId: string;
  status: string;
  businessDate: string;
  cutoffAt: string;
  placedAt?: string | null;
  revision: number;
  warehouseConfirmationStatus: "NotTracked" | "Pending" | "Confirmed" | "Exception";
  warehouseConfirmationDetail?: string | null;
  warehouseConfirmationUpdatedAt?: string | null;
  lines: ShopOrderLineDto[];
};

export type SearchShopOrdersParams = {
  storeId?: string | null;
  pageNumber?: number;
  pageSize?: number;
  status?: string | null;
};

export function searchShopOrders(
  params: SearchShopOrdersParams = {},
): Promise<PagedResponse<ShopOrderDto>> {
  const query = new URLSearchParams();
  if (params.storeId) query.set("storeId", params.storeId);
  query.set("pageNumber", String(params.pageNumber ?? 1));
  query.set("pageSize", String(params.pageSize ?? 20));
  if (params.status) query.set("status", params.status);
  return apiFetch<PagedResponse<ShopOrderDto>>(`/api/v1/shop/orders?${query.toString()}`);
}

export function getShopOrderById(orderId: string): Promise<ShopOrderDto> {
  return apiFetch<ShopOrderDto>(`/api/v1/shop/orders/${encodeURIComponent(orderId)}`);
}

export function placeShopOrder(storeId: string, idempotencyKey: string): Promise<string> {
  return apiFetch<string>("/api/v1/shop/orders", {
    method: "POST",
    headers: { "Idempotency-Key": idempotencyKey },
    body: JSON.stringify({ storeId }),
  });
}

export type AmendShopOrderLineInput = {
  productId: string;
  quantity: number;
};

export function amendShopOrder(
  orderId: string,
  lines: AmendShopOrderLineInput[],
  idempotencyKey: string,
): Promise<string> {
  return apiFetch<string>(`/api/v1/shop/orders/${encodeURIComponent(orderId)}/amend`, {
    method: "POST",
    headers: { "Idempotency-Key": idempotencyKey },
    body: JSON.stringify({ lines }),
  });
}

export function cancelShopOrder(orderId: string, idempotencyKey: string): Promise<string> {
  return apiFetch<string>(`/api/v1/shop/orders/${encodeURIComponent(orderId)}/cancel`, {
    method: "POST",
    headers: { "Idempotency-Key": idempotencyKey },
  });
}

export type ShopAfterSalesTicketType = "Shortage" | "Damage" | "Return";

export type ShopAfterSalesTicketDto = {
  id: string;
  orderId: string;
  storeId: string;
  orderLineId: string;
  type: ShopAfterSalesTicketType | string;
  quantity: number;
  reason: string;
  status: string;
  createdAt: string;
};

export type CreateShopAfterSalesTicketInput = {
  orderId: string;
  orderLineId: string;
  type: ShopAfterSalesTicketType;
  quantity: number;
  reason: string;
};

export function searchShopAfterSalesTickets(
  storeId: string,
  orderId?: string | null,
): Promise<ShopAfterSalesTicketDto[]> {
  const query = new URLSearchParams({ storeId });
  if (orderId) query.set("orderId", orderId);
  return apiFetch<ShopAfterSalesTicketDto[]>(`/api/v1/shop/after-sales?${query.toString()}`);
}

export function createShopAfterSalesTicket(
  input: CreateShopAfterSalesTicketInput,
  idempotencyKey: string,
): Promise<ShopAfterSalesTicketDto> {
  return apiFetch<ShopAfterSalesTicketDto>("/api/v1/shop/after-sales", {
    method: "POST",
    headers: { "Idempotency-Key": idempotencyKey },
    body: JSON.stringify(input),
  });
}

export const SHOP_PERMISSIONS = {
  view: "Permissions.Ordering.Shop.View",
  order: "Permissions.Ordering.Shop.Order",
} as const;
