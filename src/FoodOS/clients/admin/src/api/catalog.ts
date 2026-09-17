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

export type BrandDto = {
  id: string;
  name: string;
  slug: string;
  description?: string | null;
  logoUrl?: string | null;
  createdAtUtc: string;
};

export type CategoryDto = {
  id: string;
  name: string;
  slug: string;
  description?: string | null;
  parentCategoryId?: string | null;
  createdAtUtc: string;
};

export type ProductDto = {
  id: string;
  sku: string;
  name: string;
  slug: string;
  description?: string | null;
  brandId: string;
  categoryId: string;
  price: { amount: number; currency: string };
  isActive: boolean;
  createdAtUtc: string;
};

type SearchParams = { search?: string; pageNumber?: number; pageSize?: number };

function searchUrl(path: string, params: SearchParams, extra?: Record<string, string>) {
  const query = new URLSearchParams({
    pageNumber: String(params.pageNumber ?? 1),
    pageSize: String(params.pageSize ?? 20),
    ...extra,
  });
  if (params.search) query.set("search", params.search);
  return `${path}?${query.toString()}`;
}

export function searchBrands(params: SearchParams = {}, signal?: AbortSignal) {
  return apiFetch<PagedResponse<BrandDto>>(searchUrl("/api/v1/catalog/brands", params), { signal });
}

export function createBrand(input: { name: string; description?: string; logoUrl?: string }) {
  return apiFetch<string>("/api/v1/catalog/brands", { method: "POST", body: JSON.stringify(input) });
}

export function updateBrand(input: { brandId: string; name: string; description?: string; logoUrl?: string }) {
  return apiFetch<string>(`/api/v1/catalog/brands/${encodeURIComponent(input.brandId)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function deleteBrand(id: string) {
  return apiFetch<void>(`/api/v1/catalog/brands/${encodeURIComponent(id)}`, { method: "DELETE" });
}

export function searchCategories(params: SearchParams = {}, signal?: AbortSignal) {
  return apiFetch<PagedResponse<CategoryDto>>(
    searchUrl("/api/v1/catalog/categories", { ...params, pageSize: params.pageSize ?? 50 }),
    { signal },
  );
}

export function createCategory(input: { name: string; description?: string; parentCategoryId?: string | null }) {
  return apiFetch<string>("/api/v1/catalog/categories", { method: "POST", body: JSON.stringify(input) });
}

export function updateCategory(input: { categoryId: string; name: string; description?: string; parentCategoryId?: string | null }) {
  return apiFetch<string>(`/api/v1/catalog/categories/${encodeURIComponent(input.categoryId)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function deleteCategory(id: string) {
  return apiFetch<void>(`/api/v1/catalog/categories/${encodeURIComponent(id)}`, { method: "DELETE" });
}

export function searchProducts(
  params: SearchParams & { brandId?: string; categoryId?: string; isActive?: boolean },
  signal?: AbortSignal,
) {
  const extra: Record<string, string> = {};
  if (params.brandId) extra.brandId = params.brandId;
  if (params.categoryId) extra.categoryId = params.categoryId;
  if (params.isActive !== undefined) extra.isActive = String(params.isActive);
  return apiFetch<PagedResponse<ProductDto>>(searchUrl("/api/v1/catalog/products", params, extra), { signal });
}

export function createProduct(input: {
  sku: string;
  name: string;
  description?: string;
  brandId: string;
  categoryId: string;
  priceAmount: number;
  priceCurrency: string;
  stock: number;
}) {
  return apiFetch<string>("/api/v1/catalog/products", { method: "POST", body: JSON.stringify(input) });
}

export function updateProduct(input: {
  productId: string;
  name: string;
  description?: string;
  brandId: string;
  categoryId: string;
  isActive: boolean;
}) {
  return apiFetch<string>(`/api/v1/catalog/products/${encodeURIComponent(input.productId)}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function changeProductPrice(input: { productId: string; amount: number; currency: string }) {
  return apiFetch<string>(`/api/v1/catalog/products/${encodeURIComponent(input.productId)}/price`, {
    method: "PATCH",
    body: JSON.stringify(input),
  });
}

export function deleteProduct(id: string) {
  return apiFetch<void>(`/api/v1/catalog/products/${encodeURIComponent(id)}`, { method: "DELETE" });
}

export type PriceListLineDto = {
  id: string;
  productId: string;
  minQty: number;
  unitPrice: number;
  currency: string;
};

export type PriceListDto = {
  id: string;
  name: string;
  customerOrgId?: string | null;
  priority: number;
  validFrom: string;
  validTo?: string | null;
  lines: PriceListLineDto[];
};

export type PriceQuoteDto = {
  customerOrgId: string;
  productId: string;
  quantity: number;
  unitPrice: number;
  currency: string;
  source: string;
};

export function getPriceLists(customerOrgId = "", signal?: AbortSignal) {
  const query = new URLSearchParams();
  if (customerOrgId) query.set("customerOrgId", customerOrgId);
  const suffix = query.size > 0 ? `?${query}` : "";
  return apiFetch<PriceListDto[]>(`/api/v1/catalog/price-lists${suffix}`, { signal });
}

export function createPriceList(input: {
  name: string;
  customerOrgId?: string | null;
  priority: number;
  validFrom: string;
  validTo?: string | null;
}, idempotencyKey: string) {
  return apiFetch<string>("/api/v1/catalog/price-lists", {
    method: "POST",
    headers: { "Idempotency-Key": idempotencyKey },
    body: JSON.stringify({ ...input, lines: [] }),
  });
}

export function upsertPriceListLine(input: {
  priceListId: string;
  productId: string;
  minQty: number;
  unitPrice: number;
  currency: string;
}, idempotencyKey: string) {
  return apiFetch<string>(`/api/v1/catalog/price-lists/${encodeURIComponent(input.priceListId)}/lines`, {
    method: "PUT",
    headers: { "Idempotency-Key": idempotencyKey },
    body: JSON.stringify(input),
  });
}

export function upsertPriceLock(input: {
  customerOrgId: string;
  productId: string;
  unitPrice: number;
  currency: string;
  until: string;
}, idempotencyKey: string) {
  return apiFetch<string>("/api/v1/catalog/price-locks", {
    method: "PUT",
    headers: { "Idempotency-Key": idempotencyKey },
    body: JSON.stringify(input),
  });
}

export function quoteProductPrice(input: {
  customerOrgId: string;
  productId: string;
  quantity: number;
}, signal?: AbortSignal) {
  const query = new URLSearchParams({
    customerOrgId: input.customerOrgId,
    productId: input.productId,
    quantity: String(input.quantity),
  });
  return apiFetch<PriceQuoteDto>(`/api/v1/catalog/quotes?${query}`, { signal });
}
