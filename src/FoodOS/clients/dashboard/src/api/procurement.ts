import { apiFetch } from "@/lib/api-client";

export type PurchaseOrderLineDto = {
  id: string;
  productId: string;
  zone: string;
  quantity: number;
  receivedQty: number;
  rejectedQty: number;
};

export type QualityCheckDto = {
  id: string;
  purchaseOrderId: string;
  lineId: string;
  inspectorUserId: string;
  result: string;
  sampleQty: number;
  quantity: number;
  lotNo: string;
  lotId?: string | null;
  note?: string | null;
  photoFileIds: string[];
  checkedAt: string;
};

export type PurchaseOrderDto = {
  id: string;
  number: string;
  supplierId: string;
  warehouseId: string;
  status: string;
  expectedAt: string;
  createdAt: string;
  lines: PurchaseOrderLineDto[];
  qualityChecks: QualityCheckDto[];
};

export type QualityCheckInput = {
  quantity: number;
  sampleQty: number;
  lotNo: string;
  expiryDate: string;
  manufacturedOn?: string | null;
  note?: string | null;
};

export function searchPurchaseOrders(search?: string): Promise<PurchaseOrderDto[]> {
  const query = new URLSearchParams();
  if (search) query.set("search", search);
  const qs = query.toString();
  return apiFetch<PurchaseOrderDto[]>(`/api/v1/procurement/purchase-orders${qs ? `?${qs}` : ""}`);
}

export function getPurchaseOrderById(purchaseOrderId: string): Promise<PurchaseOrderDto> {
  return apiFetch<PurchaseOrderDto>(
    `/api/v1/procurement/purchase-orders/${encodeURIComponent(purchaseOrderId)}`,
  );
}

export function passQualityCheck(
  purchaseOrderId: string,
  lineId: string,
  body: QualityCheckInput,
  idempotencyKey: string,
): Promise<string> {
  return apiFetch<string>(
    `/api/v1/procurement/purchase-orders/${encodeURIComponent(purchaseOrderId)}/lines/${encodeURIComponent(lineId)}/qc/pass`,
    {
      method: "POST",
      headers: { "Idempotency-Key": idempotencyKey },
      body: JSON.stringify({ purchaseOrderId, lineId, ...body }),
    },
  );
}

export function failQualityCheck(
  purchaseOrderId: string,
  lineId: string,
  body: QualityCheckInput,
  idempotencyKey: string,
): Promise<string> {
  return apiFetch<string>(
    `/api/v1/procurement/purchase-orders/${encodeURIComponent(purchaseOrderId)}/lines/${encodeURIComponent(lineId)}/qc/fail`,
    {
      method: "POST",
      headers: { "Idempotency-Key": idempotencyKey },
      body: JSON.stringify({ purchaseOrderId, lineId, ...body }),
    },
  );
}

export const PROCUREMENT_PERMISSIONS = {
  purchaseView: "Permissions.Procurement.Purchase.View",
  qualityView: "Permissions.Procurement.Quality.View",
  qualityPass: "Permissions.Procurement.Quality.Pass",
  qualityFail: "Permissions.Procurement.Quality.Fail",
} as const;
