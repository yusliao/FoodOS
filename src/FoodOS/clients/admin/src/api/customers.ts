import { apiFetch } from "@/lib/api-client";

// Operator-owned business records, not Identity tenants or customer Shop DTOs.
export type CustomerOrgDto = {
  id: string;
  customerTenantId: string | null;
  code: string;
  name: string;
  creditHold: boolean;
  createdAtUtc: string;
};

export type StoreDto = {
  id: string;
  customerTenantId: string | null;
  customerOrgId: string;
  code: string;
  name: string;
  address: string;
  defaultWarehouseId: string;
  defaultRouteId: string | null;
  deliveryWindow: string | null;
  createdAtUtc: string;
};

export type CreateCustomerInput = Pick<CustomerOrgDto, "code" | "name" | "creditHold" | "customerTenantId">;
export type CreateStoreInput = Pick<StoreDto, "customerOrgId" | "code" | "name" | "address" | "defaultWarehouseId" | "defaultRouteId" | "deliveryWindow">;

export function searchCustomers(search: string, signal?: AbortSignal) {
  return apiFetch<CustomerOrgDto[]>(`/api/v1/ordering/customer-orgs?${new URLSearchParams({ search })}`, { signal });
}

export function listStores(customerOrgId = "", signal?: AbortSignal) {
  const query = new URLSearchParams();
  if (customerOrgId) query.set("customerOrgId", customerOrgId);
  return apiFetch<StoreDto[]>(`/api/v1/ordering/stores?${query}`, { signal });
}

export function createCustomer(input: CreateCustomerInput, idempotencyKey: string) {
  return apiFetch<string>("/api/v1/ordering/customer-orgs", {
    method: "POST", body: JSON.stringify(input), headers: { "Idempotency-Key": idempotencyKey },
  });
}

export function createStore(input: CreateStoreInput, idempotencyKey: string) {
  return apiFetch<string>("/api/v1/ordering/stores", {
    method: "POST", body: JSON.stringify(input), headers: { "Idempotency-Key": idempotencyKey },
  });
}
