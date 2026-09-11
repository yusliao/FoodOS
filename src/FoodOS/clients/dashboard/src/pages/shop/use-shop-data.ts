import { useMemo } from "react";
import { useQueries, useQuery } from "@tanstack/react-query";
import {
  getProductById,
  quoteProductPrice,
  type PriceQuoteDto,
  type ProductDto,
} from "@/api/catalog";
import { getAvailableQty, type AvailableQtyDto } from "@/api/inventory";

export function usePriceQuotes(
  customerOrgId: string | undefined,
  requests: ReadonlyArray<{ productId: string; quantity: number }>,
) {
  const query = useQuery({
    queryKey: ["catalog", "quotes", customerOrgId, requests],
    queryFn: async () => {
      const quotes = await Promise.all(
        requests.map((request) =>
          quoteProductPrice({
            customerOrgId: customerOrgId!,
            productId: request.productId,
            quantity: request.quantity,
          }),
        ),
      );
      return quotes;
    },
    enabled: !!customerOrgId && requests.length > 0,
    staleTime: 15_000,
  });

  const byProductId = useMemo(() => {
    const map = new Map<string, PriceQuoteDto>();
    for (const quote of query.data ?? []) {
      map.set(quote.productId, quote);
    }
    return map;
  }, [query.data]);

  return { byProductId, isLoading: query.isLoading, isError: query.isError };
}

export function useAvailableQty(
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

export function useAvailableQtys(
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

export function useProductsById(ids: readonly string[]) {
  const unique = [...new Set(ids.filter(Boolean))];
  const queries = useQueries({
    queries: unique.map((id) => ({
      queryKey: ["catalog", "products", id],
      queryFn: () => getProductById(id),
      staleTime: 60_000,
    })),
  });

  const byId = new Map<string, ProductDto>();
  queries.forEach((q, i) => {
    if (q.data) byId.set(unique[i]!, q.data);
  });

  return { byId, isLoading: queries.some((q) => q.isLoading) };
}
