import { Outlet } from "react-router-dom";
import { Store } from "lucide-react";
import { Combobox, ErrorBand } from "@/components/list";
import { describe } from "@/lib/list-helpers";
import { useT } from "@/i18n/locale-provider";
import { ShopStoreProvider, useShopStore } from "./store-context";

export function ShopLayout() {
  return (
    <ShopStoreProvider>
      <ShopLayoutBody />
    </ShopStoreProvider>
  );
}

function ShopLayoutBody() {
  const t = useT();
  const { stores, storesLoading, storesError, storeId, setStoreId, store } = useShopStore();

  return (
    <div className="space-y-4 sm:space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-2 text-[13px] text-[var(--color-muted-foreground)]">
          <Store className="size-4 shrink-0" aria-hidden />
          <span>{t("shop.selectStore", "Store")}</span>
        </div>
        <Combobox
          label={t("shop.selectStore", "Store")}
          variant="filter"
          searchable
          value={storeId}
          onChange={setStoreId}
          disabled={storesLoading || stores.length === 0}
          placeholder={
            storesLoading
              ? t("shop.loadingStores", "Loading stores…")
              : t("shop.chooseStore", "Choose a store")
          }
          options={stores.map((s) => ({
            value: s.id,
            label: s.name,
            hint: s.code,
          }))}
        />
      </div>

      {storesError ? <ErrorBand message={describe(storesError)} /> : null}

      {!storesLoading && stores.length === 0 ? (
        <ErrorBand message={t("shop.noStoresBody", "Ask an operator to create a customer and store before ordering.")} />
      ) : null}

      {!store && stores.length > 0 ? (
        <p className="text-[13px] text-[var(--color-muted-foreground)]">
          {t("shop.needStore", "Select a store to see contract prices and place orders.")}
        </p>
      ) : null}

      <Outlet />
    </div>
  );
}
