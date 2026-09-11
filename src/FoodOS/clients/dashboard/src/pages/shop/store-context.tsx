import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { getStores, type StoreDto } from "@/api/ordering";

const STORAGE_KEY = "foodos.shop.storeId";

type ShopStoreContextValue = {
  stores: StoreDto[];
  storesLoading: boolean;
  storesError: unknown;
  store: StoreDto | null;
  storeId: string | null;
  setStoreId: (id: string | null) => void;
};

const ShopStoreContext = createContext<ShopStoreContextValue | null>(null);

function readStored(): string | null {
  if (typeof window === "undefined") return null;
  try {
    return window.localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function writeStored(id: string | null) {
  try {
    if (id) window.localStorage.setItem(STORAGE_KEY, id);
    else window.localStorage.removeItem(STORAGE_KEY);
  } catch {
    /* storage unavailable */
  }
}

export function ShopStoreProvider({ children }: { children: ReactNode }) {
  const storesQuery = useQuery({
    queryKey: ["ordering", "stores"],
    queryFn: () => getStores(),
    staleTime: 30_000,
  });

  const [storeId, setStoreIdState] = useState<string | null>(readStored);
  const stores = useMemo(() => storesQuery.data ?? [], [storesQuery.data]);

  const setStoreId = useCallback((id: string | null) => {
    setStoreIdState(id);
    writeStored(id);
  }, []);

  useEffect(() => {
    if (stores.length === 0) return;
    if (storeId && stores.some((s) => s.id === storeId)) return;
    setStoreId(stores[0]?.id ?? null);
  }, [stores, storeId, setStoreId]);

  const store = useMemo(
    () => stores.find((s) => s.id === storeId) ?? null,
    [stores, storeId],
  );

  const value = useMemo<ShopStoreContextValue>(
    () => ({
      stores,
      storesLoading: storesQuery.isLoading,
      storesError: storesQuery.error,
      store,
      storeId: store?.id ?? null,
      setStoreId,
    }),
    [stores, storesQuery.isLoading, storesQuery.error, store, setStoreId],
  );

  return <ShopStoreContext.Provider value={value}>{children}</ShopStoreContext.Provider>;
}

export function useShopStore() {
  const ctx = useContext(ShopStoreContext);
  if (!ctx) throw new Error("useShopStore must be used within ShopStoreProvider");
  return ctx;
}
