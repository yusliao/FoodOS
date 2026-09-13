import { apiFetch } from "@/lib/api-client";

export type ShipmentLineLotDto = {
  orderLineId: string;
  productId: string;
  zone: string;
  lotId: string;
  lotNo: string;
  quantity: number;
};

export type ShipmentLineDto = {
  id: string;
  orderId: string;
  storeId: string;
  toteId?: string | null;
  lots: ShipmentLineLotDto[];
};

export type ProofOfDeliveryDto = {
  id: string;
  signedQtyJson: string;
  photoFileIds: string[];
  signerName: string;
  geo?: string | null;
  signedAt: string;
};

export type ShipmentStopDto = {
  id: string;
  storeId: string;
  sequence: number;
  window?: string | null;
  status: string;
  proofOfDelivery?: ProofOfDeliveryDto | null;
};

export type ShipmentDto = {
  id: string;
  number: string;
  routeId: string;
  warehouseId: string;
  businessDate: string;
  vehicleId: string;
  driverId: string;
  status: string;
  createdAt: string;
  stops: ShipmentStopDto[];
  lines: ShipmentLineDto[];
  returns: Array<{
    id: string;
    orderId: string;
    productId: string;
    lotId: string;
    quantity: number;
    reason: string;
  }>;
};

export type PodSignedLine = {
  orderLineId: string;
  lotId: string;
  signedQty: number;
};

function qs(params: Record<string, string | undefined>): string {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value) query.set(key, value);
  }
  const text = query.toString();
  return text ? `?${text}` : "";
}

export function searchShipments(warehouseId: string, businessDate?: string | null): Promise<ShipmentDto[]> {
  return apiFetch<ShipmentDto[]>(
    `/api/v1/logistics/shipments${qs({ warehouseId, businessDate: businessDate ?? undefined })}`,
  );
}

export function getMyShipments(): Promise<ShipmentDto[]> {
  return apiFetch<ShipmentDto[]>("/api/v1/logistics/shipments/mine");
}

export function loadShipment(
  shipmentId: string,
  body: { orderIds?: string[]; toteIds?: string[] },
  idempotencyKey: string,
): Promise<ShipmentDto> {
  return apiFetch<ShipmentDto>(`/api/v1/logistics/shipments/${encodeURIComponent(shipmentId)}/load`, {
    method: "POST",
    headers: { "Idempotency-Key": idempotencyKey },
    body: JSON.stringify({ shipmentId, ...body }),
  });
}

export function departShipment(shipmentId: string, idempotencyKey: string): Promise<ShipmentDto> {
  return apiFetch<ShipmentDto>(`/api/v1/logistics/shipments/${encodeURIComponent(shipmentId)}/depart`, {
    method: "POST",
    headers: { "Idempotency-Key": idempotencyKey },
  });
}

export function confirmPod(
  stopId: string,
  body: { lines: PodSignedLine[]; signerName: string; photoFileIds?: string[]; geo?: string | null },
  idempotencyKey: string,
): Promise<ShipmentDto> {
  return apiFetch<ShipmentDto>(`/api/v1/logistics/stops/${encodeURIComponent(stopId)}/pod`, {
    method: "POST",
    headers: { "Idempotency-Key": idempotencyKey },
    body: JSON.stringify({ stopId, ...body }),
  });
}

export const LOGISTICS_PERMISSIONS = {
  shipmentsView: "Permissions.Logistics.Shipments.View",
  shipmentsLoad: "Permissions.Logistics.Shipments.Load",
  shipmentsDepart: "Permissions.Logistics.Shipments.Depart",
  podConfirm: "Permissions.Logistics.POD.Confirm",
} as const;
