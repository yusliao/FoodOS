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
  /** Caller contacts for the customer service order desk (returned by the store list). */
  contacts?: StoreContactDto[] | null;
};

export type StoreContactDto = { id: string; name: string; phone: string };
export type StoreContactInput = { name: string; phone: string };

export type CreateCustomerInput = Pick<CustomerOrgDto, "code" | "name" | "creditHold" | "customerTenantId">;
export type CreateStoreInput = Pick<StoreDto, "customerOrgId" | "code" | "name" | "address" | "defaultWarehouseId" | "defaultRouteId" | "deliveryWindow"> & {
  contacts?: StoreContactInput[];
};

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

export function updateCustomer(input: Pick<CustomerOrgDto, "id" | "name" | "code">) {
  return apiFetch<void>("/api/v1/ordering/customer-orgs/details", {
    method: "PUT", body: JSON.stringify({ customerOrgId: input.id, name: input.name, code: input.code }),
  });
}

export function createStore(input: CreateStoreInput, idempotencyKey: string) {
  return apiFetch<string>("/api/v1/ordering/stores", {
    method: "POST", body: JSON.stringify(input), headers: { "Idempotency-Key": idempotencyKey },
  });
}

export function updateStore(input: Pick<StoreDto, "id" | "name" | "address">) {
  return apiFetch<void>("/api/v1/ordering/stores/details", {
    method: "PUT", body: JSON.stringify({ storeId: input.id, name: input.name, address: input.address }),
  });
}
