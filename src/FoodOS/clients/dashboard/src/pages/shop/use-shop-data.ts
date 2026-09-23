import { useQueries } from "@tanstack/react-query";
import { getShopProductById, type ShopProductDto } from "@/api/shop";

export function useShopProducts(
  storeId: string | undefined,
  items: ReadonlyArray<{ productId: string; quantity: number }>,
) {
  const unique = [
    ...new Map(
      items.filter((item) => item.productId).map((item) => [item.productId, item]),
    ).values(),
  ];
  const queries = useQueries({
    queries: unique.map((item) => ({
      queryKey: ["shop", "products", item.productId, storeId, item.quantity],
      queryFn: () => getShopProductById(item.productId, storeId!, item.quantity),
      enabled: !!storeId,
      staleTime: 60_000,
    })),
  });

  const byId = new Map<string, ShopProductDto>();
  queries.forEach((query, index) => {
    if (query.data) byId.set(unique[index]!.productId, query.data);
  });

  return {
    byId,
    isLoading: queries.some((query) => query.isLoading),
    isError: queries.some((query) => query.isError),
  };
}
