import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Truck } from "lucide-react";
import { toast } from "sonner";
import { useAuth } from "@/auth/use-auth";
import { Visibility } from "@/api/files";
import {
  confirmPod,
  createShipment,
  departShipment,
  loadShipment,
  LOGISTICS_PERMISSIONS,
  searchDrivers,
  searchRoutes,
  searchShipments,
  searchVehicles,
  type ShipmentDto,
} from "@/api/logistics";
import { FileDropzone } from "@/components/file/file-dropzone";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Combobox, EntityEmpty, EntityPageHeader, EntityStatusBadge, ErrorBand, Field } from "@/components/list";
import { describe } from "@/lib/list-helpers";
import { useT } from "@/i18n/locale-provider";
import { JobCard, newIdempotencyKey, todayIsoDate, useOpsWarehouse, WarehousePicker } from "./ops-helpers";

function ShipmentCard({ shipment }: { shipment: ShipmentDto }) {
  const t = useT();
  const { user } = useAuth();
  const perms = user?.permissions ?? [];
  const queryClient = useQueryClient();
  const [scan, setScan] = useState("");
  const [signerName, setSignerName] = useState("");
  const lots = useMemo(() => shipment.lines.flatMap((line) => line.lots.map((lot) => ({ ...lot, storeId: line.storeId }))), [shipment.lines]);
  const [signedQty, setSignedQty] = useState<Record<string, string>>(() => {
    const next: Record<string, string> = {};
    for (const lot of lots) {
      next[`${lot.orderLineId}:${lot.lotId}`] = String(lot.quantity);
    }
    return next;
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["logistics", "shipments"] });

  const load = useMutation({
    mutationFn: () => {
      const token = scan.trim();
      const isGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(token);
      if (!isGuid) {
        throw new Error(t("ops.scanGuid", "Scan an order id or tote id (GUID)."));
      }
      const asOrder = shipment.lines.some((l) => l.orderId.toLowerCase() === token.toLowerCase());
      return loadShipment(
        shipment.id,
        asOrder ? { orderIds: [token] } : { toteIds: [token] },
        newIdempotencyKey(),
      );
    },
    onSuccess: async () => {
      toast.success(t("ops.loaded", "Loaded"));
      setScan("");
      await invalidate();
    },
    onError: (error) => toast.error(describe(error)),
  });

  const depart = useMutation({
    mutationFn: () => departShipment(shipment.id, newIdempotencyKey()),
    onSuccess: async () => {
      toast.success(t("ops.departed", "Departed"));
      await invalidate();
    },
    onError: (error) => toast.error(describe(error)),
  });

  const openStop = shipment.stops.find((s) => s.status !== "Delivered");
  const [photoFileIds, setPhotoFileIds] = useState<string[]>([]);

  const pod = useMutation({
    mutationFn: (input: { stopId: string; signerName: string; photoFileIds: string[]; lines: Array<{ orderLineId: string; lotId: string; signedQty: number }> }) =>
      confirmPod(
        input.stopId,
        {
          signerName: input.signerName,
          photoFileIds: input.photoFileIds,
          lines: input.lines,
        },
        newIdempotencyKey(),
      ),
    onSuccess: async () => {
      toast.success(t("ops.podRecorded", "Proof of delivery recorded"));
      await invalidate();
    },
    onError: (error) => toast.error(describe(error)),
  });

  const canLoad = perms.includes(LOGISTICS_PERMISSIONS.shipmentsLoad) && (shipment.status === "Created" || shipment.status === "Loading");
  const canDepart = perms.includes(LOGISTICS_PERMISSIONS.shipmentsDepart) && shipment.status === "Loading";
  const canPod = perms.includes(LOGISTICS_PERMISSIONS.podConfirm) && shipment.status === "Departed" && !!openStop;

  return (
    <JobCard>
      <div className="flex items-start justify-between gap-3">
        <div>
          <code className="font-mono text-[13px] font-medium">{shipment.number}</code>
          <p className="text-[12px] text-[var(--color-muted-foreground)]">
            {shipment.businessDate} · {shipment.lines.length} {t("ops.orders", "orders")}
          </p>
        </div>
        <EntityStatusBadge tone={shipment.status === "Completed" ? "success" : shipment.status === "Departed" ? "info" : "warning"}>
          {shipment.status}
        </EntityStatusBadge>
      </div>

      {canLoad ? (
        <div className="mt-3 space-y-2">
          <Field id={`load-${shipment.id}`} label={t("ops.scanOrderOrTote", "Scan order or tote")}>
            <Input
              id={`load-${shipment.id}`}
              data-testid={`load-scan-${shipment.id}`}
              value={scan}
              onChange={(e) => setScan(e.target.value)}
              autoComplete="off"
            />
          </Field>
          <Button data-testid={`load-confirm-${shipment.id}`} disabled={!scan.trim() || load.isPending} onClick={() => load.mutate()}>
            {t("ops.load", "Load")}
          </Button>
        </div>
      ) : null}

      {canDepart ? (
        <Button className="mt-3" variant="outline" disabled={depart.isPending} onClick={() => depart.mutate()}>
          {t("ops.depart", "Depart")}
        </Button>
      ) : null}

      {canPod && openStop ? (
        <div className="mt-3 space-y-2">
          <Field id={`signer-${shipment.id}`} label={t("ops.signer", "Signer")} required>
            <Input id={`signer-${shipment.id}`} value={signerName} onChange={(e) => setSignerName(e.target.value)} />
          </Field>
          {shipment.lines
            .filter((l) => l.storeId === openStop.storeId)
            .flatMap((l) => l.lots)
            .map((lot) => {
              const key = `${lot.orderLineId}:${lot.lotId}`;
              return (
                <Field key={key} id={`signed-${key}`} label={`${lot.lotNo} (${lot.quantity})`}>
                  <Input
                    id={`signed-${key}`}
                    type="number"
                    min="0"
                    step="any"
                    value={signedQty[key] ?? ""}
                    onChange={(e) => setSignedQty((prev) => ({ ...prev, [key]: e.target.value }))}
                  />
                </Field>
              );
            })}
          <div>
            <p className="mb-2 text-[12px] text-[var(--color-muted-foreground)]">
              {t("ops.podPhotos", "Delivery photos")}
              {photoFileIds.length > 0 ? ` · ${photoFileIds.length}` : ""}
            </p>
            <FileDropzone
              options={{
                ownerType: "MyFiles",
                ownerId: openStop.id,
                category: "Image",
                visibility: Visibility.Private,
                allowedExtensions: [".jpg", ".jpeg", ".png", ".webp"],
              }}
              accept="image/jpeg,image/png,image/webp"
              onUploaded={(asset) => setPhotoFileIds((prev) => [...prev, asset.id])}
            />
          </div>
          <Button
            data-testid={`pod-confirm-${openStop.id}`}
            disabled={!signerName.trim() || pod.isPending}
            onClick={() => {
              const stopLots = shipment.lines.filter((l) => l.storeId === openStop.storeId).flatMap((l) => l.lots);
              pod.mutate({
                stopId: openStop.id,
                signerName: signerName.trim(),
                photoFileIds,
                lines: stopLots.map((lot) => ({
                  orderLineId: lot.orderLineId,
                  lotId: lot.lotId,
                  signedQty: Number(signedQty[`${lot.orderLineId}:${lot.lotId}`] ?? lot.quantity),
                })),
              });
            }}
          >
            {t("ops.confirmPod", "Confirm POD")}
          </Button>
        </div>
      ) : null}
    </JobCard>
  );
}

function CreateShipmentForm({ warehouseId }: { warehouseId: string }) {
  const t = useT();
  const { user } = useAuth();
  const perms = user?.permissions ?? [];
  const queryClient = useQueryClient();
  const canCreate = perms.includes(LOGISTICS_PERMISSIONS.shipmentsCreate);
  const [routeId, setRouteId] = useState<string | null>(null);
  const [vehicleId, setVehicleId] = useState<string | null>(null);
  const [driverId, setDriverId] = useState<string | null>(null);
  const [businessDate, setBusinessDate] = useState(todayIsoDate);

  const routes = useQuery({
    queryKey: ["logistics", "routes", warehouseId],
    queryFn: () => searchRoutes(warehouseId),
    enabled: canCreate,
  });
  const vehicles = useQuery({
    queryKey: ["logistics", "vehicles"],
    queryFn: searchVehicles,
    enabled: canCreate && perms.includes(LOGISTICS_PERMISSIONS.vehiclesView),
  });
  const drivers = useQuery({
    queryKey: ["logistics", "drivers"],
    queryFn: searchDrivers,
    enabled: canCreate && perms.includes(LOGISTICS_PERMISSIONS.driversView),
  });

  useEffect(() => {
    if (!routeId && routes.data?.[0]) setRouteId(routes.data[0].id);
  }, [routeId, routes.data]);
  useEffect(() => {
    if (!vehicleId && vehicles.data?.[0]) setVehicleId(vehicles.data[0].id);
  }, [vehicleId, vehicles.data]);
  useEffect(() => {
    if (!driverId && drivers.data?.[0]) setDriverId(drivers.data[0].id);
  }, [driverId, drivers.data]);

  const create = useMutation({
    mutationFn: (input: { routeId: string; warehouseId: string; vehicleId: string; driverId: string; businessDate: string }) =>
      createShipment(input, newIdempotencyKey()),
    onSuccess: async () => {
      toast.success(t("ops.shipmentCreated", "Shipment created"));
      await queryClient.invalidateQueries({ queryKey: ["logistics", "shipments"] });
    },
    onError: (error) => toast.error(describe(error)),
  });

  if (!canCreate) return null;

  return (
    <JobCard>
      <p className="mb-3 text-[13px] font-medium">{t("ops.newShipment", "New shipment")}</p>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field id="ship-route" label={t("ops.route", "Route")} required>
          <Combobox
            label={t("ops.route", "Route")}
            searchable
            value={routeId}
            onChange={(id) => {
              setRouteId(id);
              const route = (routes.data ?? []).find((r) => r.id === id);
              if (route?.defaultVehicleId) setVehicleId(route.defaultVehicleId);
            }}
            placeholder={t("ops.chooseRoute", "Choose a route")}
            options={(routes.data ?? []).map((r) => ({ value: r.id, label: r.code }))}
          />
        </Field>
        <Field id="ship-vehicle" label={t("ops.vehicle", "Vehicle")} required>
          <Combobox
            label={t("ops.vehicle", "Vehicle")}
            searchable
            value={vehicleId}
            onChange={setVehicleId}
            placeholder={t("ops.chooseVehicle", "Choose a vehicle")}
            options={(vehicles.data ?? []).map((v) => ({ value: v.id, label: v.plate, hint: v.compartmentZones }))}
          />
        </Field>
        <Field id="ship-driver" label={t("ops.driver", "Driver")} required>
          <Combobox
            label={t("ops.driver", "Driver")}
            searchable
            value={driverId}
            onChange={setDriverId}
            placeholder={t("ops.chooseDriver", "Choose a driver")}
            options={(drivers.data ?? []).map((d) => ({ value: d.id, label: d.phone }))}
          />
        </Field>
        <Field id="ship-date" label={t("ops.businessDate", "Business date")} required>
          <Input id="ship-date" type="date" value={businessDate} onChange={(e) => setBusinessDate(e.target.value)} />
        </Field>
      </div>
      <Button
        className="mt-3"
        data-testid="shipment-create"
        disabled={create.isPending || !routeId || !vehicleId || !driverId}
        onClick={() =>
          create.mutate({
            routeId: routeId!,
            warehouseId,
            vehicleId: vehicleId!,
            driverId: driverId!,
            businessDate,
          })
        }
      >
        {t("ops.createShipment", "Create shipment")}
      </Button>
    </JobCard>
  );
}

export function ShipmentsPage() {
  const t = useT();
  const { warehouses, warehouseId, setWarehouseId, isLoading, isError, error } = useOpsWarehouse();
  const query = useQuery({
    queryKey: ["logistics", "shipments", warehouseId],
    queryFn: () => searchShipments(warehouseId!),
    enabled: !!warehouseId,
  });

  return (
    <div className="space-y-4 sm:space-y-6">
      <EntityPageHeader
        icon={Truck}
        title={t("ops.shipmentsTitle", "Load & delivery")}
        description={t("ops.shipmentsDescription", "Build a route shipment, scan an order or tote onto the truck, depart, then capture POD with photos.")}
      />
      <WarehousePicker
        warehouses={warehouses}
        warehouseId={warehouseId}
        onChange={setWarehouseId}
        loading={isLoading}
      />
      {isError ? <ErrorBand message={describe(error)} /> : null}
      {query.isError ? <ErrorBand message={describe(query.error)} /> : null}
      {warehouseId ? <CreateShipmentForm warehouseId={warehouseId} /> : null}
      {(query.data ?? []).length === 0 && !query.isLoading ? (
        <EntityEmpty
          icon={Truck}
          title={t("ops.noShipments", "No shipments")}
          body={t("ops.noShipmentsBody", "Create a shipment for packed orders on a route, then load it here.")}
        />
      ) : (
        <div className="space-y-3">
          {(query.data ?? []).map((shipment) => (
            <ShipmentCard key={shipment.id} shipment={shipment} />
          ))}
        </div>
      )}
    </div>
  );
}
