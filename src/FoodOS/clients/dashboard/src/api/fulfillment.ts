import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api-client";

export type FulfillmentCapabilities = {
  mode: "externalWms"; readiness: "notConfigured" | "ready";
  acceptsOrders: boolean; acceptsOrderChanges: boolean; localWarehouseExecution: false;
};
export function useFulfillmentCapabilities() {
  return useQuery({
    queryKey: ["fulfillment", "capabilities"],
    queryFn: ({ signal }) => apiFetch<FulfillmentCapabilities>("/api/v1/fulfillment/capabilities", { signal }),
    staleTime: 0, retry: false,
  });
}
