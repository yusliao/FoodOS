import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  getAvailableQty,
  searchWarehouses,
  type AvailableQtyDto,
} from "@/api/inventory";

/** P0 is a single warehouse; ops Catalog ATP uses the first warehouse on the tenant. */
export function useDefaultWarehouse() {
  const query = useQuery({
    queryKey: ["inventory", "warehouses"],
    queryFn: () => searchWarehouses({ pageNumber: 1, pageSize: 20 }),
    staleTime: 60_000,
  });

  return {
    warehouse: query.data?.items[0],
    isLoading: query.isLoading,
    isError: query.isError,
  };
}

export function useInventoryAtp(
  warehouseId: string | undefined,
  productId: string | undefined,
  zone: string | undefined,
) {
  return useQuery({
    queryKey: ["inventory", "available", warehouseId, productId, zone],
    queryFn: () =>
      getAvailableQty({
        warehouseId: warehouseId!,
        productId: productId!,
        zone: zone ?? null,
      }),
    enabled: !!warehouseId && !!productId,
    staleTime: 10_000,
  });
}

export function useInventoryAtps(
  warehouseId: string | undefined,
  items: ReadonlyArray<{ productId: string; zone?: string }>,
) {
  const query = useQuery({
    queryKey: ["inventory", "available-batch", warehouseId, items],
    queryFn: async () => {
      const rows = await Promise.all(
        items.map((item) =>
          getAvailableQty({
            warehouseId: warehouseId!,
            productId: item.productId,
            zone: item.zone ?? null,
          }),
        ),
      );
      return rows;
    },
    enabled: !!warehouseId && items.length > 0,
    staleTime: 10_000,
  });

  const byProductId = useMemo(() => {
    const map = new Map<string, AvailableQtyDto>();
    for (const row of query.data ?? []) {
      map.set(row.productId, row);
    }
    return map;
  }, [query.data]);

  return { byProductId, isLoading: query.isLoading };
}
