import { useRef, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Truck } from "lucide-react";
import { createVehicle, searchVehicles } from "@/api/logistics";
import { useAuth } from "@/auth/use-auth";
import { EntityPageHeader, ErrorBand, Field, LoadingRow } from "@/components/list";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { useT } from "@/i18n/locale-provider";
import { LogisticsPermissions } from "@/lib/permissions";
import { describe } from "@/pages/customers/request-error";

const key = ["logistics", "vehicles"] as const;
export function VehiclesPage() {
  const t = useT();
  const { user } = useAuth();
  const [creating, setCreating] = useState(false);
  const canView = !!user?.permissions.includes(LogisticsPermissions.Vehicles.View);
  const canCreate = canView && !!user?.permissions.includes(LogisticsPermissions.Vehicles.Create);
  const query = useQuery({ queryKey: key, queryFn: ({ signal }) => searchVehicles(signal), enabled: canView });
  return <div className="space-y-6">
    <EntityPageHeader icon={Truck} title={t("vehicles.title")} description={t("vehicles.description")}>
      {canCreate && <Button onClick={() => setCreating(true)}>{t("vehicles.create")}</Button>}
    </EntityPageHeader>
    {query.isPending && <LoadingRow label={t("vehicles.loading")} />}
    {query.isError && <div className="space-y-2"><ErrorBand message={describe(query.error, t("vehicles.failed"))} /><Button variant="outline" onClick={() => void query.refetch()}>{t("workbench.retry")}</Button></div>}
    {query.isSuccess && query.data.length === 0 && <p role="status">{t("vehicles.empty")}</p>}
    {query.isSuccess && <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{query.data.map(vehicle => <article key={vehicle.id} className="min-w-0 space-y-3 rounded-xl border p-5">
      <h2 className="break-all font-semibold">{vehicle.plate}</h2>
      <dl className="space-y-2 text-sm">
        <div><dt>{t("vehicles.zones")}</dt><dd className="break-all">{vehicle.compartmentZones}</dd></div>
        <div><dt>{t("vehicles.payload")}</dt><dd>{vehicle.payloadKg}</dd></div>
      </dl>
    </article>)}</div>}
    {creating && canCreate && <CreateVehicleDialog onClose={() => setCreating(false)} />}
  </div>;
}

function CreateVehicleDialog({ onClose }: { onClose: () => void }) {
  const t = useT();
  const cache = useQueryClient();
  const [plate, setPlate] = useState("");
  const [zones, setZones] = useState("");
  const [payload, setPayload] = useState("");
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const mutation = useMutation({ mutationFn: createVehicle, onSuccess: async () => {
    await cache.invalidateQueries({ queryKey: key }); onClose();
  } });
  const valid = !!plate.trim() && !!zones.trim() && Number.isFinite(Number(payload)) && Number(payload) > 0;
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!valid || mutation.isPending) return;
    const body = { plate: plate.trim(), compartmentZones: zones.trim(), payloadKg: Number(payload) };
    const serialized = JSON.stringify(body);
    if (attempt.current?.body !== serialized) attempt.current = { body: serialized, key: crypto.randomUUID() };
    mutation.mutate({ body, key: attempt.current.key });
  }
  return <Dialog open onOpenChange={open => !open && !mutation.isPending && onClose()}><DialogContent>
    <DialogHeader><DialogTitle>{t("vehicles.create")}</DialogTitle><DialogDescription>{t("vehicles.createHint")}</DialogDescription></DialogHeader>
    <form onSubmit={submit}><DialogBody className="space-y-4"><fieldset disabled={mutation.isPending} className="space-y-4">
      <Field id="vehicle-plate" label={t("vehicles.plate")} required><Input id="vehicle-plate" required maxLength={16} value={plate} onChange={event => setPlate(event.target.value)} /></Field>
      <Field id="vehicle-zones" label={t("vehicles.zones")} required><Input id="vehicle-zones" required maxLength={64} value={zones} onChange={event => setZones(event.target.value)} /></Field>
      <Field id="vehicle-payload" label={t("vehicles.payload")} required><Input id="vehicle-payload" type="number" required min="0" step="any" value={payload} onChange={event => setPayload(event.target.value)} /></Field>
    </fieldset>{mutation.isError && <ErrorBand message={describe(mutation.error, t("vehicles.failed"))} />}</DialogBody>
    <DialogFooter><Button type="button" variant="outline" onClick={onClose} disabled={mutation.isPending}>{t("chrome.cancel")}</Button><Button type="submit" disabled={!valid || mutation.isPending}>{t("vehicles.save")}</Button></DialogFooter></form>
  </DialogContent></Dialog>;
}
