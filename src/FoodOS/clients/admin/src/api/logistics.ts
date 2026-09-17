import { apiFetch } from "@/lib/api-client";

export type RouteInput = { warehouseId: string; code: string; storeIds: string[]; defaultVehicleId: string | null };
export type DeliveryRoute = RouteInput & { id: string };
export function searchRoutes(warehouseId: string, signal?: AbortSignal) {
  return apiFetch<DeliveryRoute[]>(`/api/v1/logistics/routes?${new URLSearchParams({ warehouseId })}`, { signal });
}
export function createRoute(input: { body: RouteInput; key: string }) {
  return apiFetch<string>("/api/v1/logistics/routes", { method: "POST", body: JSON.stringify(input.body), headers: { "Idempotency-Key": input.key } });
}

export type DriverInput = { userId: string; phone: string };
export type Driver = DriverInput & { id: string };
export function searchDrivers(signal?: AbortSignal) {
  return apiFetch<Driver[]>("/api/v1/logistics/drivers", { signal });
}
export function createDriver(input: { body: DriverInput; key: string }) {
  return apiFetch<string>("/api/v1/logistics/drivers", {
    method: "POST", body: JSON.stringify(input.body), headers: { "Idempotency-Key": input.key },
  });
}

export type VehicleInput = { plate: string; compartmentZones: string; payloadKg: number };
export type Vehicle = VehicleInput & { id: string };
export function searchVehicles(signal?: AbortSignal) {
  return apiFetch<Vehicle[]>("/api/v1/logistics/vehicles", { signal });
}
export function createVehicle(input: { body: VehicleInput; key: string }) {
  return apiFetch<string>("/api/v1/logistics/vehicles", {
    method: "POST", body: JSON.stringify(input.body), headers: { "Idempotency-Key": input.key },
  });
}
