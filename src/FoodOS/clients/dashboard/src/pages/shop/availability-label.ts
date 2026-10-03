import type { ShopProductDto } from "@/api/shop";

export function availabilityLabelKey(product: Pick<ShopProductDto, "isAvailable" | "availabilityStatus">): string {
  if (product.isAvailable) return "shop.availableToOrder";
  switch (product.availabilityStatus) {
    case "insufficient": return "shop.outOfStock";
    case "notConfigured":
    case "notSynced": return "shop.inventoryPending";
    case "stale": return "shop.inventoryStale";
    case "warehouseMissing": return "shop.warehouseMissing";
    default: return "shop.inventoryUnknown";
  }
}
