import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Layers } from "lucide-react";
import { toast } from "sonner";
import { useAuth } from "@/auth/use-auth";
import {
  confirmCutoff,
  generateWaves,
  packWave,
  releaseWave,
  searchWaves,
  WAREHOUSE_PERMISSIONS,
  type WaveDto,
} from "@/api/warehouse";
import { Button } from "@/components/ui/button";
import { EntityEmpty, EntityPageHeader, EntityStatusBadge, ErrorBand } from "@/components/list";
import { describe } from "@/lib/list-helpers";
import { useT } from "@/i18n/locale-provider";
import { JobCard, newIdempotencyKey, useOpsWarehouse, WarehousePicker } from "./ops-helpers";

function WaveCard({ wave }: { wave: WaveDto }) {
  const t = useT();
  const { user } = useAuth();
  const perms = user?.permissions ?? [];
  const queryClient = useQueryClient();
  const pending = wave.tasks.filter((task) => task.status === "Pending").length;
  const packed = wave.tasks.filter((task) => task.status === "Packed" || task.status === "Picked").length;

  const release = useMutation({
    mutationFn: () => releaseWave(wave.id, newIdempotencyKey()),
    onSuccess: async () => {
      toast.success(t("ops.waveReleased", "Wave released"));
      await queryClient.invalidateQueries({ queryKey: ["warehouse", "waves"] });
    },
    onError: (error) => toast.error(describe(error)),
  });

  const pack = useMutation({
    mutationFn: () => {
      const orderIds = [...new Set(wave.tasks.map((task) => task.orderId))];
      return packWave(wave.id, orderIds, newIdempotencyKey());
    },
    onSuccess: async (tote) => {
      toast.success(t("ops.packedTote", "Packed tote {sscc}").replace("{sscc}", tote.sscc));
      await queryClient.invalidateQueries({ queryKey: ["warehouse", "waves"] });
    },
    onError: (error) => toast.error(describe(error)),
  });

  return (
    <JobCard>
      <div className="flex items-start justify-between gap-3">
        <div>
          <code className="font-mono text-[13px] font-medium">{wave.number}</code>
          <p className="text-[12px] text-[var(--color-muted-foreground)]">
            {wave.zone} · {wave.businessDate} · {wave.tasks.length} {t("ops.tasks", "tasks")}
            {pending > 0 ? ` · ${pending} pending` : ""}
            {packed > 0 ? ` · ${packed} picked` : ""}
          </p>
        </div>
        <EntityStatusBadge tone={wave.status === "Completed" ? "success" : wave.status === "Released" ? "warning" : "default"}>
          {wave.status}
        </EntityStatusBadge>
      </div>
      <div className="mt-3 flex flex-wrap gap-2">
        {wave.status === "Draft" && perms.includes(WAREHOUSE_PERMISSIONS.wavesRelease) ? (
          <Button data-testid={`wave-release-${wave.id}`} disabled={release.isPending} onClick={() => release.mutate()}>
            {t("ops.release", "Release FEFO")}
          </Button>
        ) : null}
        {wave.status === "Completed" && perms.includes(WAREHOUSE_PERMISSIONS.packCreate) ? (
          <Button data-testid={`wave-pack-${wave.id}`} disabled={pack.isPending} onClick={() => pack.mutate()}>
            {t("ops.packTote", "Pack tote")}
          </Button>
        ) : null}
      </div>
    </JobCard>
  );
}

export function WavesPage() {
  const t = useT();
  const { user } = useAuth();
  const perms = user?.permissions ?? [];
  const { warehouses, warehouseId, setWarehouseId, isLoading, isError, error } = useOpsWarehouse();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState<"cutoff" | "generate" | null>(null);

  const query = useQuery({
    queryKey: ["warehouse", "waves", warehouseId],
    queryFn: () => searchWaves(warehouseId!),
    enabled: !!warehouseId,
  });

  const runCutoff = async () => {
    if (!warehouseId) return;
    setBusy("cutoff");
    try {
      const result = await confirmCutoff(warehouseId, newIdempotencyKey());
      toast.success(t("ops.cutoffDone", "Cutoff locked {n} orders").replace("{n}", String(result.ordersLocked)));
      await queryClient.invalidateQueries({ queryKey: ["warehouse", "waves"] });
    } catch (err) {
      toast.error(describe(err));
    } finally {
      setBusy(null);
    }
  };

  const runGenerate = async () => {
    if (!warehouseId) return;
    setBusy("generate");
    try {
      const waves = await generateWaves(warehouseId, newIdempotencyKey());
      toast.success(t("ops.wavesGenerated", "Generated {n} waves").replace("{n}", String(waves.length)));
      await queryClient.invalidateQueries({ queryKey: ["warehouse", "waves"] });
    } catch (err) {
      toast.error(describe(err));
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="space-y-4 sm:space-y-6">
      <EntityPageHeader
        icon={Layers}
        title={t("ops.wavesTitle", "Waves")}
        description={t("ops.wavesDescription", "Cutoff, generate by zone, release FEFO allocation, then pack a tote.")}
      />
      <WarehousePicker
        warehouses={warehouses}
        warehouseId={warehouseId}
        onChange={setWarehouseId}
        loading={isLoading}
      />
      <div className="flex flex-wrap gap-2">
        {perms.includes(WAREHOUSE_PERMISSIONS.wavesCutoff) ? (
          <Button variant="outline" disabled={!warehouseId || busy !== null} onClick={() => void runCutoff()}>
            {t("ops.cutoff", "Confirm cutoff")}
          </Button>
        ) : null}
        {perms.includes(WAREHOUSE_PERMISSIONS.wavesGenerate) ? (
          <Button data-testid="generate-waves" disabled={!warehouseId || busy !== null} onClick={() => void runGenerate()}>
            {t("ops.generateWaves", "Generate waves")}
          </Button>
        ) : null}
      </div>
      {isError ? <ErrorBand message={describe(error)} /> : null}
      {query.isError ? <ErrorBand message={describe(query.error)} /> : null}
      {(query.data ?? []).length === 0 && !query.isLoading ? (
        <EntityEmpty
          icon={Layers}
          title={t("ops.noWaves", "No waves")}
          body={t("ops.noWavesBody", "Run cutoff then generate waves for this warehouse.")}
        />
      ) : (
        <div className="space-y-3">
          {(query.data ?? []).map((wave) => (
            <WaveCard key={wave.id} wave={wave} />
          ))}
        </div>
      )}
    </div>
  );
}
