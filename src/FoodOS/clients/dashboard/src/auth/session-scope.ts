export const SHOP_STORE_STORAGE_KEY = "foodos.shop.storeId";

/**
 * Remove choices that belong to the current authenticated customer scope.
 * Appearance and culture are device preferences, so they intentionally survive
 * account changes; the selected restaurant store must not.
 */
export function clearCustomerScopeSelections(): void {
  try {
    window.localStorage.removeItem(SHOP_STORE_STORAGE_KEY);
  } catch {
    /* storage unavailable */
  }
}
