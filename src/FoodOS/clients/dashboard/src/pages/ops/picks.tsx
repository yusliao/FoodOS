import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ScanLine } from "lucide-react";
import { toast } from "sonner";
import { useAuth } from "@/auth/use-auth";
import { confirmPickTask, getMyPickTasks, WAREHOUSE_PERMISSIONS, type PickTaskDto } from "@/api/warehouse";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { EntityEmpty, EntityPageHeader, EntityStatusBadge, ErrorBand, Field } from "@/components/list";
import { describe } from "@/lib/list-helpers";
import { useT } from "@/i18n/locale-provider";
import { JobCard, newIdempotencyKey, useOpsWarehouse, WarehousePicker } from "./ops-helpers";

function PickCard({ task }: { task: PickTaskDto }) {
  const t = useT();
  const { user } = useAuth();
  const canConfirm = user?.permissions.includes(WAREHOUSE_PERMISSIONS.picksConfirm) ?? false;
  const queryClient = useQueryClient();
  const [scannedLotId, setScannedLotId] = useState(task.lotId ?? "");

  const mutation = useMutation({
    mutationFn: () => confirmPickTask(task.id, scannedLotId.trim(), newIdempotencyKey()),
    onSuccess: async () => {
      toast.success(t("ops.pickConfirmed", "Pick confirmed"));
      await queryClient.invalidateQueries({ queryKey: ["warehouse", "pick-tasks"] });
    },
    onError: (error) => toast.error(describe(error)),
  });

  return (
    <JobCard>
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-[13px] font-medium">
            {t("ops.allocatedLot", "Allocated lot")} {task.lotNo ?? task.lotId}
          </p>
          <p className="text-[12px] text-[var(--color-muted-foreground)]">
            {task.quantity}
            {task.shortageQty > 0 ? ` · shortage ${task.shortageQty}` : ""}
          </p>
        </div>
        <EntityStatusBadge tone="warning">{task.status}</EntityStatusBadge>
      </div>
      {canConfirm ? (
        <div className="mt-3 space-y-2">
          <Field id={`scan-${task.id}`} label={t("ops.scanLot", "Scan lot id")} required>
            <Input
              id={`scan-${task.id}`}
              data-testid={`pick-scan-${task.id}`}
              value={scannedLotId}
              onChange={(e) => setScannedLotId(e.target.value)}
              autoComplete="off"
              inputMode="text"
            />
          </Field>
          <Button
            data-testid={`pick-confirm-${task.id}`}
            disabled={!scannedLotId.trim() || mutation.isPending}
            onClick={() => mutation.mutate()}
          >
            {t("ops.confirmPick", "Confirm pick")}
          </Button>
        </div>
      ) : null}
    </JobCard>
  );
}

export function PicksPage() {
  const t = useT();
  const { warehouses, warehouseId, setWarehouseId, isLoading, isError, error } = useOpsWarehouse();
  const query = useQuery({
    queryKey: ["warehouse", "pick-tasks", warehouseId],
    queryFn: () => getMyPickTasks(warehouseId),
    enabled: !!warehouseId,
  });

  return (
    <div className="space-y-4 sm:space-y-6">
      <EntityPageHeader
        icon={ScanLine}
        title={t("ops.picksTitle", "Pick tasks")}
        description={t("ops.picksDescription", "Scan the allocated lot. A wrong lot is rejected.")}
      />
      <WarehousePicker
        warehouses={warehouses}
        warehouseId={warehouseId}
        onChange={setWarehouseId}
        loading={isLoading}
      />
      {isError ? <ErrorBand message={describe(error)} /> : null}
      {query.isError ? <ErrorBand message={describe(query.error)} /> : null}
      {(query.data ?? []).length === 0 && !query.isLoading ? (
        <EntityEmpty
          icon={ScanLine}
          title={t("ops.noPicks", "No open picks")}
          body={t("ops.noPicksBody", "Release a wave to allocate FEFO lots onto pick tasks.")}
        />
      ) : (
        <div className="space-y-3">
          {(query.data ?? []).map((task) => (
            <PickCard key={task.id} task={task} />
          ))}
        </div>
      )}
    </div>
  );
}
