import { apiFetch } from "@/lib/api-client";

export type LocationDto = {
  id: string;
  warehouseId: string;
  zoneId: string;
  code: string;
  type: string;
};

export type PickTaskDto = {
  id: string;
  waveId: string;
  orderId: string;
  orderLineId: string;
  productId: string;
  locationId: string;
  lotId?: string | null;
  lotNo?: string | null;
  quantity: number;
  shortageQty: number;
  status: string;
};

export type WaveDto = {
  id: string;
  number: string;
  dailyPlanId: string;
  warehouseId: string;
  zoneId: string;
  zone: string;
  businessDate: string;
  status: string;
  createdAt: string;
  tasks: PickTaskDto[];
};

export type PutawayTaskDto = {
  id: string;
  warehouseId: string;
  zoneId: string;
  zone: string;
  productId: string;
  lotId: string;
  quantity: number;
  suggestedLocationId?: string | null;
  locationId?: string | null;
  source: string;
  status: string;
  createdAt: string;
};

export type PackToteDto = {
  id: string;
  waveId: string;
  sscc: string;
  dockLocationId?: string | null;
  status: string;
  orderIds: string[];
  packedAt: string;
};

export type CutoffResultDto = {
  dailyPlanId: string;
  warehouseId: string;
  businessDate: string;
  cutoffAt: string;
  ordersLocked: number;
};

function qs(params: Record<string, string | undefined>): string {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value) query.set(key, value);
  }
  const text = query.toString();
  return text ? `?${text}` : "";
}

export function searchLocations(warehouseId: string, zoneId?: string | null): Promise<LocationDto[]> {
  return apiFetch<LocationDto[]>(
    `/api/v1/warehouse/locations${qs({ warehouseId, zoneId: zoneId ?? undefined })}`,
  );
}

export function searchPutawayTasks(warehouseId: string, status?: string | null): Promise<PutawayTaskDto[]> {
  return apiFetch<PutawayTaskDto[]>(
    `/api/v1/warehouse/putaway-tasks${qs({ warehouseId, status: status ?? undefined })}`,
  );
}

export function confirmPutaway(
  putawayTaskId: string,
  locationId: string,
  idempotencyKey: string,
): Promise<PutawayTaskDto> {
  return apiFetch<PutawayTaskDto>(
    `/api/v1/warehouse/putaway-tasks/${encodeURIComponent(putawayTaskId)}/confirm`,
    {
      method: "POST",
      headers: { "Idempotency-Key": idempotencyKey },
      body: JSON.stringify({ putawayTaskId, locationId }),
    },
  );
}

export function searchWaves(warehouseId: string, businessDate?: string | null): Promise<WaveDto[]> {
  return apiFetch<WaveDto[]>(
    `/api/v1/warehouse/waves${qs({ warehouseId, businessDate: businessDate ?? undefined })}`,
  );
}

export function generateWaves(warehouseId: string, idempotencyKey: string, businessDate?: string | null): Promise<WaveDto[]> {
  return apiFetch<WaveDto[]>("/api/v1/warehouse/waves", {
    method: "POST",
    headers: { "Idempotency-Key": idempotencyKey },
    body: JSON.stringify({ warehouseId, businessDate: businessDate || null }),
  });
}

export function releaseWave(waveId: string, idempotencyKey: string): Promise<WaveDto> {
  return apiFetch<WaveDto>(`/api/v1/warehouse/waves/${encodeURIComponent(waveId)}/release`, {
    method: "POST",
    headers: { "Idempotency-Key": idempotencyKey },
  });
}

export function packWave(
  waveId: string,
  orderIds: string[],
  idempotencyKey: string,
  sscc?: string | null,
): Promise<PackToteDto> {
  return apiFetch<PackToteDto>(`/api/v1/warehouse/waves/${encodeURIComponent(waveId)}/pack`, {
    method: "POST",
    headers: { "Idempotency-Key": idempotencyKey },
    body: JSON.stringify({ waveId, orderIds, sscc: sscc || null }),
  });
}

export function getMyPickTasks(warehouseId?: string | null): Promise<PickTaskDto[]> {
  return apiFetch<PickTaskDto[]>(
    `/api/v1/warehouse/pick-tasks/mine${qs({ warehouseId: warehouseId ?? undefined })}`,
  );
}

export function confirmPickTask(
  pickTaskId: string,
  scannedLotId: string,
  idempotencyKey: string,
): Promise<PickTaskDto> {
  return apiFetch<PickTaskDto>(
    `/api/v1/warehouse/pick-tasks/${encodeURIComponent(pickTaskId)}/confirm`,
    {
      method: "POST",
      headers: { "Idempotency-Key": idempotencyKey },
      body: JSON.stringify({ pickTaskId, scannedLotId }),
    },
  );
}

export function confirmCutoff(warehouseId: string, idempotencyKey: string): Promise<CutoffResultDto> {
  return apiFetch<CutoffResultDto>(
    `/api/v1/warehouse/warehouses/${encodeURIComponent(warehouseId)}/cutoff`,
    {
      method: "POST",
      headers: { "Idempotency-Key": idempotencyKey },
    },
  );
}

export const WAREHOUSE_PERMISSIONS = {
  locationsView: "Permissions.Warehouse.Locations.View",
  wavesView: "Permissions.Warehouse.Waves.View",
  wavesCutoff: "Permissions.Warehouse.Waves.Cutoff",
  wavesGenerate: "Permissions.Warehouse.Waves.Generate",
  wavesRelease: "Permissions.Warehouse.Waves.Release",
  picksView: "Permissions.Warehouse.Picks.View",
  picksConfirm: "Permissions.Warehouse.Picks.Confirm",
  putawayView: "Permissions.Warehouse.Putaway.View",
  putawayConfirm: "Permissions.Warehouse.Putaway.Confirm",
  packCreate: "Permissions.Warehouse.Pack.Create",
} as const;
