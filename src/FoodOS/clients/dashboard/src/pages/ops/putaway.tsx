import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Forklift } from "lucide-react";
import { toast } from "sonner";
import { useAuth } from "@/auth/use-auth";
import {
  confirmPutaway,
  searchLocations,
  searchPutawayTasks,
  WAREHOUSE_PERMISSIONS,
  type PutawayTaskDto,
} from "@/api/warehouse";
import { Button } from "@/components/ui/button";
import { Combobox, EntityEmpty, EntityPageHeader, EntityStatusBadge, ErrorBand } from "@/components/list";
import { describe } from "@/lib/list-helpers";
import { useT } from "@/i18n/locale-provider";
import { JobCard, newIdempotencyKey, useOpsWarehouse, WarehousePicker } from "./ops-helpers";

function PutawayCard({ task, warehouseId }: { task: PutawayTaskDto; warehouseId: string }) {
  const t = useT();
  const { user } = useAuth();
  const canConfirm = user?.permissions.includes(WAREHOUSE_PERMISSIONS.putawayConfirm) ?? false;
  const queryClient = useQueryClient();
  const locationsQuery = useQuery({
    queryKey: ["warehouse", "locations", warehouseId, task.zoneId],
    queryFn: () => searchLocations(warehouseId, task.zoneId),
    enabled: task.status === "Pending",
  });
  const locations = (locationsQuery.data ?? []).filter((l) => l.type === "Storage" || l.type === "Pick");
  const suggested = task.suggestedLocationId ?? locations[0]?.id ?? null;
  const [locationId, setLocationId] = useState<string | null>(suggested);

  const mutation = useMutation({
    mutationFn: () => confirmPutaway(task.id, locationId!, newIdempotencyKey()),
    onSuccess: async () => {
      toast.success(t("ops.putawayConfirmed", "Putaway confirmed"));
      await queryClient.invalidateQueries({ queryKey: ["warehouse", "putaway-tasks"] });
    },
    onError: (error) => toast.error(describe(error)),
  });

  return (
    <JobCard>
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="font-mono text-[13px]">{task.lotId}</p>
          <p className="text-[12px] text-[var(--color-muted-foreground)]">
            {task.zone} · {task.quantity} · {task.source}
          </p>
        </div>
        <EntityStatusBadge tone={task.status === "Pending" ? "warning" : task.status === "Completed" ? "success" : "default"}>
          {task.status}
        </EntityStatusBadge>
      </div>
      {task.status === "Pending" && canConfirm ? (
        <div className="mt-3 flex flex-col gap-2 sm:flex-row sm:items-end">
          <div className="min-w-0 flex-1">
            <Combobox
              label={t("ops.location", "Location")}
              value={locationId}
              onChange={setLocationId}
              options={locations.map((l) => ({ value: l.id, label: l.code, hint: l.type }))}
              placeholder={t("ops.chooseLocation", "Storage or pick bin")}
            />
          </div>
          <Button
            data-testid={`putaway-confirm-${task.id}`}
            disabled={!locationId || mutation.isPending}
            onClick={() => mutation.mutate()}
          >
            {t("ops.confirmPutaway", "Confirm putaway")}
          </Button>
        </div>
      ) : null}
    </JobCard>
  );
}

export function PutawayPage() {
  const t = useT();
  const { warehouses, warehouseId, setWarehouseId, isLoading, isError, error } = useOpsWarehouse();
  const query = useQuery({
    queryKey: ["warehouse", "putaway-tasks", warehouseId],
    queryFn: () => searchPutawayTasks(warehouseId!),
    enabled: !!warehouseId,
  });
  const tasks = useMemo(
    () => (query.data ?? []).filter((t) => t.status !== "Cancelled"),
    [query.data],
  );

  return (
    <div className="space-y-4 sm:space-y-6">
      <EntityPageHeader
        icon={Forklift}
        title={t("ops.putawayTitle", "Putaway")}
        description={t("ops.putawayDescription", "Confirm inbound or return lots onto a same-zone storage or pick location.")}
      />
      <WarehousePicker
        warehouses={warehouses}
        warehouseId={warehouseId}
        onChange={setWarehouseId}
        loading={isLoading}
      />
      {isError ? <ErrorBand message={describe(error)} /> : null}
      {query.isError ? <ErrorBand message={describe(query.error)} /> : null}
      {tasks.length === 0 && !query.isLoading ? (
        <EntityEmpty
          icon={Forklift}
          title={t("ops.noPutaway", "No putaway tasks")}
          body={t("ops.noPutawayBody", "QC pass and on-truck returns create pending putaway tasks.")}
        />
      ) : (
        <div className="space-y-3">
          {tasks.map((task) => (
            <PutawayCard key={task.id} task={task} warehouseId={warehouseId!} />
          ))}
        </div>
      )}
    </div>
  );
}
