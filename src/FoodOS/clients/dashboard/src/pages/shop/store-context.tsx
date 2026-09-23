import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { getShopStores, type ShopStoreDto } from "@/api/shop";
import { SHOP_STORE_STORAGE_KEY } from "@/auth/session-scope";

type ShopStoreContextValue = {
  stores: ShopStoreDto[];
  storesLoading: boolean;
  storesError: unknown;
  retryStores: () => void;
  store: ShopStoreDto | null;
  storeId: string | null;
  setStoreId: (id: string | null) => void;
};

const ShopStoreContext = createContext<ShopStoreContextValue | null>(null);

function readStored(): string | null {
  if (typeof window === "undefined") return null;
  try {
    return window.localStorage.getItem(SHOP_STORE_STORAGE_KEY);
  } catch {
    return null;
  }
}

function writeStored(id: string | null) {
  try {
    if (id) window.localStorage.setItem(SHOP_STORE_STORAGE_KEY, id);
    else window.localStorage.removeItem(SHOP_STORE_STORAGE_KEY);
  } catch {
    /* storage unavailable */
  }
}

export function ShopStoreProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const storesQuery = useQuery({
    queryKey: ["shop", "stores"],
    queryFn: getShopStores,
    staleTime: 30_000,
  });
  const {
    data: storesData,
    error: storesError,
    isLoading: storesLoading,
    refetch: refetchStores,
  } = storesQuery;
  const retryStores = useCallback(() => {
    void refetchStores();
  }, [refetchStores]);

  const [storeId, setStoreIdState] = useState<string | null>(readStored);
  const previousStoreId = useRef(storeId);
  const stores = useMemo(() => storesData ?? [], [storesData]);

  const setStoreId = useCallback((id: string | null) => {
    setStoreIdState(id);
    writeStored(id);
  }, []);

  useEffect(() => {
    if (stores.length === 0) return;
    if (storeId && stores.some((s) => s.id === storeId)) return;
    setStoreId(stores[0]?.id ?? null);
  }, [stores, storeId, setStoreId]);

  useEffect(() => {
    const previous = previousStoreId.current;
    previousStoreId.current = storeId;
    if (!previous || previous === storeId) return;
    queryClient.removeQueries({
      predicate: (query) =>
        query.queryKey[0] === "shop" && query.queryKey.includes(previous),
    });
  }, [queryClient, storeId]);

  const store = useMemo(
    () => stores.find((s) => s.id === storeId) ?? null,
    [stores, storeId],
  );

  const value = useMemo<ShopStoreContextValue>(
    () => ({
      stores,
      storesLoading,
      storesError,
      retryStores,
      store,
      storeId: store?.id ?? null,
      setStoreId,
    }),
    [stores, storesLoading, storesError, retryStores, store, setStoreId],
  );

  return <ShopStoreContext.Provider value={value}>{children}</ShopStoreContext.Provider>;
}

export function useShopStore() {
  const ctx = useContext(ShopStoreContext);
  if (!ctx) throw new Error("useShopStore must be used within ShopStoreProvider");
  return ctx;
}
