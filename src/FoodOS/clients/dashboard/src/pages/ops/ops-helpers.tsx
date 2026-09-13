import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Warehouse } from "lucide-react";
import { searchWarehouses, type WarehouseDto } from "@/api/inventory";
import { Combobox } from "@/components/list";
import { useT } from "@/i18n/locale-provider";

export function newIdempotencyKey(): string {
  return crypto.randomUUID();
}

export function todayIsoDate(): string {
  return new Date().toISOString().slice(0, 10);
}

export function useOpsWarehouse() {
  const query = useQuery({
    queryKey: ["inventory", "warehouses"],
    queryFn: () => searchWarehouses({ pageNumber: 1, pageSize: 50 }),
    staleTime: 60_000,
  });
  const warehouses = query.data?.items ?? [];
  const [warehouseId, setWarehouseId] = useState<string | null>(null);

  useEffect(() => {
    if (!warehouseId && warehouses[0]) {
      setWarehouseId(warehouses[0].id);
    }
  }, [warehouseId, warehouses]);

  const warehouse = warehouses.find((w) => w.id === warehouseId) ?? null;
  return { ...query, warehouses, warehouseId, setWarehouseId, warehouse };
}

export function JobCard({ children }: { children: React.ReactNode }) {
  return (
    <div className="rounded-xl border border-[var(--color-border)] bg-[var(--color-card)] p-4 shadow-xs">
      {children}
    </div>
  );
}

export function WarehousePicker({
  warehouses,
  warehouseId,
  onChange,
  loading,
}: {
  warehouses: WarehouseDto[];
  warehouseId: string | null;
  onChange: (id: string | null) => void;
  loading?: boolean;
}) {
  const t = useT();
  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <div className="flex items-center gap-2 text-[13px] text-[var(--color-muted-foreground)]">
        <Warehouse className="size-4 shrink-0" aria-hidden />
        <span>{t("ops.warehouse", "Warehouse")}</span>
      </div>
      <Combobox
        label={t("ops.warehouse", "Warehouse")}
        variant="filter"
        searchable
        value={warehouseId}
        onChange={onChange}
        disabled={loading || warehouses.length === 0}
        placeholder={
          loading ? t("ops.loadingWarehouses", "Loading warehouses…") : t("ops.chooseWarehouse", "Choose a warehouse")
        }
        options={warehouses.map((w) => ({
          value: w.id,
          label: w.name,
          hint: w.code,
        }))}
      />
    </div>
  );
}
